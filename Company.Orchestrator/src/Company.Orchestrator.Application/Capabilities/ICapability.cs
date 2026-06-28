namespace Company.Orchestrator.Application.Capabilities;

/// <summary>
/// Marker interface for all capabilities.
/// A capability is a named group of related operations (File I/O, Mail, Browser, Excel, …).
/// The WorkflowEngine never imports a concrete capability — it only knows this interface.
/// Step handlers request a specific capability via WorkflowContext.GetCapability&lt;T&gt;().
/// </summary>
public interface ICapability
{
    /// <summary>Unique name used for logging and diagnostics (e.g. "File", "Mail", "Browser").</summary>
    string CapabilityName { get; }
}
