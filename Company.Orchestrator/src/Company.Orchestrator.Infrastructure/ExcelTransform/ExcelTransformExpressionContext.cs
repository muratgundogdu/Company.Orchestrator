namespace Company.Orchestrator.Infrastructure.ExcelTransform;

/// <summary>
/// Per-row evaluation context for excel.transform transformColumn expressions.
/// </summary>
public sealed class ExcelTransformExpressionContext
{
    public string Value { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Row { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, object> Variables { get; init; }
        = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
}
