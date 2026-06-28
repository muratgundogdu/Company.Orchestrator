// ── Enums ────────────────────────────────────────────────────────────────────

export enum JobStatus {
  Pending   = 0,
  Running   = 1,
  Success   = 2,
  Failed    = 3,
  Retrying  = 4,
  Cancelled = 5,
  Cancelling = 6,
}

export enum ProcessStatus {
  Pending   = 0,
  Running   = 1,
  Success   = 2,
  Failed    = 3,
  Cancelled = 4,
}

export enum StepStatus {
  Pending = 0,
  Running = 1,
  Success = 2,
  Failed  = 3,
  Skipped = 4,
}

export enum VersionStatus {
  Draft       = 0,
  Published   = 1,
  Deprecated  = 2,
}

export enum WorkerStatusLabel {
  Online  = 'Online',
  Warning = 'Warning',
  Offline = 'Offline',
}

// ── Workers ───────────────────────────────────────────────────────────────────

export interface WorkerListItemDto {
  workerId: string;
  workerName: string;
  machineName: string;
  version: string;
  status: string;
  lastHeartbeatUtc: string;
  runningJobCount: number;
  cpuUsagePercent: number | null;
  memoryUsageMb: number | null;
}

export interface WorkerDetailDto extends WorkerListItemDto {
  startedAtUtc: string;
  processId: number;
  metadataJson: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface WorkerSummaryDto {
  total: number;
  online: number;
  warning: number;
  offline: number;
  runningJobs: number;
}

// ── Common ───────────────────────────────────────────────────────────────────

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

// ── Process Definitions ───────────────────────────────────────────────────────

export interface ProcessDefinitionDto {
  id: string;
  name: string;
  description: string | null;
  category: string | null;
  isActive: boolean;
  createdAt: string;
  versionCount: number;
  latestVersionNumber: number | null;
}

export interface CreateProcessDefinitionRequest {
  name: string;
  description?: string;
  category?: string;
}

export interface UpdateProcessDefinitionRequest {
  name?: string;
  description?: string;
  category?: string;
  isActive?: boolean;
}

export interface ProcessVersionDto {
  id: string;
  processDefinitionId: string;
  versionNumber: number;
  jsonDefinition: string;
  status: VersionStatus;
  changeNotes: string | null;
  publishedAt: string | null;
  createdAt: string;
}

export interface CreateProcessVersionRequest {
  jsonDefinition: string;
  changeNotes?: string;
}

// ── Process Instances ─────────────────────────────────────────────────────────

export interface StepInstanceDto {
  id: string;
  stepId: string;
  stepName: string;
  stepType: string;
  status: StepStatus;
  errorMessage: string | null;
  startedAt: string | null;
  completedAt: string | null;
  durationMs: number | null;
  /** Final attempt number — 1 means first attempt succeeded, >1 means retries occurred. */
  attemptNumber: number;
}

export interface JobLogDto {
  id: string;
  jobId: string;
  stepInstanceId: string | null;
  level: string;
  message: string;
  details: string | null;
  exception: string | null;
  createdAt: string;
}

export interface ProcessInstanceDto {
  id: string;
  processDefinitionId: string;
  processDefinitionName: string;
  processVersionId: string;
  versionNumber: number;
  status: ProcessStatus;
  correlationId: string | null;
  inputData: string | null;
  outputData: string | null;
  errorMessage: string | null;
  startedAt: string | null;
  completedAt: string | null;
  triggeredBy: string | null;
  createdAt: string;
  steps: StepInstanceDto[];
}

export interface StartProcessRequest {
  processDefinitionId: string;
  correlationId?: string;
  inputData?: string;
  triggeredBy?: string;
}

// ── Jobs ─────────────────────────────────────────────────────────────────────

export interface JobDto {
  id: string;
  processInstanceId: string;
  status: JobStatus;
  attemptCount: number;
  maxAttempts: number;
  scheduledAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  errorMessage: string | null;
  cancelRequestedAtUtc: string | null;
  cancelledAtUtc: string | null;
  cancelReason: string | null;
  cancelledBy: string | null;
  createdAt: string;
}

export interface CancelJobRequest {
  reason?: string;
  cancelledBy?: string;
}

export interface CancelJobResponse {
  jobId: string;
  status: string;
}

// ── Artifacts ─────────────────────────────────────────────────────────────────

export interface ArtifactDto {
  id: string;
  processInstanceId: string;
  stepInstanceId: string | null;
  name: string;
  contentType: string;
  sizeBytes: number;
  isPersistent: boolean;
  createdAt: string;
  metadata: Record<string, string> | null;
  downloadUrl: string;
}

// ── Triggers ──────────────────────────────────────────────────────────────────

export interface TriggerDto {
  id: string;
  processDefinitionId: string;
  name: string;
  type: string;
  isActive: boolean;
  cronExpression: string | null;
  configJson: string | null;
  lastTriggeredAt: string | null;
  nextScheduledAt: string | null;
  createdAt: string;
  lastEventStatus: string | null;
  lastEventError: string | null;
}

export interface CreateTriggerRequest {
  processDefinitionId: string;
  name: string;
  type: string;
  isActive: boolean;
  cronExpression?: string;
  configJson?: string;
  defaultInputData?: string;
}

export interface UpdateTriggerRequest {
  name?: string;
  isActive?: boolean;
  cronExpression?: string;
  configJson?: string;
  defaultInputData?: string;
}

export interface TriggerEventDto {
  id: string;
  triggerId: string;
  eventKey: string;
  filePath: string;
  fileName: string;
  status: string;
  processInstanceId: string | null;
  errorMessage: string | null;
  createdAt: string;
  completedAt: string | null;
}

// ── Browser Picker ────────────────────────────────────────────────────────────

export type BrowserPickerConfidence = 'high' | 'medium' | 'low';

export interface BrowserPickerCandidate {
  selector: string;
  strategy: string;
  confidence: BrowserPickerConfidence;
  matchCount: number;
  reason: string;
}

export interface BrowserPickerSelectedElement {
  tagName: string;
  text: string;
  id: string;
  name: string;
  ariaLabel: string;
  href: string;
}

export interface BrowserPickerSelectedResponse {
  primarySelector: string;
  /** Alias for primarySelector — backward compatible */
  selector: string;
  candidates: BrowserPickerCandidate[];
  selectedElement: BrowserPickerSelectedElement;
  originalClickedElement: BrowserPickerSelectedElement;
  resolvedClickableElement: BrowserPickerSelectedElement;
  // Legacy flat fields (resolved target)
  tagName: string;
  text: string;
  id: string;
  name: string;
  ariaLabel: string;
  href: string;
}

export type {
  CredentialDto,
  CreateCredentialRequest,
  UpdateCredentialRequest,
  CredentialTestResponse,
  CredentialType,
} from './credentialTypes';

export { CREDENTIAL_TYPES } from './credentialTypes';

// ── Auth / RBAC ───────────────────────────────────────────────────────────────

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresAtUtc: string;
  user: UserProfileDto;
}

export interface UserProfileDto {
  id: string;
  username: string;
  displayName: string;
  email: string;
  roles: string[];
  permissions: string[];
}

export interface UserDto {
  id: string;
  username: string;
  displayName: string;
  email: string;
  isActive: boolean;
  roles: string[];
  createdAt: string;
}

export interface CreateUserRequest {
  username: string;
  displayName: string;
  email: string;
  password: string;
  isActive?: boolean;
  roles?: string[];
}

export interface UpdateUserRequest {
  displayName?: string;
  email?: string;
  password?: string;
  isActive?: boolean;
}

export interface AssignUserRolesRequest {
  roles: string[];
}

export interface RoleDto {
  id: string;
  name: string;
  description?: string;
  permissions: string[];
}

export interface PermissionDto {
  id: string;
  name: string;
  description?: string;
}

export interface UpdateRolePermissionsRequest {
  permissions: string[];
}

// ── Dashboard KPI ─────────────────────────────────────────────────────────────

export interface DashboardKpiDto {
  range: DashboardRangeDto;
  jobs: DashboardJobStatsDto;
  instances: DashboardInstanceStatsDto;
  workers: DashboardWorkerStatsDto;
  topWorkflows: TopWorkflowDto[];
  failingWorkflows: FailingWorkflowDto[];
  recentFailures: RecentFailureDto[];
  throughputByHour: ThroughputHourDto[];
}

export interface DashboardRangeDto {
  fromUtc: string;
  toUtc: string;
}

export interface DashboardJobStatsDto {
  total: number;
  succeeded: number;
  failed: number;
  cancelled: number;
  running: number;
  pending: number;
  successRate: number;
  failureRate: number;
  averageDurationSeconds: number;
}

export interface DashboardInstanceStatsDto {
  total: number;
  succeeded: number;
  failed: number;
  cancelled: number;
}

export interface DashboardWorkerStatsDto {
  total: number;
  online: number;
  warning: number;
  offline: number;
  runningJobs: number;
}

export interface TopWorkflowDto {
  processDefinitionId: string;
  name: string;
  runCount: number;
  successCount: number;
  failedCount: number;
  averageDurationSeconds: number;
}

export interface FailingWorkflowDto {
  processDefinitionId: string;
  name: string;
  failedCount: number;
  lastFailedAtUtc: string | null;
}

export interface RecentFailureDto {
  jobId: string;
  processInstanceId: string;
  processName: string;
  failedAtUtc: string | null;
  error: string | null;
}

export interface ThroughputHourDto {
  hourUtc: string;
  succeeded: number;
  failed: number;
  cancelled: number;
}

// ── Audit ─────────────────────────────────────────────────────────────────────

export interface AuditQueryFilter {
  fromUtc?: string;
  toUtc?: string;
  category?: string;
  eventType?: string;
  username?: string;
  severity?: string;
  entityType?: string;
  entityId?: string;
  success?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface AuditLogListItemDto {
  id: string;
  timestampUtc: string;
  eventType: string;
  category: string;
  severity: string;
  username?: string | null;
  displayName?: string | null;
  entityType: string;
  entityId: string;
  entityName?: string | null;
  action: string;
  success: boolean;
}

export interface AuditLogDetailDto extends AuditLogListItemDto {
  userId?: string | null;
  detailsJson?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  correlationId?: string | null;
}

export interface AuditSummaryDto {
  totalEvents: number;
  criticalEvents: number;
  failedEvents: number;
  uniqueUsers: number;
  topUsers: AuditSummaryItemDto[];
  topCategories: AuditSummaryItemDto[];
}

export interface AuditSummaryItemDto {
  name: string;
  count: number;
}
