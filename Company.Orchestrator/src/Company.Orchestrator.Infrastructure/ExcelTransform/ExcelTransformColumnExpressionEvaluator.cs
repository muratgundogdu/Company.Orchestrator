using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Company.Orchestrator.Infrastructure.ExcelTransform;

/// <summary>
/// Evaluates transformColumn expressions with access to the current cell value,
/// same-row column values, and workflow variables.
/// </summary>
public static class ExcelTransformColumnExpressionEvaluator
{
    private static readonly Regex FunctionCallRegex =
        new(@"(\w+)\(([^()]*)\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RowAccessRegex =
        new(@"^row\s*\[\s*""([^""]+)""\s*\]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RowAccessSingleQuoteRegex =
        new(@"^row\s*\[\s*'([^']+)'\s*\]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static object Evaluate(string expression, string rawValue)
        => Evaluate(expression, new ExcelTransformExpressionContext { Value = rawValue });

    public static object Evaluate(string expression, ExcelTransformExpressionContext context)
    {
        var expr = expression.Trim();
        if (string.IsNullOrEmpty(expr))
            throw new InvalidOperationException("Expression must not be empty.");

        while (true)
        {
            Match? innermost = null;
            foreach (Match match in FunctionCallRegex.Matches(expr))
            {
                if (innermost is null || match.Groups[2].Length < innermost.Groups[2].Length)
                    innermost = match;
            }

            if (innermost is null)
                break;

            var fnName = innermost.Groups[1].Value;
            var args   = innermost.Groups[2].Value;
            var result = InvokeFunction(fnName, args, context);
            expr = expr[..innermost.Index]
                 + FormatIntermediate(result)
                 + expr[(innermost.Index + innermost.Length)..];
        }

        return EvaluateFinalExpression(expr.Trim(), context);
    }

    private static object InvokeFunction(string name, string argsRaw, ExcelTransformExpressionContext context)
    {
        var args = SplitFunctionArgs(argsRaw);
        return name.ToLowerInvariant() switch
        {
            "tonumber" => ToNumber(ResolveArg(args, 0, context)),
            "totext"   => ToText(ResolveArg(args, 0, context)),
            "trim"     => Trim(ResolveArg(args, 0, context)),
            "removeleadingzeros" => RemoveLeadingZeros(ResolveArg(args, 0, context)),
            "divide"   => ToNumber(ResolveArg(args, 0, context))
                          / ParseNumericArg(ResolveArg(args, 1, context), "divide"),
            "multiply" => ToNumber(ResolveArg(args, 0, context))
                          * ParseNumericArg(ResolveArg(args, 1, context), "multiply"),
            "add"      => ToNumber(ResolveArg(args, 0, context))
                          + ParseNumericArg(ResolveArg(args, 1, context), "add"),
            "subtract" => ToNumber(ResolveArg(args, 0, context))
                          - ParseNumericArg(ResolveArg(args, 1, context), "subtract"),
            _ => throw new InvalidOperationException($"Unsupported expression function '{name}'.")
        };
    }

    private static string ResolveArg(string[] args, int index, ExcelTransformExpressionContext context)
    {
        if (args.Length <= index)
            throw new InvalidOperationException($"Function expected argument {index + 1}.");

        return ResolveAtom(args[index].Trim(), context);
    }

    internal static string ResolveAtom(string atom, ExcelTransformExpressionContext context)
    {
        if (string.Equals(atom, "value", StringComparison.OrdinalIgnoreCase))
            return context.Value;

        var rowMatch = RowAccessRegex.Match(atom);
        if (!rowMatch.Success)
            rowMatch = RowAccessSingleQuoteRegex.Match(atom);
        if (rowMatch.Success)
            return ResolveRowValue(context.Row, rowMatch.Groups[1].Value);

        if (atom.StartsWith("variables.", StringComparison.OrdinalIgnoreCase))
            return ResolveVariable(context.Variables, atom["variables.".Length..]);

        return atom;
    }

    private static string ResolveRowValue(IReadOnlyDictionary<string, string> row, string key)
    {
        if (row.TryGetValue(key, out var value))
            return value;

        foreach (var (k, v) in row)
        {
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                return v;
        }

        throw new InvalidOperationException($"Row column '{key}' not found.");
    }

    private static string ResolveVariable(IReadOnlyDictionary<string, object> variables, string path)
    {
        path = path.Trim();
        if (variables.TryGetValue(path, out var direct))
            return VariableToString(direct);

        foreach (var (k, v) in variables)
        {
            if (string.Equals(k, path, StringComparison.OrdinalIgnoreCase))
                return VariableToString(v);
        }

        throw new InvalidOperationException($"Variable 'variables.{path}' not found.");
    }

    private static double ParseNumericArg(string arg, string fn)
    {
        if (TryParseDecimal(arg, out var n))
            return n;
        throw new InvalidOperationException($"'{fn}' second argument must be numeric, got '{arg}'.");
    }

    private static string[] SplitFunctionArgs(string args)
    {
        var parts   = new List<string>();
        var current = new StringBuilder();
        var depth   = 0;

        foreach (var ch in args)
        {
            if (ch == '(') depth++;
            else if (ch == ')') depth--;
            else if (ch == ',' && depth == 0)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        parts.Add(current.ToString().Trim());
        return parts.ToArray();
    }

    internal static double ToNumber(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return 0;

        if (TryParseDecimal(s, out var n))
            return n;

        var trimmed = s.Trim();
        if (trimmed.Length > 0 && trimmed.All(char.IsDigit))
            return double.Parse(trimmed, CultureInfo.InvariantCulture);

        throw new InvalidOperationException($"toNumber could not parse '{s}'.");
    }

    internal static bool TryParseDecimal(string s, out double result)
    {
        var trimmed = s.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            result = 0;
            return false;
        }

        var hasComma  = trimmed.Contains(',');
        var hasPeriod = trimmed.Contains('.');

        if (hasComma && !hasPeriod)
            return double.TryParse(trimmed, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out result);

        if (hasPeriod && !hasComma)
            return double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out result);

        if (hasComma && hasPeriod)
        {
            var culture = trimmed.LastIndexOf(',') > trimmed.LastIndexOf('.')
                ? CultureInfo.GetCultureInfo("tr-TR")
                : CultureInfo.InvariantCulture;
            return double.TryParse(trimmed, NumberStyles.Any, culture, out result);
        }

        if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            return true;

        if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out result))
            return true;

        result = 0;
        return false;
    }

    private static string ToText(string s) => s;

    private static string Trim(string s) => s.Trim();

    private static string RemoveLeadingZeros(string s)
    {
        var trimmed = s.Trim();
        if (trimmed.Length == 0) return trimmed;

        var negative = trimmed.StartsWith('-');
        if (negative) trimmed = trimmed[1..];

        var i = 0;
        while (i < trimmed.Length - 1 && trimmed[i] == '0') i++;
        var result = trimmed[i..];
        return negative ? "-" + result : result;
    }

    private static string FormatIntermediate(object result) => result switch
    {
        double d    => d.ToString(CultureInfo.InvariantCulture),
        float f     => f.ToString(CultureInfo.InvariantCulture),
        int i       => i.ToString(CultureInfo.InvariantCulture),
        long l      => l.ToString(CultureInfo.InvariantCulture),
        decimal m   => m.ToString(CultureInfo.InvariantCulture),
        _           => result.ToString() ?? string.Empty
    };

    private static object EvaluateFinalExpression(string expr, ExcelTransformExpressionContext context)
    {
        if (string.IsNullOrEmpty(expr))
            return context.Value;

        if (string.Equals(expr, "value", StringComparison.OrdinalIgnoreCase))
            return context.Value;

        if (expr.IndexOfAny(['+', '-', '*', '/']) >= 0)
        {
            try
            {
                var computed = new DataTable().Compute(expr, null);
                if (computed is null)
                    throw new InvalidOperationException($"Expression '{expr}' did not produce a value.");
                return computed switch
                {
                    double d  => d,
                    decimal m => (double)m,
                    int i     => (double)i,
                    long l    => (double)l,
                    float f   => (double)f,
                    _         => Convert.ToDouble(computed, CultureInfo.InvariantCulture)
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to evaluate expression '{expr}': {ex.Message}");
            }
        }

        if (double.TryParse(expr, NumberStyles.Any, CultureInfo.InvariantCulture, out var direct)
            && (expr.Contains('.') || expr.Contains('e', StringComparison.OrdinalIgnoreCase)))
            return direct;

        return ResolveAtom(expr, context);
    }

    private static string VariableToString(object? value) =>
        value switch
        {
            null           => string.Empty,
            string s       => s,
            bool b         => b ? "true" : "false",
            JsonElement je => je.ValueKind switch
            {
                JsonValueKind.String => je.GetString() ?? string.Empty,
                JsonValueKind.True   => "true",
                JsonValueKind.False  => "false",
                JsonValueKind.Null   => string.Empty,
                _                    => je.GetRawText(),
            },
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
}
