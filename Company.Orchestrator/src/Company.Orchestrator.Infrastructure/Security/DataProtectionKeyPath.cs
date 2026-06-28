using Microsoft.Extensions.Configuration;

namespace Company.Orchestrator.Infrastructure.Security;

public static class DataProtectionKeyPath
{
    public const string ApplicationName = "Company.Orchestrator";

    /// <summary>
    /// Resolves the shared Data Protection key ring directory for API and Worker.
    /// Must be identical across all hosts that encrypt/decrypt credential secrets.
    /// </summary>
    public static string Resolve(IConfiguration configuration)
    {
        var configured = configuration["DataProtection:KeysPath"]?.Trim();
        if (!string.IsNullOrEmpty(configured))
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AlterOne",
            "dp-keys");
    }
}
