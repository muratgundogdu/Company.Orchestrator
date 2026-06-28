using Company.Orchestrator.Application.Models;

namespace Company.Orchestrator.Application.Common.Interfaces;

/// <summary>
/// Evaluates safe workflow expressions with variables, math, comparisons, and built-in functions.
/// </summary>
public interface IExpressionEvaluator
{
    Task<object> EvaluateAsync(
        string expression,
        WorkflowContext context,
        CancellationToken cancellationToken = default);
}
