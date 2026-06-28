import api from './client';
import type {
  PagedResult,
  ProcessDefinitionDto,
  ProcessVersionDto,
  ProcessInstanceDto,
  JobDto,
  JobLogDto,
  ArtifactDto,
  TriggerDto,
  TriggerEventDto,
  CreateProcessDefinitionRequest,
  UpdateProcessDefinitionRequest,
  CreateProcessVersionRequest,
  StartProcessRequest,
  CreateTriggerRequest,
  UpdateTriggerRequest,
  BrowserPickerSelectedResponse,
  CredentialDto,
  CreateCredentialRequest,
  UpdateCredentialRequest,
  CredentialTestResponse,
  WorkerListItemDto,
  WorkerDetailDto,
  WorkerSummaryDto,
  CancelJobRequest,
  CancelJobResponse,
  LoginRequest,
  LoginResponse,
  UserProfileDto,
  UserDto,
  CreateUserRequest,
  UpdateUserRequest,
  AssignUserRolesRequest,
  RoleDto,
  PermissionDto,
  UpdateRolePermissionsRequest,
  DashboardKpiDto,
  AuditQueryFilter,
  AuditLogListItemDto,
  AuditLogDetailDto,
  AuditSummaryDto,
} from './types';

// ── Audit ─────────────────────────────────────────────────────────────────────

export const auditApi = {
  list: (filter: AuditQueryFilter) =>
    api.get<PagedResult<AuditLogListItemDto>>('/api/audit', { params: filter }),

  getById: (id: string) =>
    api.get<AuditLogDetailDto>(`/api/audit/${id}`),

  summary: (filter: AuditQueryFilter) =>
    api.get<AuditSummaryDto>('/api/audit/summary', { params: filter }),
};

// ── Dashboard ─────────────────────────────────────────────────────────────────

export const dashboardApi = {
  kpi: (fromUtc?: string, toUtc?: string) =>
    api.get<DashboardKpiDto>('/api/dashboard/kpi', {
      params: {
        ...(fromUtc ? { fromUtc } : {}),
        ...(toUtc ? { toUtc } : {}),
      },
    }),
};

// ── Auth ──────────────────────────────────────────────────────────────────────

export const authApi = {
  login: (body: LoginRequest) =>
    api.post<LoginResponse>('/api/auth/login', body),

  me: () =>
    api.get<UserProfileDto>('/api/auth/me'),
};

// ── Users ─────────────────────────────────────────────────────────────────────

export const userApi = {
  list: () => api.get<UserDto[]>('/api/users'),
  getById: (id: string) => api.get<UserDto>(`/api/users/${id}`),
  create: (body: CreateUserRequest) => api.post<UserDto>('/api/users', body),
  update: (id: string, body: UpdateUserRequest) => api.put<UserDto>(`/api/users/${id}`, body),
  delete: (id: string) => api.delete(`/api/users/${id}`),
  assignRoles: (id: string, body: AssignUserRolesRequest) =>
    api.post<UserDto>(`/api/users/${id}/roles`, body),
};

// ── Roles ─────────────────────────────────────────────────────────────────────

export const roleApi = {
  list: () => api.get<RoleDto[]>('/api/roles'),
  listPermissions: () => api.get<PermissionDto[]>('/api/permissions'),
  updatePermissions: (id: string, body: UpdateRolePermissionsRequest) =>
    api.put<RoleDto>(`/api/roles/${id}/permissions`, body),
};

// ── Process Definitions ───────────────────────────────────────────────────────

export const processDefinitionApi = {
  list: (page = 1, pageSize = 20) =>
    api.get<PagedResult<ProcessDefinitionDto>>('/api/process-definitions', {
      params: { page, pageSize },
    }),

  getById: (id: string) =>
    api.get<ProcessDefinitionDto>(`/api/process-definitions/${id}`),

  create: (body: CreateProcessDefinitionRequest) =>
    api.post<ProcessDefinitionDto>('/api/process-definitions', body),

  update: (id: string, body: UpdateProcessDefinitionRequest) =>
    api.put<ProcessDefinitionDto>(`/api/process-definitions/${id}`, body),

  delete: (id: string) =>
    api.delete(`/api/process-definitions/${id}`),

  getVersions: (definitionId: string) =>
    api.get<ProcessVersionDto[]>(`/api/process-definitions/${definitionId}/versions`),

  createVersion: (definitionId: string, body: CreateProcessVersionRequest) =>
    api.post<ProcessVersionDto>(`/api/process-definitions/${definitionId}/versions`, body),

  publishVersion: (definitionId: string, versionId: string) =>
    api.post<ProcessVersionDto>(
      `/api/process-definitions/${definitionId}/versions/${versionId}/publish`,
    ),
};

// ── Process Instances ─────────────────────────────────────────────────────────

export const processInstanceApi = {
  list: (page = 1, pageSize = 20, definitionId?: string) =>
    api.get<PagedResult<ProcessInstanceDto>>('/api/process-instances', {
      params: { page, pageSize, ...(definitionId ? { definitionId } : {}) },
    }),

  getById: (id: string) =>
    api.get<ProcessInstanceDto>(`/api/process-instances/${id}`),

  start: (body: StartProcessRequest) =>
    api.post<ProcessInstanceDto>('/api/process-instances/start', body),

  cancel: (id: string) =>
    api.post(`/api/process-instances/${id}/cancel`),

  getLogs: (id: string) =>
    api.get<JobLogDto[]>(`/api/process-instances/${id}/logs`),
};

// ── Jobs ─────────────────────────────────────────────────────────────────────

export const jobApi = {
  list: (page = 1, pageSize = 20, instanceId?: string) =>
    api.get<PagedResult<JobDto>>('/api/jobs', {
      params: { page, pageSize, ...(instanceId ? { instanceId } : {}) },
    }),

  getById: (id: string) =>
    api.get<JobDto>(`/api/jobs/${id}`),

  retry: (id: string) =>
    api.post(`/api/jobs/${id}/retry`),

  cancel: (id: string, body?: CancelJobRequest) =>
    api.post<CancelJobResponse>(`/api/jobs/${id}/cancel`, body ?? {}),
};

// ── Artifacts ─────────────────────────────────────────────────────────────────

export const artifactApi = {
  list: (page = 1, pageSize = 20) =>
    api.get<PagedResult<ArtifactDto>>('/api/artifacts', { params: { page, pageSize } }),

  getById: (id: string) =>
    api.get<ArtifactDto>(`/api/artifacts/${id}`),

  getByProcessInstance: (processInstanceId: string) =>
    api.get<ArtifactDto[]>(`/api/artifacts/process-instance/${processInstanceId}`),
};

// ── Triggers ──────────────────────────────────────────────────────────────────

export const triggerApi = {
  list: (page = 1, pageSize = 20) =>
    api.get<PagedResult<TriggerDto>>('/api/triggers', { params: { page, pageSize } }),

  getById: (id: string) =>
    api.get<TriggerDto>(`/api/triggers/${id}`),

  create: (body: CreateTriggerRequest) =>
    api.post<TriggerDto>('/api/triggers', body),

  update: (id: string, body: UpdateTriggerRequest) =>
    api.put<TriggerDto>(`/api/triggers/${id}`, body),

  activate: (id: string) =>
    api.post(`/api/triggers/${id}/activate`),

  deactivate: (id: string) =>
    api.post(`/api/triggers/${id}/deactivate`),

  getEvents: (id: string, page = 1, pageSize = 50) =>
    api.get<PagedResult<TriggerEventDto>>(`/api/triggers/${id}/events`, {
      params: { page, pageSize },
    }),
};

// ── Health / Diagnostics ──────────────────────────────────────────────────────

export const diagnosticsApi = {
  health: () => api.get('/api/health'),
  imapTcp: () => api.get('/api/diagnostics/imap-tcp'),
};

// ── Browser Picker (dev only) ─────────────────────────────────────────────────

export const browserPickerApi = {
  start: (url: string) =>
    api.post<{ sessionId: string; status: string }>('/api/browser-picker/start', { url }),

  stop: (sessionId: string) =>
    api.post<{ status: string }>('/api/browser-picker/stop', { sessionId }),

  getSelected: (sessionId: string) =>
    api.get<BrowserPickerSelectedResponse>(`/api/browser-picker/${sessionId}/selected`),
};

// ── Credentials (Credential Vault) ──────────────────────────────────────────

export const credentialApi = {
  list: (page = 1, pageSize = 100) =>
    api.get<PagedResult<CredentialDto>>('/api/credentials', { params: { page, pageSize } }),

  getById: (id: string) =>
    api.get<CredentialDto>(`/api/credentials/${id}`),

  create: (body: CreateCredentialRequest) =>
    api.post<CredentialDto>('/api/credentials', body),

  update: (id: string, body: UpdateCredentialRequest) =>
    api.put<CredentialDto>(`/api/credentials/${id}`, body),

  delete: (id: string) =>
    api.delete(`/api/credentials/${id}`),

  test: (id: string) =>
    api.post<CredentialTestResponse>(`/api/credentials/${id}/test`),
};

// ── Workers ───────────────────────────────────────────────────────────────────

export const workerApi = {
  list: () =>
    api.get<WorkerListItemDto[]>('/api/workers'),

  getById: (workerId: string) =>
    api.get<WorkerDetailDto>(`/api/workers/${encodeURIComponent(workerId)}`),

  summary: () =>
    api.get<WorkerSummaryDto>('/api/workers/summary'),
};
