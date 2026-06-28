import { JobStatus, ProcessStatus, StepStatus, VersionStatus } from '../api/types';

type BadgeVariant = 'gray' | 'blue' | 'green' | 'red' | 'yellow' | 'purple';

const variantClass: Record<BadgeVariant, string> = {
  gray:   'bg-gray-100 text-gray-600 ring-gray-200',
  blue:   'bg-blue-100 text-blue-700 ring-blue-200',
  green:  'bg-green-100 text-green-700 ring-green-200',
  red:    'bg-red-100 text-red-700 ring-red-200',
  yellow: 'bg-amber-100 text-amber-700 ring-amber-200',
  purple: 'bg-purple-100 text-purple-700 ring-purple-200',
};

interface BadgeProps {
  label: string;
  variant: BadgeVariant;
}

function Badge({ label, variant }: BadgeProps) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${variantClass[variant]}`}
    >
      {label}
    </span>
  );
}

// ── Job Status ────────────────────────────────────────────────────────────────

const JOB_MAP: Record<number, { label: string; variant: BadgeVariant }> = {
  [JobStatus.Pending]:   { label: 'Pending',   variant: 'gray' },
  [JobStatus.Running]:   { label: 'Running',   variant: 'blue' },
  [JobStatus.Success]:   { label: 'Success',   variant: 'green' },
  [JobStatus.Failed]:    { label: 'Failed',    variant: 'red' },
  [JobStatus.Retrying]:  { label: 'Retrying',  variant: 'yellow' },
  [JobStatus.Cancelled]: { label: 'Cancelled', variant: 'gray' },
  [JobStatus.Cancelling]: { label: 'Cancelling', variant: 'yellow' },
};

export function JobStatusBadge({ status }: { status: number }) {
  const cfg = JOB_MAP[status] ?? { label: String(status), variant: 'gray' as BadgeVariant };
  return <Badge {...cfg} />;
}

// ── Process Status ────────────────────────────────────────────────────────────

const PROCESS_MAP: Record<number, { label: string; variant: BadgeVariant }> = {
  [ProcessStatus.Pending]:   { label: 'Pending',   variant: 'gray' },
  [ProcessStatus.Running]:   { label: 'Running',   variant: 'blue' },
  [ProcessStatus.Success]:   { label: 'Success',   variant: 'green' },
  [ProcessStatus.Failed]:    { label: 'Failed',    variant: 'red' },
  [ProcessStatus.Cancelled]: { label: 'Cancelled', variant: 'gray' },
};

export function ProcessStatusBadge({ status }: { status: number }) {
  const cfg = PROCESS_MAP[status] ?? { label: String(status), variant: 'gray' as BadgeVariant };
  return <Badge {...cfg} />;
}

// ── Step Status ───────────────────────────────────────────────────────────────

const STEP_MAP: Record<number, { label: string; variant: BadgeVariant }> = {
  [StepStatus.Pending]: { label: 'Pending', variant: 'gray' },
  [StepStatus.Running]: { label: 'Running', variant: 'blue' },
  [StepStatus.Success]: { label: 'Success', variant: 'green' },
  [StepStatus.Failed]:  { label: 'Failed',  variant: 'red' },
  [StepStatus.Skipped]: { label: 'Skipped', variant: 'yellow' },
};

export function StepStatusBadge({ status }: { status: number }) {
  const cfg = STEP_MAP[status] ?? { label: String(status), variant: 'gray' as BadgeVariant };
  return <Badge {...cfg} />;
}

// ── Version Status ────────────────────────────────────────────────────────────

const VERSION_MAP: Record<number, { label: string; variant: BadgeVariant }> = {
  [VersionStatus.Draft]:      { label: 'Draft',      variant: 'yellow' },
  [VersionStatus.Published]:  { label: 'Published',  variant: 'green' },
  [VersionStatus.Deprecated]: { label: 'Deprecated', variant: 'gray' },
};

export function VersionStatusBadge({ status }: { status: number }) {
  const cfg = VERSION_MAP[status] ?? { label: String(status), variant: 'gray' as BadgeVariant };
  return <Badge {...cfg} />;
}

// ── Generic string badge (for trigger events, etc.) ───────────────────────────

const STRING_STATUS_MAP: Record<string, BadgeVariant> = {
  pending:   'gray',
  running:   'blue',
  completed: 'green',
  success:   'green',
  failed:    'red',
  error:     'red',
  skipped:   'yellow',
};

export function StringStatusBadge({ status }: { status: string }) {
  const variant = STRING_STATUS_MAP[status.toLowerCase()] ?? 'gray';
  return <Badge label={status} variant={variant} />;
}

export function ActiveBadge({ active }: { active: boolean }) {
  return <Badge label={active ? 'Active' : 'Inactive'} variant={active ? 'green' : 'gray'} />;
}

// ── Worker Status ─────────────────────────────────────────────────────────────

const WORKER_STATUS_MAP: Record<string, { label: string; variant: BadgeVariant; emoji: string }> = {
  online:  { label: 'Online',  variant: 'green',  emoji: '🟢' },
  warning: { label: 'Warning', variant: 'yellow', emoji: '🟡' },
  offline: { label: 'Offline', variant: 'red',    emoji: '🔴' },
};

export function WorkerStatusBadge({ status }: { status: string }) {
  const cfg = WORKER_STATUS_MAP[status.toLowerCase()] ?? { label: status, variant: 'gray' as BadgeVariant, emoji: '⚪' };
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${variantClass[cfg.variant]}`}
    >
      <span aria-hidden>{cfg.emoji}</span>
      {cfg.label}
    </span>
  );
}
