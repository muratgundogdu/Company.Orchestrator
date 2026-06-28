namespace Company.Orchestrator.Domain.Constants;

public static class AuditCategories
{
    public const string Authentication  = "Authentication";
    public const string Authorization   = "Authorization";
    public const string Workflow          = "Workflow";
    public const string Job             = "Job";
    public const string Credential      = "Credential";
    public const string UserManagement  = "UserManagement";
    public const string RoleManagement  = "RoleManagement";
    public const string System          = "System";
    public const string Worker          = "Worker";
}

public static class AuditSeverity
{
    public const string Info     = "Info";
    public const string Warning  = "Warning";
    public const string Error    = "Error";
    public const string Critical = "Critical";
}

public static class AuditEventTypes
{
    public const string UserLogin         = "UserLogin";
    public const string UserLogout        = "UserLogout";
    public const string LoginFailed       = "LoginFailed";
    public const string AccessDenied      = "AccessDenied";

    public const string WorkflowCreated   = "WorkflowCreated";
    public const string WorkflowUpdated   = "WorkflowUpdated";
    public const string WorkflowDeleted   = "WorkflowDeleted";
    public const string WorkflowExecuted  = "WorkflowExecuted";

    public const string JobStarted        = "JobStarted";
    public const string JobCompleted      = "JobCompleted";
    public const string JobFailed         = "JobFailed";
    public const string JobCancelled      = "JobCancelled";
    public const string JobRetried        = "JobRetried";

    public const string CredentialCreated = "CredentialCreated";
    public const string CredentialUpdated = "CredentialUpdated";
    public const string CredentialDeleted = "CredentialDeleted";
    public const string CredentialUsed    = "CredentialUsed";

    public const string UserCreated       = "UserCreated";
    public const string UserUpdated       = "UserUpdated";
    public const string UserDeleted       = "UserDeleted";

    public const string RoleCreated       = "RoleCreated";
    public const string RoleUpdated       = "RoleUpdated";
    public const string PermissionChanged = "PermissionChanged";

    public const string WorkerRegistered  = "WorkerRegistered";
    public const string WorkerOffline     = "WorkerOffline";
    public const string WorkerOnline      = "WorkerOnline";

    public const string ConfigurationChanged = "ConfigurationChanged";
}
