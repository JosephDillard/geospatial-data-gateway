using System.Data.Common;
using System.Text.Json;

namespace Geospatial.DataGateway.Api;

public sealed record ProblemMessage(string Message);

public sealed record CreateDatasetRequest(
    string Name,
    string? Description,
    string? SourceUri,
    string? SourceType);

public sealed record CreateIngestJobRequest(
    string? DatasetName,
    string? SourceUri,
    string? SourceType,
    string? TargetSchema,
    string? TargetTable,
    string? IfExists,
    string? Notes)
{
    public ProblemMessage? Validate(string defaultFeatureSchema)
    {
        if (string.IsNullOrWhiteSpace(SourceUri))
        {
            return new ProblemMessage("Source URI is required.");
        }

        if (string.IsNullOrWhiteSpace(TargetTable))
        {
            return new ProblemMessage("Target table is required.");
        }

        try
        {
            GatewayValidators.RequireIdentifier(TargetSchema ?? defaultFeatureSchema, "target schema");
            GatewayValidators.RequireIdentifier(TargetTable, "target table");
        }
        catch (ArgumentException ex)
        {
            return new ProblemMessage(ex.Message);
        }

        var ifExists = string.IsNullOrWhiteSpace(IfExists) ? "fail" : IfExists.Trim().ToLowerInvariant();
        return GatewayValidators.ValidIfExists.Contains(ifExists)
            ? null
            : new ProblemMessage("ifExists must be one of: fail, replace, append.");
    }
}

public sealed record UpdateIngestJobRequest(
    string? Status,
    long? FeatureCount,
    string? GeometryType,
    string? SourceCrs,
    string? TargetCrs,
    double[]? Bounds,
    string? ErrorMessage)
{
    public ProblemMessage? Validate()
    {
        if (!string.IsNullOrWhiteSpace(Status) &&
            !GatewayValidators.ValidStatuses.Contains(Status.Trim().ToLowerInvariant()))
        {
            return new ProblemMessage("Status must be one of: queued, running, loaded, failed, cancelled.");
        }

        return Bounds is { Length: not 4 }
            ? new ProblemMessage("Bounds must contain [minX, minY, maxX, maxY].")
            : null;
    }
}

public sealed record CreateIngestEventRequest(
    string? EventType,
    string? Message,
    JsonElement? Payload);

public sealed record DatasetResponse(
    Guid DatasetId,
    string Name,
    string? Description,
    string? SourceUri,
    string? SourceType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static DatasetResponse FromReader(DbDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetNullableString(2),
        reader.GetNullableString(3),
        reader.GetNullableString(4),
        reader.GetDateTime(5),
        reader.GetDateTime(6));
}

public sealed record IngestJobResponse(
    Guid JobId,
    Guid? DatasetId,
    string? DatasetName,
    string SourceUri,
    string? SourceType,
    string TargetSchema,
    string TargetTable,
    string IfExists,
    string Status,
    long FeatureCount,
    string? GeometryType,
    string? SourceCrs,
    string TargetCrs,
    JsonElement? Bounds,
    string? Notes,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset UpdatedAt)
{
    public static IngestJobResponse FromReader(DbDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetNullableGuid(1),
        reader.GetNullableString(2),
        reader.GetString(3),
        reader.GetNullableString(4),
        reader.GetString(5),
        reader.GetString(6),
        reader.GetString(7),
        reader.GetString(8),
        reader.GetInt64(9),
        reader.GetNullableString(10),
        reader.GetNullableString(11),
        reader.GetString(12),
        reader.GetNullableJson(13),
        reader.GetNullableString(14),
        reader.GetNullableString(15),
        reader.GetDateTime(16),
        reader.GetNullableDateTime(17),
        reader.GetNullableDateTime(18),
        reader.GetDateTime(19));
}
