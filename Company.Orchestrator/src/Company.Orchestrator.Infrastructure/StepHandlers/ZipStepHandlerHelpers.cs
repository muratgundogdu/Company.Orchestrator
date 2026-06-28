using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Company.Orchestrator.Application.Models;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

internal static class ZipStepHandlerHelpers
{
    private static readonly HashSet<string> SupportedCompressionLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "fastest", "optimal", "nocompression",
    };

    public static CompressionLevel ResolveCompressionLevel(string value)
    {
        var level = value.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(level))
            level = "optimal";

        if (!SupportedCompressionLevels.Contains(level))
        {
            throw new InvalidOperationException(
                "compressionLevel must be 'fastest', 'optimal', or 'noCompression'.");
        }

        return level switch
        {
            "fastest"        => CompressionLevel.Fastest,
            "nocompression"  => CompressionLevel.NoCompression,
            _                => CompressionLevel.Optimal,
        };
    }

    public static List<string> ResolveArtifactNames(object raw, WorkflowContext context)
    {
        if (raw is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
            {
                return je.EnumerateArray()
                    .Select(e => context.Interpolate(
                        e.ValueKind == JsonValueKind.String ? e.GetString()! : e.GetRawText()))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }

            if (je.ValueKind == JsonValueKind.String)
                return ResolveCollection(context.Interpolate(je.GetString() ?? ""));
        }

        var str = raw.ToString() ?? string.Empty;
        return ResolveCollection(context.Interpolate(str));
    }

    public static bool MatchesWildcardPattern(string fileName, string pattern)
    {
        if (string.IsNullOrEmpty(pattern) || pattern == "*.*")
            return true;

        var regex = "^"
            + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".")
            + "$";
        return Regex.IsMatch(fileName, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static bool IsSafeZipEntryPath(string entryPath)
    {
        if (string.IsNullOrWhiteSpace(entryPath))
            return false;

        var normalized = entryPath.Replace('\\', '/').Trim();
        if (normalized.StartsWith("/", StringComparison.Ordinal))
            return false;

        if (Path.IsPathRooted(normalized))
            return false;

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.All(segment => segment != "..");
    }

    public static bool IsDirectoryEntry(string entryPath) =>
        entryPath.EndsWith('/') || entryPath.EndsWith('\\');

    public static string GetEntryFileName(string entryPath)
    {
        var normalized = entryPath.Replace('\\', '/').TrimEnd('/');
        var lastSlash  = normalized.LastIndexOf('/');
        return lastSlash >= 0 ? normalized[(lastSlash + 1)..] : normalized;
    }

    public static string BuildArtifactName(string outputPrefix, string entryFileName)
    {
        var prefix = outputPrefix.Trim().TrimEnd('-');
        return string.IsNullOrEmpty(prefix)
            ? entryFileName
            : $"{prefix}-{entryFileName}";
    }

    public static string MakeUniqueName(string baseName, ISet<string> usedNames)
    {
        if (usedNames.Add(baseName))
            return baseName;

        var extension = Path.GetExtension(baseName);
        var nameWithoutExt = string.IsNullOrEmpty(extension)
            ? baseName
            : baseName[..^extension.Length];

        for (var i = 2; ; i++)
        {
            var candidate = $"{nameWithoutExt}-{i}{extension}";
            if (usedNames.Add(candidate))
                return candidate;
        }
    }

    public static string MakeUniqueZipEntryName(string entryName, ISet<string> usedNames)
    {
        var normalized = entryName.Replace('\\', '/');
        if (usedNames.Add(normalized))
            return normalized;

        var fileName = GetEntryFileName(normalized);
        var directory = normalized.Length > fileName.Length
            ? normalized[..^(fileName.Length)]
            : string.Empty;

        var extension = Path.GetExtension(fileName);
        var nameWithoutExt = string.IsNullOrEmpty(extension)
            ? fileName
            : fileName[..^extension.Length];

        for (var i = 2; ; i++)
        {
            var uniqueFileName = $"{nameWithoutExt}-{i}{extension}";
            var candidate = $"{directory}{uniqueFileName}";
            if (usedNames.Add(candidate))
                return candidate;
        }
    }

    public static Dictionary<string, object> BuildArtifactArrayOutputs(string outputVar, IReadOnlyList<string> names)
    {
        var json = JsonSerializer.Serialize(names);
        var output = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [outputVar]            = json,
            [$"{outputVar}_count"] = names.Count,
            [$"{outputVar}_first"] = names.Count > 0 ? names[0] : string.Empty,
        };

        for (var i = 0; i < Math.Min(names.Count, 10); i++)
            output[$"{outputVar}_{i}"] = names[i];

        return output;
    }

    public static string ResolveMimeType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".zip"  => "application/zip",
            ".pdf"  => "application/pdf",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls"  => "application/vnd.ms-excel",
            ".csv"  => "text/csv",
            ".json" => "application/json",
            ".xml"  => "application/xml",
            ".txt"  => "text/plain",
            ".html" => "text/html",
            ".htm"  => "text/html",
            ".png"  => "image/png",
            ".jpg"  => "image/jpeg",
            ".jpeg" => "image/jpeg",
            _       => "application/octet-stream",
        };

    public static bool GetBool(Dictionary<string, object> config, string key, bool defaultValue)
    {
        if (!config.TryGetValue(key, out var raw) || raw is null)
            return defaultValue;

        if (raw is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.True   => true,
                JsonValueKind.False  => false,
                JsonValueKind.String => bool.TryParse(je.GetString(), out var parsed) ? parsed : defaultValue,
                _                    => defaultValue,
            };
        }

        if (raw is bool flag)
            return flag;

        return bool.TryParse(raw.ToString(), out var boolVal) ? boolVal : defaultValue;
    }

    public static string GetString(Dictionary<string, object> config, string key, string fallback = "")
    {
        if (!config.TryGetValue(key, out var raw) || raw is null)
            return fallback;

        if (raw is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString() ?? fallback,
                JsonValueKind.Number => el.GetRawText(),
                JsonValueKind.True   => "true",
                JsonValueKind.False  => "false",
                _                    => el.GetRawText(),
            };
        }

        return raw.ToString() ?? fallback;
    }

    private static List<string> ResolveCollection(string resolved)
    {
        if (string.IsNullOrWhiteSpace(resolved))
            return [];

        var trimmed = resolved.Trim();

        if (trimmed.StartsWith('['))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(trimmed);
                if (list is { Count: > 0 })
                    return list.Where(s => !string.IsNullOrWhiteSpace(s)).ToList()!;
            }
            catch { /* fall through */ }

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return doc.RootElement.EnumerateArray()
                        .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()! : e.GetRawText())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
                }
            }
            catch { /* fall through */ }
        }

        if (trimmed.Contains(','))
        {
            return trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        return [trimmed];
    }
}
