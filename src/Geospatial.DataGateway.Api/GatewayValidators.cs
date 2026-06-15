using System.Text.RegularExpressions;

namespace Geospatial.DataGateway.Api;

public static partial class GatewayValidators
{
    public static readonly HashSet<string> ValidIfExists = new(StringComparer.OrdinalIgnoreCase)
    {
        "fail",
        "replace",
        "append"
    };

    public static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "queued",
        "running",
        "loaded",
        "failed",
        "cancelled"
    };

    public static string RequireIdentifier(string value, string label)
    {
        var trimmed = value.Trim();
        if (!SqlIdentifierPattern().IsMatch(trimmed))
        {
            throw new ArgumentException($"{label} must be a valid unquoted SQL identifier.");
        }

        return trimmed;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex SqlIdentifierPattern();
}
