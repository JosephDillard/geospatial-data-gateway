using System.Data.Common;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Geospatial.DataGateway.Api;

public static class NpgsqlExtensions
{
    public static void AddWithValue(this NpgsqlParameterCollection parameters, object? value)
    {
        AddParameter(parameters, value);
    }

    public static void AddNullableText(this NpgsqlParameterCollection parameters, string? value)
    {
        AddParameter(parameters, string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim());
    }

    public static void AddNullableLong(this NpgsqlParameterCollection parameters, long? value)
    {
        AddParameter(parameters, value.HasValue ? value.Value : DBNull.Value);
    }

    public static void AddNullableJson(this NpgsqlParameterCollection parameters, JsonElement? value)
    {
        var parameter = new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Jsonb,
            Value = value.HasValue ? value.Value.GetRawText() : DBNull.Value
        };
        parameters.Add(parameter);
    }

    private static void AddParameter(NpgsqlParameterCollection parameters, object? value)
    {
        parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value });
    }

    public static void AddNullableJson(this NpgsqlParameterCollection parameters, double[]? bounds)
    {
        var parameter = new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Jsonb,
            Value = bounds is null ? DBNull.Value : JsonSerializer.Serialize(bounds)
        };
        parameters.Add(parameter);
    }

    public static string? GetNullableString(this DbDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static Guid? GetNullableGuid(this DbDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    public static DateTimeOffset? GetNullableDateTime(this DbDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    public static JsonElement? GetNullableJson(this DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var json = reader.GetString(ordinal);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
