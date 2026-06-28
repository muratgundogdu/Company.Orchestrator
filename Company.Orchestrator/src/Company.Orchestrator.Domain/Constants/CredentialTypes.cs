namespace Company.Orchestrator.Domain.Constants;

public static class CredentialTypes
{
    public const string SqlConnectionString = "SqlConnectionString";
    public const string ApiKey              = "ApiKey";
    public const string BearerToken         = "BearerToken";
    public const string BasicAuth           = "BasicAuth";
    public const string UsernamePassword    = "UsernamePassword";
    public const string GenericSecret       = "GenericSecret";

    public static readonly string[] All =
    [
        SqlConnectionString,
        ApiKey,
        BearerToken,
        BasicAuth,
        UsernamePassword,
        GenericSecret,
    ];
}
