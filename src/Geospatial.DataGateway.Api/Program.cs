using System.Text.Json;
using Geospatial.DataGateway.Api;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using NpgsqlTypes;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgis")
    ?? builder.Configuration["POSTGIS_CONNECTION_STRING"]
    ?? "Host=localhost;Port=5432;Database=geostatusboard;Username=gsb;Password=gsb";

var metadataSchema = GatewayValidators.RequireIdentifier(
    builder.Configuration["Gateway:MetadataSchema"] ?? "geomain",
    "metadata schema");
var defaultFeatureSchema = GatewayValidators.RequireIdentifier(
    builder.Configuration["Gateway:DefaultFeatureSchema"] ?? "public",
    "default feature schema");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSignalR();

var app = builder.Build();

await GatewaySchema.EnsureCreatedAsync(app.Services.GetRequiredService<NpgsqlDataSource>(), metadataSchema);

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "geospatial-data-gateway",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/datasets", async (NpgsqlDataSource db, CancellationToken cancellationToken) =>
{
    var datasets = new List<DatasetResponse>();
    await using var command = db.CreateCommand($"""
        SELECT dataset_id, name, description, source_uri, source_type, created_at, updated_at
        FROM {metadataSchema}.datasets
        ORDER BY updated_at DESC, name ASC
        """);
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
        datasets.Add(DatasetResponse.FromReader(reader));
    }

    return Results.Ok(new { datasets });
});

app.MapPost("/datasets", async (
    CreateDatasetRequest request,
    NpgsqlDataSource db,
    IHubContext<GeospatialUpdatesHub> hub,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new ProblemMessage("Dataset name is required."));
    }

    var datasetId = Guid.NewGuid();
    await using var command = db.CreateCommand($"""
        INSERT INTO {metadataSchema}.datasets
            (dataset_id, name, description, source_uri, source_type)
        VALUES
            ($1, $2, $3, $4, $5)
        ON CONFLICT (name) DO UPDATE SET
            description = EXCLUDED.description,
            source_uri = EXCLUDED.source_uri,
            source_type = EXCLUDED.source_type,
            updated_at = now()
        RETURNING dataset_id, name, description, source_uri, source_type, created_at, updated_at
        """);
    command.Parameters.AddWithValue(datasetId);
    command.Parameters.AddWithValue(request.Name.Trim());
    command.Parameters.AddNullableText(request.Description);
    command.Parameters.AddNullableText(request.SourceUri);
    command.Parameters.AddNullableText(request.SourceType);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    await reader.ReadAsync(cancellationToken);
    var dataset = DatasetResponse.FromReader(reader);

    await hub.Clients.All.SendAsync("dataset.updated", dataset, cancellationToken);
    return Results.Created($"/datasets/{dataset.DatasetId}", dataset);
});

app.MapGet("/ingest-jobs", async (NpgsqlDataSource db, int? limit, CancellationToken cancellationToken) =>
{
    var boundedLimit = Math.Clamp(limit ?? 100, 1, 500);
    var jobs = new List<IngestJobResponse>();
    await using var command = db.CreateCommand($"""
        SELECT job_id, dataset_id, dataset_name, source_uri, source_type, target_schema,
               target_table, if_exists, status, feature_count, geometry_type, source_crs,
               target_crs, bounds, notes, error_message, created_at, started_at, finished_at,
               updated_at
        FROM {metadataSchema}.ingest_jobs
        ORDER BY created_at DESC
        LIMIT $1
        """);
    command.Parameters.AddWithValue(boundedLimit);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
        jobs.Add(IngestJobResponse.FromReader(reader));
    }

    return Results.Ok(new { jobs });
});

app.MapGet("/ingest-jobs/{jobId:guid}", async (
    Guid jobId,
    NpgsqlDataSource db,
    CancellationToken cancellationToken) =>
{
    await using var command = db.CreateCommand($"""
        SELECT job_id, dataset_id, dataset_name, source_uri, source_type, target_schema,
               target_table, if_exists, status, feature_count, geometry_type, source_crs,
               target_crs, bounds, notes, error_message, created_at, started_at, finished_at,
               updated_at
        FROM {metadataSchema}.ingest_jobs
        WHERE job_id = $1
        """);
    command.Parameters.AddWithValue(jobId);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    if (!await reader.ReadAsync(cancellationToken))
    {
        return Results.NotFound(new ProblemMessage("Ingest job was not found."));
    }

    return Results.Ok(IngestJobResponse.FromReader(reader));
});

app.MapPost("/ingest-jobs", async (
    CreateIngestJobRequest request,
    NpgsqlDataSource db,
    IHubContext<GeospatialUpdatesHub> hub,
    CancellationToken cancellationToken) =>
{
    var validation = request.Validate(defaultFeatureSchema);
    if (validation is not null)
    {
        return Results.BadRequest(validation);
    }

    var jobId = Guid.NewGuid();
    var targetSchema = GatewayValidators.RequireIdentifier(request.TargetSchema ?? defaultFeatureSchema, "target schema");
    var targetTable = GatewayValidators.RequireIdentifier(request.TargetTable!, "target table");
    var ifExists = string.IsNullOrWhiteSpace(request.IfExists) ? "fail" : request.IfExists.Trim().ToLowerInvariant();

    await using var command = db.CreateCommand($"""
        INSERT INTO {metadataSchema}.ingest_jobs
            (job_id, dataset_name, source_uri, source_type, target_schema, target_table, if_exists, notes)
        VALUES
            ($1, $2, $3, $4, $5, $6, $7, $8)
        RETURNING job_id, dataset_id, dataset_name, source_uri, source_type, target_schema,
                  target_table, if_exists, status, feature_count, geometry_type, source_crs,
                  target_crs, bounds, notes, error_message, created_at, started_at, finished_at,
                  updated_at
        """);
    command.Parameters.AddWithValue(jobId);
    command.Parameters.AddNullableText(request.DatasetName);
    command.Parameters.AddWithValue(request.SourceUri!.Trim());
    command.Parameters.AddNullableText(request.SourceType);
    command.Parameters.AddWithValue(targetSchema);
    command.Parameters.AddWithValue(targetTable);
    command.Parameters.AddWithValue(ifExists);
    command.Parameters.AddNullableText(request.Notes);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    await reader.ReadAsync(cancellationToken);
    var job = IngestJobResponse.FromReader(reader);

    await hub.Clients.All.SendAsync("ingest.job.created", job, cancellationToken);
    return Results.Accepted($"/ingest-jobs/{jobId}", job);
});

app.MapPatch("/ingest-jobs/{jobId:guid}", async (
    Guid jobId,
    UpdateIngestJobRequest request,
    NpgsqlDataSource db,
    IHubContext<GeospatialUpdatesHub> hub,
    CancellationToken cancellationToken) =>
{
    var validation = request.Validate();
    if (validation is not null)
    {
        return Results.BadRequest(validation);
    }

    await using var command = db.CreateCommand($"""
        UPDATE {metadataSchema}.ingest_jobs
        SET status = COALESCE($2, status),
            feature_count = COALESCE($3, feature_count),
            geometry_type = COALESCE($4, geometry_type),
            source_crs = COALESCE($5, source_crs),
            target_crs = COALESCE($6, target_crs),
            bounds = COALESCE($7, bounds),
            error_message = COALESCE($8, error_message),
            started_at = CASE WHEN $2 = 'running' AND started_at IS NULL THEN now() ELSE started_at END,
            finished_at = CASE WHEN $2 IN ('loaded', 'failed', 'cancelled') THEN now() ELSE finished_at END,
            updated_at = now()
        WHERE job_id = $1
        RETURNING job_id, dataset_id, dataset_name, source_uri, source_type, target_schema,
                  target_table, if_exists, status, feature_count, geometry_type, source_crs,
                  target_crs, bounds, notes, error_message, created_at, started_at, finished_at,
                  updated_at
        """);
    command.Parameters.AddWithValue(jobId);
    command.Parameters.AddNullableText(request.Status);
    command.Parameters.AddNullableLong(request.FeatureCount);
    command.Parameters.AddNullableText(request.GeometryType);
    command.Parameters.AddNullableText(request.SourceCrs);
    command.Parameters.AddNullableText(request.TargetCrs);
    command.Parameters.AddNullableJson(request.Bounds);
    command.Parameters.AddNullableText(request.ErrorMessage);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    if (!await reader.ReadAsync(cancellationToken))
    {
        return Results.NotFound(new ProblemMessage("Ingest job was not found."));
    }

    var job = IngestJobResponse.FromReader(reader);
    await hub.Clients.All.SendAsync("ingest.job.updated", job, cancellationToken);
    if (job.Status == "loaded")
    {
        await hub.Clients.All.SendAsync("layer.refresh_requested", new
        {
            job.JobId,
            job.TargetSchema,
            job.TargetTable,
            job.FeatureCount
        }, cancellationToken);
    }

    return Results.Ok(job);
});

app.MapPost("/ingest-jobs/{jobId:guid}/events", async (
    Guid jobId,
    CreateIngestEventRequest request,
    NpgsqlDataSource db,
    IHubContext<GeospatialUpdatesHub> hub,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.EventType))
    {
        return Results.BadRequest(new ProblemMessage("Event type is required."));
    }

    await using var command = db.CreateCommand($"""
        INSERT INTO {metadataSchema}.ingest_events
            (job_id, event_type, message, payload)
        VALUES
            ($1, $2, $3, $4)
        RETURNING event_id, created_at
        """);
    command.Parameters.AddWithValue(jobId);
    command.Parameters.AddWithValue(request.EventType.Trim());
    command.Parameters.AddNullableText(request.Message);
    command.Parameters.AddNullableJson(request.Payload);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    await reader.ReadAsync(cancellationToken);
    var ingestEvent = new
    {
        EventId = reader.GetInt64(0),
        JobId = jobId,
        request.EventType,
        request.Message,
        request.Payload,
        CreatedAt = reader.GetDateTime(1)
    };

    await hub.Clients.All.SendAsync(request.EventType, ingestEvent, cancellationToken);
    return Results.Accepted($"/ingest-jobs/{jobId}/events/{ingestEvent.EventId}", ingestEvent);
});

app.MapHub<GeospatialUpdatesHub>("/hubs/geospatial-updates");

app.Run();
