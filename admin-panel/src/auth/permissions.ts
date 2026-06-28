export const Permissions = {
  WorkflowView: 'workflow.view',
  WorkflowEdit: 'workflow.edit',
  WorkflowExecute: 'workflow.execute',
  JobView: 'job.view',
  JobCancel: 'job.cancel',
  JobRetry: 'job.retry',
  CredentialView: 'credential.view',
  CredentialManage: 'credential.manage',
  CredentialUse: 'credential.use',
  WorkerView: 'worker.view',
  DashboardView: 'dashboard.view',
  AuditView: 'audit.view',
  AdminManage: 'admin.manage',
} as const;

export type Permission = (typeof Permissions)[keyof typeof Permissions];
