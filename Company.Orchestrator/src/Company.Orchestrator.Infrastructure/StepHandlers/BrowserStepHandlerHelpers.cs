using Company.Orchestrator.Application.Models;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

internal static class BrowserStepHandlerHelpers
{
    public static string GetSessionName(IReadOnlyDictionary<string, object> config, WorkflowContext context)
    {
        var raw = config.GetValueOrDefault("sessionName")?.ToString() ?? "default";
        return context.Interpolate(raw);
    }

    public static bool TryRequire(
        WorkflowContext context,
        IReadOnlyDictionary<string, object> config,
        string key,
        out string value,
        out StepResult? failure)
    {
        failure = null;
        value = string.Empty;

        if (!config.TryGetValue(key, out var raw) || raw is null)
        {
            failure = StepResult.Fail($"Browser step: '{key}' is required.");
            return false;
        }

        var text = raw.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            failure = StepResult.Fail($"Browser step: '{key}' is required.");
            return false;
        }

        value = context.Interpolate(text);
        return true;
    }

    public static bool ParseBool(object? value, bool defaultValue = false)
    {
        if (value is null) return defaultValue;
        if (value is bool b) return b;
        var s = value.ToString();
        if (string.IsNullOrWhiteSpace(s)) return defaultValue;
        return s.Equals("true", StringComparison.OrdinalIgnoreCase)
            || s.Equals("1", StringComparison.OrdinalIgnoreCase)
            || s.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    public static int ParseInt(object? value, int defaultValue)
    {
        if (value is null) return defaultValue;
        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is double d) return (int)d;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : defaultValue;
    }

    /// <summary>
    /// Ensures screenshot artifact names include a .png extension for download/open compatibility.
    /// Names that already end with .png (any casing) are returned unchanged.
    /// </summary>
    public static int ParseTimeoutMs(object? value, int defaultValue, string stepType)
    {
        var timeoutMs = ParseInt(value, defaultValue);
        if (timeoutMs <= 0)
            throw new InvalidOperationException($"{stepType}: 'timeoutMs' must be greater than 0.");
        return timeoutMs;
    }

    public static string ResolveUrlPattern(IReadOnlyDictionary<string, object> config, WorkflowContext context)
    {
        var urlContains = config.GetValueOrDefault("urlContains")?.ToString();
        var pattern     = config.GetValueOrDefault("pattern")?.ToString();

        var raw = !string.IsNullOrWhiteSpace(urlContains) ? urlContains : pattern;
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("URL pattern is required.");

        return context.Interpolate(raw);
    }

    public static string ResolveMatchMode(IReadOnlyDictionary<string, object> config) =>
        config.GetValueOrDefault("matchMode")?.ToString()?.Trim().ToLowerInvariant() ?? "contains";

    public static string SanitizeForLog(string pathOrName) =>
        string.IsNullOrWhiteSpace(pathOrName) ? string.Empty : Path.GetFileName(pathOrName.Trim());

    /// <summary>
    /// Ensures screenshot artifact names include a .png extension for download/open compatibility.
    /// Names that already end with .png (any casing) are returned unchanged.
    /// </summary>
    public static string EnsurePngExtension(string name)
    {
        if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            return name;
        return name + ".png";
    }
}
