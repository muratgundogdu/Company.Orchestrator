import { Link, useParams } from 'react-router-dom';
import {
  ArrowLeft, Briefcase, Archive, FileText, FileWarning, File, Download,
  RefreshCw, AlertTriangle,
} from 'lucide-react';
import { processInstanceApi, jobApi, artifactApi } from '../../api/endpoints';
import { useApi } from '../../hooks/useApi';
import type { JobLogDto } from '../../api/types';
import PageHeader from '../../components/PageHeader';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import { ProcessStatusBadge, StepStatusBadge, JobStatusBadge } from '../../components/StatusBadge';
import { fmtDate, fmtDuration, elapsed, fmtBytes, shortId } from '../../utils/format';
import { StepStatus } from '../../api/types';

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

export default function ProcessInstanceDetail() {
  const { id } = useParams<{ id: string }>();

  const { data: inst, loading, error, refetch } = useApi(
    () => processInstanceApi.getById(id!),
    [id],
  );
  const { data: jobsData } = useApi(
    () => jobApi.list(1, 50, id),
    [id],
  );
  const { data: artifacts } = useApi(
    () => artifactApi.getByProcessInstance(id!),
    [id],
  );
  const { data: instanceLogs } = useApi(
    () => processInstanceApi.getLogs(id!),
    [id],
  );

  if (loading) return <LoadingSpinner />;
  if (error)   return <ErrorAlert message={error} onRetry={refetch} />;
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
          <Link to="/process-instances" className="btn btn-secondary">
            <ArrowLeft size={14} /> Back
          </Link>
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
          <p className="text-center text-gray-400 py-10 text-sm">No steps recorded</p>
        ) : (
          <div className="divide-y divide-gray-100">
            {sortedSteps.map((step, idx) => {
              const stepLogList  = logsByStep.get(step.id) ?? [];
              const retryEntries = retryLogs(stepLogList);
              const hadRetries   = (step.attemptNumber ?? 1) > 1 || retryEntries.length > 0;
              const isExhausted  = step.status === StepStatus.Failed && hadRetries;

              return (
                <div
                  key={step.id}
                  className={`flex items-start gap-4 px-4 py-3 ${isExhausted ? 'bg-red-50' : hadRetries ? 'bg-amber-50' : ''}`}
                >
                  {/* Step number */}
                  <div className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold shrink-0 mt-0.5 ${
                    step.status === StepStatus.Success
                      ? 'bg-green-100 text-green-700'
                      : step.status === StepStatus.Failed
                        ? 'bg-red-100 text-red-700'
                        : step.status === StepStatus.Running
                          ? 'bg-blue-100 text-blue-700'
                          : 'bg-gray-100 text-gray-500'
                  }`}>
                    {idx + 1}
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
                      <a
                        href={a.downloadUrl}
                        className={`btn btn-sm ${isFailureReport ? 'btn-danger' : 'btn-secondary'}`}
                        download={a.name}
                      >
                        <Download size={12} /> Download
                      </a>
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
