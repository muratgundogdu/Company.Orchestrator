using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Expressions;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using NCalc;
using NCalc.Handlers;

namespace Company.Orchestrator.Infrastructure.Expressions;

/// <summary>
/// Safe expression evaluator backed by NCalc with workflow variable substitution and whitelisted functions.
/// </summary>
public sealed class ExpressionEvaluator : IExpressionEvaluator
{
    private static readonly Regex VariableTokenRegex = new(
        @"\{\{([^}]+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DoubleQuotedStringRegex = new(
        @"""((?:[^""\\]|\\.)*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SingleQuotedStringRegex = new(
        @"(?<![a-zA-Z0-9_])'(?:''|[^'])*'",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Postfix factorial (!), not unary NOT or !=.</summary>
    private static readonly Regex FactorialOperatorRegex = new(
        @"(?<=[0-9\)a-zA-Z_])\!(?!=)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "trim", "toupper", "tolower", "contains", "startswith", "endswith", "substring", "replace",
        "tonumber", "round", "abs", "min", "max",
        "now", "today", "formatdate", "adddays", "addmonths",
        "coalesce", "isempty", "isnotempty", "length", "guid",
    };

    private readonly ILogger<ExpressionEvaluator> _logger;

    public ExpressionEvaluator(ILogger<ExpressionEvaluator> logger)
        => _logger = logger;

    public Task<object> EvaluateAsync(
        string expression,
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(expression))
            throw new ExpressionEvaluationException("Expression cannot be empty.");

        RejectFactorialOperator(expression);

        string prepared;
        try
        {
            prepared = PrepareExpression(expression, context);
        }
        catch (ExpressionEvaluationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ExpressionEvaluationException($"Invalid expression: {ex.Message}", ex);
        }

        _logger.LogDebug("Expression prepared for evaluation ({Length} chars)", prepared.Length);

        RejectFactorialOperator(prepared);

        try
        {
            var ncalc = new Expression(prepared, ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
            ncalc.EvaluateFunction += HandleFunction;

            var result = ncalc.Evaluate(cancellationToken);
            return Task.FromResult(NormalizeResult(result));
        }
        catch (ExpressionEvaluationException)
        {
            throw;
        }
        catch (DivideByZeroException)
        {
            throw new ExpressionEvaluationException("Divide by zero.");
        }
        catch (Exception ex)
        {
            throw new ExpressionEvaluationException(MapEvaluationError(ex), ex);
        }
    }

    private static void RejectFactorialOperator(string expression)
    {
        var withoutStrings = SingleQuotedStringRegex.Replace(
            DoubleQuotedStringRegex.Replace(expression, "\"\""),
            "''");

        if (FactorialOperatorRegex.IsMatch(withoutStrings))
            throw new ExpressionEvaluationException("Factorial operator is not allowed.");
    }

    private static string PrepareExpression(string expression, WorkflowContext context)
    {
        var withVariables = VariableTokenRegex.Replace(expression, match =>
        {
            var path = match.Groups[1].Value.Trim();
            if (!context.TryResolveVariablePath(path, out var value))
                throw new ExpressionEvaluationException($"Missing variable '{path}'.");

            return QuoteForNCalc(value);
        });

        return ConvertDoubleQuotedStrings(withVariables);
    }

    private static string ConvertDoubleQuotedStrings(string expression) =>
        DoubleQuotedStringRegex.Replace(expression, match =>
        {
            var inner = match.Groups[1].Value
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\r", "\r", StringComparison.Ordinal)
                .Replace("\\t", "\t", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
            return QuoteForNCalc(inner);
        });

    private static string QuoteForNCalc(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static void HandleFunction(string name, FunctionEventArgs args)
    {
        if (!AllowedFunctions.Contains(name))
            throw new ExpressionEvaluationException($"Unknown function '{name}'.");

        switch (name.ToLowerInvariant())
        {
            case "trim":
                args.Result = AsString(EvaluateArg(args, 0)).Trim();
                break;
            case "toupper":
                args.Result = AsString(EvaluateArg(args, 0)).ToUpperInvariant();
                break;
            case "tolower":
                args.Result = AsString(EvaluateArg(args, 0)).ToLowerInvariant();
                break;
            case "contains":
                args.Result = AsString(EvaluateArg(args, 0))
                    .Contains(AsString(EvaluateArg(args, 1)), StringComparison.OrdinalIgnoreCase);
                break;
            case "startswith":
                args.Result = AsString(EvaluateArg(args, 0))
                    .StartsWith(AsString(EvaluateArg(args, 1)), StringComparison.OrdinalIgnoreCase);
                break;
            case "endswith":
                args.Result = AsString(EvaluateArg(args, 0))
                    .EndsWith(AsString(EvaluateArg(args, 1)), StringComparison.OrdinalIgnoreCase);
                break;
            case "substring":
                args.Result = SubstringValue(args);
                break;
            case "replace":
                args.Result = AsString(EvaluateArg(args, 0))
                    .Replace(AsString(EvaluateArg(args, 1)), AsString(EvaluateArg(args, 2)), StringComparison.Ordinal);
                break;
            case "tonumber":
                args.Result = ToNumber(EvaluateArg(args, 0), "toNumber");
                break;
            case "round":
                args.Result = Math.Round(
                    ToNumber(EvaluateArg(args, 0), "round"),
                    (int)ToNumber(EvaluateArg(args, 1), "round"));
                break;
            case "abs":
                args.Result = Math.Abs(ToNumber(EvaluateArg(args, 0), "abs"));
                break;
            case "min":
                args.Result = Math.Min(
                    ToNumber(EvaluateArg(args, 0), "min"),
                    ToNumber(EvaluateArg(args, 1), "min"));
                break;
            case "max":
                args.Result = Math.Max(
                    ToNumber(EvaluateArg(args, 0), "max"),
                    ToNumber(EvaluateArg(args, 1), "max"));
                break;
            case "now":
                args.Result = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                break;
            case "today":
                args.Result = args.Parameters.Count == 0
                    ? DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : DateTime.Today.ToString(
                        AsString(EvaluateArg(args, 0)),
                        CultureInfo.InvariantCulture);
                break;
            case "formatdate":
                args.Result = ParseDate(EvaluateArg(args, 0), "formatDate")
                    .ToString(AsString(EvaluateArg(args, 1)), CultureInfo.InvariantCulture);
                break;
            case "adddays":
                args.Result = ParseDate(EvaluateArg(args, 0), "addDays")
                    .AddDays(ToNumber(EvaluateArg(args, 1), "addDays"))
                    .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                break;
            case "addmonths":
                args.Result = ParseDate(EvaluateArg(args, 0), "addMonths")
                    .AddMonths((int)ToNumber(EvaluateArg(args, 1), "addMonths"))
                    .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                break;
            case "coalesce":
                args.Result = Coalesce(args);
                break;
            case "isempty":
                args.Result = string.IsNullOrEmpty(AsString(EvaluateArg(args, 0)));
                break;
            case "isnotempty":
                args.Result = !string.IsNullOrEmpty(AsString(EvaluateArg(args, 0)));
                break;
            case "length":
                args.Result = AsString(EvaluateArg(args, 0)).Length;
                break;
            case "guid":
                args.Result = Guid.NewGuid().ToString("D");
                break;
        }
    }

    private static object Coalesce(FunctionEventArgs args)
    {
        var first = AsString(EvaluateArg(args, 0));
        return string.IsNullOrEmpty(first)
            ? AsString(EvaluateArg(args, 1))
            : first;
    }

    private static string SubstringValue(FunctionEventArgs args)
    {
        var text  = AsString(EvaluateArg(args, 0));
        var start = (int)ToNumber(EvaluateArg(args, 1), "substring");
        if (args.Parameters.Count >= 3)
        {
            var length = (int)ToNumber(EvaluateArg(args, 2), "substring");
            if (start < 0 || length < 0 || start > text.Length)
                return string.Empty;
            var maxLen = Math.Min(length, text.Length - start);
            return text.Substring(start, maxLen);
        }

        if (start < 0 || start > text.Length)
            return string.Empty;
        return text[start..];
    }

    private static object EvaluateArg(FunctionEventArgs args, int index)
    {
        if (index >= args.Parameters.Count)
            throw new ExpressionEvaluationException("Function parameter count mismatch.");

        return args.Parameters.Evaluate(index) ?? string.Empty;
    }

    private static double ToNumber(object value, string operation)
    {
        switch (value)
        {
            case byte b:     return b;
            case sbyte sb:   return sb;
            case short s:    return s;
            case ushort us:  return us;
            case int i:      return i;
            case uint ui:    return ui;
            case long l:     return l;
            case ulong ul:   return ul;
            case float f:    return f;
            case double d:   return d;
            case decimal m:  return (double)m;
            case bool boolean: return boolean ? 1 : 0;
        }

        var text = AsString(value).Trim();
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            return integer;

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return number;

        throw new ExpressionEvaluationException($"Invalid number conversion in {operation}.");
    }

    private static DateTime ParseDate(object value, string operation)
    {
        if (value is DateTime dt)
            return dt;

        var text = AsString(value).Trim();
        if (string.IsNullOrEmpty(text))
            throw new ExpressionEvaluationException($"Invalid date conversion in {operation}.");

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;

        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed))
            return parsed;

        throw new ExpressionEvaluationException($"Invalid date conversion in {operation}.");
    }

    private static string AsString(object? value) =>
        value switch
        {
            null           => string.Empty,
            string s       => s,
            bool b         => b ? "true" : "false",
            DateTime dt    => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
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

    private static object NormalizeResult(object? value) =>
        value switch
        {
            null => string.Empty,
            _    => value,
        };

    private static string MapEvaluationError(Exception ex) =>
        ex.Message.Contains("parse", StringComparison.OrdinalIgnoreCase)
            ? "Invalid expression syntax."
            : $"Expression evaluation failed: {ex.Message}";
}
