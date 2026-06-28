namespace Company.Orchestrator.Domain.Constants;

public static class Permissions
{
    public const string WorkflowView    = "workflow.view";
    public const string WorkflowEdit    = "workflow.edit";
    public const string WorkflowExecute = "workflow.execute";

    public const string JobView    = "job.view";
    public const string JobCancel  = "job.cancel";
    public const string JobRetry   = "job.retry";

    public const string CredentialView   = "credential.view";
    public const string CredentialManage = "credential.manage";
    public const string CredentialUse    = "credential.use";

    public const string WorkerView = "worker.view";

    public const string DashboardView = "dashboard.view";

    public const string AuditView  = "audit.view";
    public const string AdminManage = "admin.manage";

    public static readonly IReadOnlyList<string> All =
    [
        WorkflowView, WorkflowEdit, WorkflowExecute,
        JobView, JobCancel, JobRetry,
        CredentialView, CredentialManage, CredentialUse,
        WorkerView, DashboardView, AuditView, AdminManage,
    ];

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [WorkflowView]    = "View workflows and definitions",
        [WorkflowEdit]    = "Create and edit workflows",
        [WorkflowExecute] = "Start workflow instances",
        [JobView]         = "View jobs and logs",
        [JobCancel]       = "Cancel running or pending jobs",
        [JobRetry]        = "Retry failed jobs",
        [CredentialView]  = "View credential vault entries",
        [CredentialManage]= "Create, edit, and delete credentials",
        [CredentialUse]   = "Resolve credentials during operations",
        [WorkerView]      = "View worker health and status",
        [DashboardView]   = "View dashboard KPIs and operational metrics",
        [AuditView]       = "View audit logs",
        [AdminManage]     = "Manage users, roles, and permissions",
    };
}

public static class DefaultRoles
{
    public const string Admin     = "Admin";
    public const string Developer = "Developer";
    public const string Operator  = "Operator";
    public const string Viewer    = "Viewer";

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultPermissions =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Admin] = Permissions.All,
            [Developer] =
            [
                Permissions.WorkflowView, Permissions.WorkflowEdit, Permissions.WorkflowExecute,
                Permissions.JobView, Permissions.CredentialUse, Permissions.WorkerView,
                Permissions.DashboardView,
            ],
            [Operator] =
            [
                Permissions.WorkflowView, Permissions.WorkflowExecute,
                Permissions.JobView, Permissions.JobCancel, Permissions.JobRetry,
                Permissions.DashboardView,
            ],
            [Viewer] =
            [
                Permissions.WorkflowView, Permissions.JobView, Permissions.WorkerView,
                Permissions.DashboardView,
            ],
        };
}
