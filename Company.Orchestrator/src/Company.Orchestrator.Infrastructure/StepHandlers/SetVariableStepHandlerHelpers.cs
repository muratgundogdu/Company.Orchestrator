using System.Globalization;
using System.Text.Json;
using Company.Orchestrator.Application.Models;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

internal static class SetVariableStepHandlerHelpers
{
    public static readonly HashSet<string> SupportedValueTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string", "number", "boolean", "json",
    };

    public static readonly HashSet<string> SupportedModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "literal", "expression",
    };

    public static object CoerceValue(string interpolated, string valueType, string stepType) =>
        CoerceObject(interpolated, valueType, stepType);

    public static object CoerceObject(object value, string valueType, string stepType) =>
        valueType switch
        {
            "string"  => ValueToString(value),
            "number"  => CoerceToNumber(value, stepType),
            "boolean" => CoerceToBoolean(value, stepType),
            "json"    => CoerceToJson(value, stepType),
            _         => ValueToString(value),
        };

    public static int ValueLength(object value) =>
        value switch
        {
            string s       => s.Length,
            bool b           => b ? 4 : 5,
            JsonElement je   => je.GetRawText().Length,
            _                => ValueToString(value).Length,
        };

    public static string NormalizeVariableName(string name) =>
        name.Trim().Trim('{', '}');

    public static string GetString(
        IReadOnlyDictionary<string, object> config,
        string key,
        string fallback = "")
    {
        if (!config.TryGetValue(key, out var raw) || raw is null)
            return fallback;

        if (raw is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String  => el.GetString() ?? fallback,
                JsonValueKind.Number  => el.GetRawText(),
                JsonValueKind.True    => "true",
                JsonValueKind.False   => "false",
                JsonValueKind.Null    => fallback,
                _                     => el.GetRawText(),
            };
        }

        return raw.ToString() ?? fallback;
    }

    private static object CoerceToNumber(object value, string stepType)
    {
        switch (value)
        {
            case byte b:     return (long)b;
            case sbyte sb:   return (long)sb;
            case short s:    return (long)s;
            case ushort us:  return (long)us;
            case int i:      return (long)i;
            case uint ui:    return (long)ui;
            case long l:     return l;
            case ulong ul:   return (long)ul;
            case float f:    return (double)f;
            case double d:   return d;
            case decimal m:  return (double)m;
        }

        var text = ValueToString(value).Trim();
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            return integer;

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return number;

        throw new InvalidOperationException($"{stepType}: value '{text}' is not a valid number.");
    }

    private static bool CoerceToBoolean(object value, string stepType)
    {
        switch (value)
        {
            case bool boolValue:
                return boolValue;
            case int i:
                return i != 0;
            case long l:
                return l != 0;
            case double d:
                return Math.Abs(d) > double.Epsilon;
        }

        var text = ValueToString(value).Trim();
        if (bool.TryParse(text, out var parsedBool))
            return parsedBool;

        if (text.Equals("1", StringComparison.OrdinalIgnoreCase)
            || text.Equals("yes", StringComparison.OrdinalIgnoreCase))
            return true;

        if (text.Equals("0", StringComparison.OrdinalIgnoreCase)
            || text.Equals("no", StringComparison.OrdinalIgnoreCase))
            return false;

        throw new InvalidOperationException($"{stepType}: value '{text}' is not a valid boolean.");
    }

    private static string CoerceToJson(object value, string stepType)
    {
        if (value is string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException($"{stepType}: JSON value cannot be empty.");

            try
            {
                using var doc = JsonDocument.Parse(text);
                return doc.RootElement.GetRawText();
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"{stepType}: invalid JSON value: {ex.Message}");
            }
        }

        try
        {
            return JsonSerializer.Serialize(value);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{stepType}: cannot serialize result to JSON: {ex.Message}");
        }
    }

    private static string ValueToString(object? value) =>
        value switch
        {
            null           => string.Empty,
            string s       => s,
            bool b         => b ? "true" : "false",
            JsonElement je => je.ValueKind switch
            {
                JsonValueKind.String  => je.GetString() ?? string.Empty,
                JsonValueKind.Number  => je.GetRawText(),
                JsonValueKind.True    => "true",
                JsonValueKind.False   => "false",
                JsonValueKind.Null    => string.Empty,
                _                     => je.GetRawText(),
            },
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
}
