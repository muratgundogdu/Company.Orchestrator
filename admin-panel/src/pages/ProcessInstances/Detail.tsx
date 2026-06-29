import { Link, useParams } from 'react-router-dom';
import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ArrowLeft, Briefcase, Archive, FileText, FileWarning, File, Download,
  RefreshCw, AlertTriangle, Check, X, Loader2, Wifi, WifiOff,
} from 'lucide-react';
import { processInstanceApi, jobApi, artifactApi } from '../../api/endpoints';
import { useApi } from '../../hooks/useApi';
import { useInstanceMonitoring, type LiveConnectionStatus } from '../../hooks/useInstanceMonitoring';
import type { JobLogDto, ProcessInstanceDto } from '../../api/types';
import PageHeader from '../../components/PageHeader';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import { ProcessStatusBadge, StepStatusBadge, JobStatusBadge } from '../../components/StatusBadge';
import { fmtDate, fmtDuration, elapsed, fmtBytes, shortId } from '../../utils/format';
import { downloadArtifact } from '../../utils/downloadArtifact';
import { ProcessStatus, StepStatus } from '../../api/types';
import {
  applyInstanceCompleted,
  applyStepCompleted,
  applyStepFailed,
  applyStepStarted,
} from '../../monitoring/applyInstanceMonitoringEvent';

// ── helpers ───────────────────────────────────────────────────────────────────

/** Groups log entries by stepInstanceId. Falls back to 'job' key for logs without a step. */
function groupLogsByStep(logs: JobLogDto[]): Map<string, JobLogDto[]> {
  const map = new Map<string, JobLogDto[]>();
  for (const log of logs) {
    const key = log.stepInstanceId ?? '__job__';
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(log);
  }
  return map;
}

/** Returns logs that are retry-related (Warning/Error levels from attempt tracking). */
function retryLogs(logs: JobLogDto[]): JobLogDto[] {
  return logs.filter((l) =>
    l.level === 'Warning' || l.level === 'Error' ||
    /attempt|retry|retrying|exhausted|succeeded/i.test(l.message),
  );
}

/** Badge colour based on log level. */
function levelClass(level: string): string {
  switch (level.toLowerCase()) {
    case 'error':   return 'bg-red-100 text-red-700';
    case 'warning': return 'bg-amber-100 text-amber-700';
    default:        return 'bg-gray-100 text-gray-600';
  }
}

function LiveStatusBadge({ status }: { status: LiveConnectionStatus }) {
  switch (status) {
    case 'connecting':
      return (
        <span className="inline-flex items-center gap-1 text-xs px-2 py-1 rounded bg-blue-50 text-blue-700 border border-blue-200">
          <Loader2 size={12} className="animate-spin" />
          Connecting...
        </span>
      );
    case 'connected':
      return (
        <span className="inline-flex items-center gap-1 text-xs px-2 py-1 rounded bg-green-50 text-green-700 border border-green-200">
          <Wifi size={12} />
          Live connected
        </span>
      );
    case 'disconnected':
      return (
        <span className="inline-flex items-center gap-1 text-xs px-2 py-1 rounded bg-amber-50 text-amber-700 border border-amber-200">
          <WifiOff size={12} />
          Live disconnected - polling
        </span>
      );
    default:
      return null;
  }
}

function isTerminalStatus(status: ProcessStatus): boolean {
  return status === ProcessStatus.Success
    || status === ProcessStatus.Failed
    || status === ProcessStatus.Cancelled;
}

const ACTIVE_POLL_INTERVAL_MS = 3000;
const LIVE_REFETCH_DEBOUNCE_MS = 500;

export default function ProcessInstanceDetail() {
  const { id } = useParams<{ id: string }>();
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const [inst, setInst] = useState<ProcessInstanceDto | null>(null);

  async function handleDownload(artifactId: string, name: string) {
    setDownloadingId(artifactId);
    try {
      await downloadArtifact(artifactId, name);
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Download failed');
    } finally {
      setDownloadingId(null);
    }
  }

  const { data: fetchedInst, loading, error, refetch } = useApi(
    () => processInstanceApi.getById(id!),
    [id],
  );
  const { data: jobsData, refetch: refetchJobs } = useApi(
    () => jobApi.list(1, 50, id),
    [id],
  );
  const { data: artifacts, refetch: refetchArtifacts } = useApi(
    () => artifactApi.getByProcessInstance(id!),
    [id],
  );
  const { data: instanceLogs, refetch: refetchLogs } = useApi(
    () => processInstanceApi.getLogs(id!),
    [id],
  );

  useEffect(() => {
    if (fetchedInst) setInst(fetchedInst);
  }, [fetchedInst]);

  const refetchAllSilent = useCallback(() => {
    refetch({ silent: true });
    refetchJobs({ silent: true });
    refetchArtifacts({ silent: true });
    refetchLogs({ silent: true });
  }, [refetch, refetchJobs, refetchArtifacts, refetchLogs]);

  const refetchAll = useCallback(() => {
    refetch();
    refetchJobs();
    refetchArtifacts();
    refetchLogs();
  }, [refetch, refetchJobs, refetchArtifacts, refetchLogs]);

  const debouncedRefetchRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const flushDebouncedRefetch = useCallback(() => {
    if (debouncedRefetchRef.current) {
      clearTimeout(debouncedRefetchRef.current);
      debouncedRefetchRef.current = null;
    }
    refetchAllSilent();
  }, [refetchAllSilent]);

  const scheduleDebouncedRefetch = useCallback(() => {
    if (debouncedRefetchRef.current) clearTimeout(debouncedRefetchRef.current);
    debouncedRefetchRef.current = setTimeout(() => {
      debouncedRefetchRef.current = null;
      refetchAllSilent();
    }, LIVE_REFETCH_DEBOUNCE_MS);
  }, [refetchAllSilent]);

  useEffect(() => () => {
    if (debouncedRefetchRef.current) clearTimeout(debouncedRefetchRef.current);
  }, []);

  const instanceStatus = (inst ?? fetchedInst)?.status;
  const isPending = instanceStatus === ProcessStatus.Pending;
  const isInstanceRunning = instanceStatus === ProcessStatus.Running;
  const isActive = isPending || isInstanceRunning;
  const stepCount = inst?.steps.length ?? fetchedInst?.steps.length ?? 0;

  const monitoringReady = !loading && !!fetchedInst;
  const signalREnabled = monitoringReady && isInstanceRunning;

  const [joinGeneration, setJoinGeneration] = useState(0);

  const handleJoined = useCallback(() => {
    refetchAllSilent();
    setJoinGeneration((g) => g + 1);
  }, [refetchAllSilent]);

  const applyUpdate = useCallback((updater: (current: ProcessInstanceDto) => ProcessInstanceDto) => {
    setInst((current) => (current ? updater(current) : current));
  }, []);

  const { status: liveStatus } = useInstanceMonitoring({
    processInstanceId: id,
    enabled: signalREnabled,
    onStepStarted: (event) => applyUpdate((current) => applyStepStarted(current, event)),
    onStepCompleted: (event) => {
      applyUpdate((current) => applyStepCompleted(current, event));
      scheduleDebouncedRefetch();
    },
    onStepFailed: (event) => {
      applyUpdate((current) => applyStepFailed(current, event));
      scheduleDebouncedRefetch();
    },
    onInstanceCompleted: (event) => {
      applyUpdate((current) => applyInstanceCompleted(current, event));
      flushDebouncedRefetch();
    },
    onJoined: handleJoined,
  });

  // Safety refetch once ~5s after each successful SignalR join.
  useEffect(() => {
    if (!signalREnabled || liveStatus !== 'connected') return;
    const timer = setTimeout(() => refetchAllSilent(), 5000);
    return () => clearTimeout(timer);
  }, [joinGeneration, liveStatus, signalREnabled, refetchAllSilent]);

  // Poll while Pending/Running: always for Pending, when SignalR down, or until steps appear.
  useEffect(() => {
    if (!monitoringReady || !isActive) return;

    const shouldPoll =
      isPending
      || liveStatus !== 'connected'
      || stepCount === 0;

    if (!shouldPoll) return;

    const timer = setInterval(() => refetchAllSilent(), ACTIVE_POLL_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [monitoringReady, isActive, isPending, liveStatus, stepCount, refetchAllSilent]);

  if (loading) return <LoadingSpinner />;
  if (error)   return <ErrorAlert message={error} onRetry={refetchAll} />;
  if (!inst)   return null;

  const logsByStep = groupLogsByStep(instanceLogs ?? []);

  const sortedSteps = [...inst.steps].sort((a, b) => {
    if (!a.startedAt && !b.startedAt) return 0;
    if (!a.startedAt) return 1;
    if (!b.startedAt) return -1;
    return new Date(a.startedAt).getTime() - new Date(b.startedAt).getTime();
  });

  return (
    <div>
      <PageHeader
        title={inst.processDefinitionName}
        subtitle={`Instance ${shortId(inst.id)} — v${inst.versionNumber}`}
        actions={
          <div className="flex items-center gap-2">
            {isInstanceRunning && <LiveStatusBadge status={liveStatus} />}
            {isPending && (
              <span className="inline-flex items-center gap-1 text-xs px-2 py-1 rounded bg-gray-50 text-gray-600 border border-gray-200">
                <Loader2 size={12} className="animate-spin" />
                Polling…
              </span>
            )}
            <button
              type="button"
              onClick={() => refetchAll()}
              className="btn btn-secondary"
              title="Refresh"
            >
              <RefreshCw size={14} /> Refresh
            </button>
            <Link to="/process-instances" className="btn btn-secondary">
              <ArrowLeft size={14} /> Back
            </Link>
          </div>
        }
      />

      {/* Info card */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 mb-6">
        <div className="card p-5 space-y-3 lg:col-span-2">
          <h3 className="font-semibold text-gray-700 text-sm uppercase tracking-wide">Instance Details</h3>
          {[
            { label: 'ID',           value: <span className="font-mono text-xs">{inst.id}</span> },
            { label: 'Definition',   value: inst.processDefinitionName },
            { label: 'Version',      value: `v${inst.versionNumber}` },
            { label: 'Status',       value: <ProcessStatusBadge status={inst.status} /> },
            { label: 'Correlation',  value: inst.correlationId ?? '—' },
            { label: 'Triggered By', value: inst.triggeredBy ?? '—' },
            { label: 'Started',      value: fmtDate(inst.startedAt) },
            { label: 'Completed',    value: fmtDate(inst.completedAt) },
            { label: 'Duration',     value: elapsed(inst.startedAt, inst.completedAt) },
          ].map(({ label, value }) => (
            <div key={label} className="flex items-start justify-between gap-4">
              <span className="text-sm text-gray-500 w-28 shrink-0">{label}</span>
              <span className="text-sm text-gray-900 font-medium text-right">{value}</span>
            </div>
          ))}
        </div>

        {inst.errorMessage && (
          <div className="card p-5 border-red-200 bg-red-50">
            <h3 className="font-semibold text-red-700 text-sm uppercase tracking-wide mb-3">Error</h3>
            <p className="text-sm text-red-600 font-mono whitespace-pre-wrap break-all">{inst.errorMessage}</p>
          </div>
        )}
      </div>

      {/* Steps timeline */}
      <div className="card overflow-hidden mb-6">
        <div className="px-4 py-3 border-b border-gray-200">
          <h3 className="font-semibold text-gray-700">Steps ({inst.steps.length})</h3>
        </div>
        {inst.steps.length === 0 ? (
          isPending ? (
            <div className="flex flex-col items-center justify-center py-10 text-center">
              <Loader2 size={28} className="animate-spin text-blue-500 mb-3" />
              <p className="text-sm text-gray-600 font-medium">Workflow queued, waiting for worker…</p>
              <p className="text-xs text-gray-400 mt-1">Worker bekleniyor…</p>
            </div>
          ) : isInstanceRunning ? (
            <div className="flex flex-col items-center justify-center py-10 text-center">
              <Loader2 size={28} className="animate-spin text-blue-500 mb-3" />
              <p className="text-sm text-gray-600 font-medium">Starting workflow…</p>
            </div>
          ) : isTerminalStatus(inst.status) ? (
            <p className="text-center text-gray-400 py-10 text-sm">No steps recorded</p>
          ) : null
        ) : (
          <div className="divide-y divide-gray-100">
            {sortedSteps.map((step, idx) => {
              const stepLogList  = logsByStep.get(step.id) ?? [];
              const retryEntries = retryLogs(stepLogList);
              const hadRetries   = (step.attemptNumber ?? 1) > 1 || retryEntries.length > 0;
              const isExhausted  = step.status === StepStatus.Failed && hadRetries;
              const isRunning    = step.status === StepStatus.Running;

              const stepIcon = step.status === StepStatus.Success
                ? <Check size={14} />
                : step.status === StepStatus.Failed
                  ? <X size={14} />
                  : isRunning
                    ? <Loader2 size={14} className="animate-spin" />
                    : idx + 1;

              return (
                <div
                  key={step.id}
                  className={`flex items-start gap-4 px-4 py-3 ${
                    isRunning
                      ? 'bg-blue-50 ring-1 ring-inset ring-blue-200'
                      : isExhausted
                        ? 'bg-red-50'
                        : hadRetries
                          ? 'bg-amber-50'
                          : ''
                  }`}
                >
                  {/* Step number / status icon */}
                  <div className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold shrink-0 mt-0.5 ${
                    step.status === StepStatus.Success
                      ? 'bg-green-100 text-green-700'
                      : step.status === StepStatus.Failed
                        ? 'bg-red-100 text-red-700'
                        : isRunning
                          ? 'bg-blue-100 text-blue-700'
                          : 'bg-gray-100 text-gray-500'
                  }`}>
                    {stepIcon}
                  </div>

                  <div className="flex-1 min-w-0">
                    {/* Step header row */}
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="font-medium text-sm text-gray-900">{step.stepName}</span>
                      <StepStatusBadge status={step.status} />
                      <span className="text-xs text-gray-400 font-mono">{step.stepType}</span>

                      {/* Attempt badge — shown only when retries occurred */}
                      {(step.attemptNumber ?? 1) > 1 && (
                        <span className={`inline-flex items-center gap-1 text-xs px-1.5 py-0.5 rounded font-medium ${
                          isExhausted
                            ? 'bg-red-100 text-red-700'
                            : 'bg-amber-100 text-amber-700'
                        }`}>
                          <RefreshCw size={10} />
                          Attempts: {step.attemptNumber}
                        </span>
                      )}
                    </div>

                    {/* Timing row */}
                    <div className="flex gap-4 mt-1 text-xs text-gray-400">
                      <span>Started: {fmtDate(step.startedAt)}</span>
                      <span>Duration: {fmtDuration(step.durationMs)}</span>
                    </div>

                    {/* Final error */}
                    {step.errorMessage && (
                      <p className="mt-1 text-xs text-red-600 bg-red-50 rounded px-2 py-1 font-mono">
                        {step.errorMessage}
                      </p>
                    )}

                    {/* Retry log entries */}
                    {retryEntries.length > 0 && (
                      <div className="mt-2 space-y-1">
                        <p className="text-xs font-semibold text-gray-500 flex items-center gap-1">
                          <AlertTriangle size={11} className="text-amber-500" />
                          Retry log ({retryEntries.length})
                        </p>
                        {retryEntries.map((log) => (
                          <div
                            key={log.id}
                            className="flex items-start gap-2 text-xs"
                          >
                            <span className={`shrink-0 rounded px-1 py-0.5 font-semibold text-xs ${levelClass(log.level)}`}>
                              {log.level}
                            </span>
                            <span className="text-gray-600 font-mono">{log.message}</span>
                            <span className="text-gray-300 shrink-0 ml-auto">{fmtDate(log.createdAt)}</span>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Jobs */}
      {jobsData && jobsData.items.length > 0 && (
        <div className="card overflow-hidden mb-6">
          <div className="px-4 py-3 border-b border-gray-200 flex items-center gap-2">
            <Briefcase size={16} className="text-gray-400" />
            <h3 className="font-semibold text-gray-700">Jobs ({jobsData.items.length})</h3>
          </div>
          <table className="w-full">
            <thead>
              <tr>
                <th className="table-th">Job ID</th>
                <th className="table-th">Status</th>
                <th className="table-th">Job Attempts</th>
                <th className="table-th">Started</th>
                <th className="table-th">Completed</th>
                <th className="table-th">Error</th>
              </tr>
            </thead>
            <tbody>
              {jobsData.items.map((job) => (
                <tr key={job.id} className="table-tr">
                  <td className="table-td font-mono text-xs text-gray-500">{shortId(job.id)}</td>
                  <td className="table-td"><JobStatusBadge status={job.status} /></td>
                  <td className="table-td text-gray-500">{job.attemptCount}/{job.maxAttempts}</td>
                  <td className="table-td text-gray-500">{fmtDate(job.startedAt)}</td>
                  <td className="table-td text-gray-500">{fmtDate(job.completedAt)}</td>
                  <td className="table-td max-w-xs">
                    {job.errorMessage && (
                      <span className="text-xs text-red-500 truncate block">{job.errorMessage}</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Artifacts */}
      {artifacts && artifacts.length > 0 && (
        <div className="card overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-200 flex items-center gap-2">
            <Archive size={16} className="text-gray-400" />
            <h3 className="font-semibold text-gray-700">Artifacts ({artifacts.length})</h3>
          </div>
          <table className="w-full">
            <thead>
              <tr>
                <th className="table-th">Name</th>
                <th className="table-th">Type</th>
                <th className="table-th">Size</th>
                <th className="table-th">Created</th>
                <th className="table-th">Download</th>
              </tr>
            </thead>
            <tbody>
              {artifacts.map((a) => {
                const isFailureReport = a.name.startsWith('failure-report-');
                const isPlainText = a.contentType === 'text/plain';
                const Icon = isFailureReport
                  ? FileWarning
                  : isPlainText
                  ? FileText
                  : File;
                const iconClass = isFailureReport
                  ? 'text-red-500'
                  : isPlainText
                  ? 'text-blue-400'
                  : 'text-gray-400';
                return (
                  <tr key={a.id} className={`table-tr ${isFailureReport ? 'bg-red-50' : ''}`}>
                    <td className="table-td">
                      <div className="flex items-center gap-2">
                        <Icon size={16} className={iconClass} />
                        <span className={`font-medium text-sm ${isFailureReport ? 'text-red-700' : 'text-gray-900'}`}>
                          {a.name}
                        </span>
                        {isFailureReport && (
                          <span className="text-xs bg-red-100 text-red-700 px-1.5 py-0.5 rounded font-medium">
                            Failure Report
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="table-td text-gray-500 text-xs font-mono">{a.contentType}</td>
                    <td className="table-td text-gray-500 text-sm">{fmtBytes(a.sizeBytes)}</td>
                    <td className="table-td text-gray-500 text-sm">{fmtDate(a.createdAt)}</td>
                    <td className="table-td">
                      <button
                        type="button"
                        onClick={() => handleDownload(a.id, a.name)}
                        disabled={downloadingId === a.id}
                        className={`btn btn-sm inline-flex items-center gap-1 ${isFailureReport ? 'btn-danger' : 'btn-secondary'}`}
                      >
                        <Download size={12} />
                        {downloadingId === a.id ? 'Downloading…' : 'Download'}
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
