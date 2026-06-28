using System.Text.Json;
using System.Text.RegularExpressions;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Capabilities;
using Company.Orchestrator.Application.Common;
using Company.Orchestrator.Domain.Entities;

namespace Company.Orchestrator.Application.Models;

/// <summary>
/// The single shared boundary between the WorkflowEngine and every step handler.
///
/// Design goals:
///   - Handlers access capabilities via GetCapability&lt;T&gt;(), never via direct imports.
///   - Variables, Artifacts, and StepOutputs flow through the context between steps.
///   - WorkflowEngine is the only class that instantiates and mutates this object.
///   - Step handlers treat context as the full execution environment.
/// </summary>
public sealed class WorkflowContext
{
    private readonly ICapabilityRegistry _registry;
    private readonly Dictionary<string, StepOutput> _stepOutputs = new();

    // ---- Process-level metadata (immutable after construction) ----

    public Guid ProcessInstanceId { get; }
    public Guid JobId { get; }
    public string CorrelationId { get; }

    // ---- Step-level (set by engine before calling ExecuteAsync) ----

    /// <summary>
    /// The definition of the currently executing step.
    /// Set by the engine before each handler call; handlers use this to read their Config.
    /// </summary>
    public StepDefinition StepDefinition { get; internal set; } = null!;

    /// <summary>Reference to the process instance entity (read-only for handlers).</summary>
    public ProcessInstance ProcessInstance { get; }

    // ---- Mutable workflow state ----

    /// <summary>
    /// Shared variable bag. Handlers read inputs from and write outputs to this dictionary.
    /// Supports {{variableName}} interpolation in step config.
    /// </summary>
    public Dictionary<string, object> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Named artifacts available in this execution.
    /// Key = logical artifact name (e.g. "invoice-pdf", "report-xlsx").
    /// Handlers add produced artifacts here; downstream steps consume them.
    /// </summary>
    public Dictionary<string, ArtifactReference> Artifacts { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Immutable view of outputs from all steps that completed before the current step.
    /// Key = StepDefinition.Id.
    /// </summary>
    public IReadOnlyDictionary<string, StepOutput> StepOutputs => _stepOutputs;

    // ---- Constructor (called only by WorkflowEngine) ----

    public WorkflowContext(
        ProcessInstance processInstance,
        Guid jobId,
        ICapabilityRegistry registry,
        Dictionary<string, object>? initialVariables = null)
    {
        ProcessInstance = processInstance;
        ProcessInstanceId = processInstance.Id;
        JobId = jobId;
        CorrelationId = processInstance.CorrelationId ?? string.Empty;
        _registry = registry;

        if (initialVariables is not null)
            foreach (var (k, v) in initialVariables)
                Variables[k] = v;
    }

    // ---- Capability resolution ----

    /// <summary>
    /// Returns the registered implementation for capability T.
    /// This is the ONLY way step handlers should access external systems.
    /// </summary>
    /// <example>
    ///   var mail = context.GetCapability&lt;IMailCapability&gt;();
    ///   await mail.SendAsync(message, cancellationToken);
    /// </example>
    public T GetCapability<T>() where T : class, ICapability
        => _registry.Resolve<T>();

    /// <summary>Returns true if the capability T is available in this execution environment.</summary>
    public bool HasCapability<T>() where T : class, ICapability
        => _registry.IsRegistered<T>();

    // ---- Variable helpers ----

    public bool TryGetVariable<T>(string key, out T? value)
    {
        if (Variables.TryGetValue(key, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    public string Interpolate(string template)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        return Regex.Replace(
            template,
            @"\{\{([^}]+)\}\}",
            match => ResolveVariablePath(match.Groups[1].Value.Trim()),
            RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Resolves a dotted variable path. Returns false when the root variable or nested property is missing.
    /// </summary>
    public bool TryResolveVariablePath(string path, out string value)
    {
        value = string.Empty;
        path = path.Trim();
        if (string.IsNullOrEmpty(path))
            return true;

        if (Variables.TryGetValue(path, out var direct))
        {
            value = VariableToString(direct);
            return true;
        }

        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return true;

        if (!Variables.TryGetValue(parts[0], out var current))
            return false;

        if (parts.Length == 1)
        {
            value = VariableToString(current);
            return true;
        }

        for (var i = 1; i < parts.Length; i++)
        {
            current = ResolveProperty(current, parts[i]);
            if (current is null)
                return false;
        }

        value = VariableToString(current);
        return true;
    }

    private string ResolveVariablePath(string path)
    {
        return TryResolveVariablePath(path, out var value) ? value : string.Empty;
    }

    private static object? ResolveProperty(object? value, string propertyName)
    {
        if (value is null)
            return null;

        if (value is JsonElement je)
            return ResolveJsonProperty(je, propertyName);

        if (value is Dictionary<string, object> dict)
        {
            foreach (var (key, val) in dict)
            {
                if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
                    return val;
            }

            return null;
        }

        if (value is string text)
        {
            var trimmed = text.Trim();
            if (trimmed.StartsWith('{'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    return ResolveJsonProperty(doc.RootElement, propertyName);
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }

    private static object? ResolveJsonProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (element.TryGetProperty(propertyName, out var direct))
            return direct;

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                return prop.Value;
        }

        return null;
    }

    private static string VariableToString(object? val)
    {
        if (val is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String  => je.GetString() ?? "",
                JsonValueKind.Number  => je.GetRawText(),
                JsonValueKind.True    => "true",
                JsonValueKind.False   => "false",
                JsonValueKind.Null    => "",
                _                     => je.GetRawText()
            };
        }

        return val?.ToString() ?? "";
    }

    // ---- Artifact helpers ----

    public bool HasArtifact(string name) => Artifacts.ContainsKey(name);

    public ArtifactReference GetArtifact(string name)
        => Artifacts.TryGetValue(name, out var a) ? a
           : throw new KeyNotFoundException($"Artifact '{name}' not found in WorkflowContext.");

    public ArtifactReference? TryGetArtifact(string name)
        => Artifacts.GetValueOrDefault(name);

    // ---- Engine-internal mutation (not accessible to handlers) ----

    internal void RegisterStepOutput(StepOutput output)
        => _stepOutputs[output.StepId] = output;

    internal void MergeVariables(Dictionary<string, object>? variables)
    {
        if (variables is null) return;
        foreach (var (k, v) in variables)
            Variables[k] = v;
    }

    internal void RegisterArtifacts(IEnumerable<ArtifactReference>? artifacts)
    {
        if (artifacts is null) return;
        foreach (var a in artifacts)
            Artifacts[a.Name] = a;
    }
}
