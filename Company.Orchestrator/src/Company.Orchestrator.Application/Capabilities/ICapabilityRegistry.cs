namespace Company.Orchestrator.Application.Capabilities;

/// <summary>
/// Central registry that maps capability interfaces to implementations.
/// Registered once at startup; consulted at runtime by WorkflowContext.GetCapability&lt;T&gt;().
/// Adding a new capability requires only a new registration — no engine changes.
/// </summary>
public interface ICapabilityRegistry
{
    /// <summary>Returns the registered implementation for capability T.</summary>
    /// <exception cref="InvalidOperationException">Thrown when no implementation is registered for T.</exception>
    T Resolve<T>() where T : class, ICapability;

    /// <summary>Returns true if an implementation is registered for capability T.</summary>
    bool IsRegistered<T>() where T : class, ICapability;

    /// <summary>Registers a capability implementation. Called from DI setup.</summary>
    void Register<T>(T implementation) where T : class, ICapability;

    /// <summary>Returns the names of all registered capabilities (for diagnostics).</summary>
    IReadOnlyCollection<string> RegisteredCapabilities { get; }
}
