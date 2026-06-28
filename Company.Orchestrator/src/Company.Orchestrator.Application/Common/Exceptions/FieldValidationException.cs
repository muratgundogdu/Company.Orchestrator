namespace Company.Orchestrator.Application.Common.Exceptions;

/// <summary>
/// Validation failure tied to a specific request field (returned as 400 Bad Request).
/// </summary>
public sealed class FieldValidationException : Exception
{
    public string Field { get; }

    public FieldValidationException(string field, string message) : base(message)
    {
        Field = field;
    }
}
