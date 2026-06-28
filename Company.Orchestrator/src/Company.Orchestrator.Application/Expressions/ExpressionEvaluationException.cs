namespace Company.Orchestrator.Application.Expressions;

public sealed class ExpressionEvaluationException : Exception
{
    public ExpressionEvaluationException(string message) : base(message) { }

    public ExpressionEvaluationException(string message, Exception innerException)
        : base(message, innerException) { }
}
