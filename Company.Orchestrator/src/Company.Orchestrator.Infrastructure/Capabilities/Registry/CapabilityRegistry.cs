using Company.Orchestrator.Application.Capabilities;

namespace Company.Orchestrator.Infrastructure.Capabilities.Registry;

/// <summary>
/// Thread-safe, type-keyed registry for ICapability implementations.
/// Populated at startup via DependencyInjection.AddInfrastructure().
/// The WorkflowEngine and step handlers never depend on concrete capability types.
/// </summary>
public sealed class CapabilityRegistry : ICapabilityRegistry
{
    private readonly Dictionary<Type, ICapability> _registry = new();
    private readonly object _lock = new();

    public void Register<T>(T implementation) where T : class, ICapability
    {
        ArgumentNullException.ThrowIfNull(implementation);
        lock (_lock)
        {
            _registry[typeof(T)] = implementation;
        }
    }

    public T Resolve<T>() where T : class, ICapability
    {
        lock (_lock)
        {
            if (_registry.TryGetValue(typeof(T), out var capability))
                return (T)capability;
        }

        throw new InvalidOperationException(
            $"No implementation registered for capability '{typeof(T).Name}'. " +
            $"Register it in DependencyInjection.AddInfrastructure(). " +
            $"Currently registered: [{string.Join(", ", RegisteredCapabilities)}]");
    }

    public bool IsRegistered<T>() where T : class, ICapability
    {
        lock (_lock)
        {
            return _registry.ContainsKey(typeof(T));
        }
    }

    public IReadOnlyCollection<string> RegisteredCapabilities
    {
        get
        {
            lock (_lock)
            {
                return _registry.Values.Select(c => c.CapabilityName).ToList();
            }
        }
    }
}
