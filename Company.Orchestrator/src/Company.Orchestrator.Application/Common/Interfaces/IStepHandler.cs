using Company.Orchestrator.Application.Models;

namespace Company.Orchestrator.Application.Common.Interfaces;

/// <summary>
/// Contract for all step handlers. Implementations must not import concrete capabilities
/// or infrastructure types — use WorkflowContext.GetCapability&lt;T&gt;() instead.
/// </summary>
public interface IStepHandler
{
    /// <summary>
    /// The string that matches StepDefinition.Type in the workflow JSON.
    /// Must be unique across all registered handlers.
    /// </summary>
    string HandlerType { get; }

    Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default);
}
