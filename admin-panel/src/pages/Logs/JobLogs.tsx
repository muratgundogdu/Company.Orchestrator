import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Search, ExternalLink, RefreshCw, AlertTriangle, Info } from 'lucide-react';
import { jobApi, processInstanceApi } from '../../api/endpoints';
import type { JobDto, JobLogDto, ProcessInstanceDto, StepInstanceDto } from '../../api/types';
import { StepStatus } from '../../api/types';
import PageHeader from '../../components/PageHeader';
import LoadingSpinner from '../../components/LoadingSpinner';
import { JobStatusBadge, StepStatusBadge, ProcessStatusBadge } from '../../components/StatusBadge';
import { fmtDate, fmtDuration, elapsed, shortId } from '../../utils/format';

// ── helpers ───────────────────────────────────────────────────────────────────

function groupLogsByStep(logs: JobLogDto[]): Map<string, JobLogDto[]> {
  const map = new Map<string, JobLogDto[]>();
  for (const log of logs) {
    const key = log.stepInstanceId ?? '__job__';
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(log);
  }
  return map;
}

/** Detect retry-relevant logs by level or message content. */
function retryLogs(logs: JobLogDto[]): JobLogDto[] {
  return logs.filter((l) =>
    l.level === 'Warning' || l.level === 'Error' ||
    /attempt|retry|retrying|exhausted|succeeded/i.test(l.message),
  );
}

function levelClass(level: string): string {
  switch (level.toLowerCase()) {
    case 'error':   return 'bg-red-100 text-red-700';
    case 'warning': return 'bg-amber-100 text-amber-700';
    default:        return 'bg-blue-50 text-blue-600';
  }
}

function stepNumberBg(step: StepInstanceDto): string {
  switch (step.status) {
    case StepStatus.Success: return 'bg-green-100 text-green-700';
    case StepStatus.Failed:  return 'bg-red-100 text-red-700';
    case StepStatus.Running: return 'bg-blue-100 text-blue-700';
    default:                 return 'bg-gray-100 text-gray-500';
  }
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function JobLogs() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [inputId, setInputId]           = useState(searchParams.get('jobId') ?? '');
  const [job, setJob]                   = useState<JobDto | null>(null);
  const [instance, setInstance]         = useState<ProcessInstanceDto | null>(null);
  const [instanceLogs, setInstanceLogs] = useState<JobLogDto[]>([]);
  const [loading, setLoading]           = useState(false);
  const [error, setError]               = useState<string | null>(null);

  async function load(jobId: string) {
    setLoading(true);
    setError(null);
    setJob(null);
    setInstance(null);
    setInstanceLogs([]);

    try {
      const jobRes  = await jobApi.getById(jobId);
      setJob(jobRes.data);

      const instRes  = await processInstanceApi.getById(jobRes.data.processInstanceId);
      setInstance(instRes.data);

      const logsRes  = await processInstanceApi.getLogs(jobRes.data.processInstanceId);
      setInstanceLogs(logsRes.data);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Not found');
    } finally {
      setLoading(false);
    }
  }

  async function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    const trimmed = inputId.trim();
    if (!trimmed) return;
    setSearchParams({ jobId: trimmed });
    await load(trimmed);
  }

  // Auto-load if jobId is in URL on first render
  useState(() => {
    const urlJobId = searchParams.get('jobId');
    if (urlJobId) {
      setInputId(urlJobId);
      load(urlJobId);
    }
  });

  const logsByStep = groupLogsByStep(instanceLogs);

  // All logs not tied to any step
  const jobLevelLogs = logsByStep.get('__job__') ?? [];

  return (
    <div>
      <PageHeader
        title="Job Logs"
        subtitle="Look up step execution logs by job ID"
      />

      {/* Search bar */}
      <form onSubmit={handleSearch} className="flex gap-3 mb-6">
        <input
          value={inputId}
          onChange={(e) => setInputId(e.target.value)}
          className="input flex-1 font-mono text-sm max-w-lg"
          placeholder="Enter Job ID (UUID)…"
        />
        <button type="submit" disabled={loading || !inputId.trim()} className="btn btn-primary">
          <Search size={14} />
          {loading ? 'Searching…' : 'Search'}
        </button>
      </form>

      {loading && <LoadingSpinner text="Loading job logs…" />}

      {error && (
        <div className="card p-6 text-center text-red-500">
          <p className="font-medium">Job not found</p>
          <p className="text-sm text-gray-400 mt-1">{error}</p>
        </div>
      )}

      {job && instance && (
        <div className="space-y-6">
          {/* Job summary card */}
          <div className="card p-5">
            <div className="flex items-start justify-between mb-4">
              <h3 className="font-semibold text-gray-700">Job Summary</h3>
              <div className="flex gap-2">
                <JobStatusBadge status={job.status} />
                <Link
                  to={`/process-instances/${job.processInstanceId}`}
                  className="btn btn-secondary btn-sm"
                >
                  <ExternalLink size={12} />
                  Instance
                </Link>
              </div>
            </div>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
              {[
                { label: 'Job ID',      value: <span className="font-mono text-xs">{job.id}</span> },
                { label: 'Instance',    value: <Link to={`/process-instances/${job.processInstanceId}`} className="font-mono text-xs text-blue-600 hover:underline">{shortId(job.processInstanceId)}</Link> },
                { label: 'Job Retries', value: `${job.attemptCount} / ${job.maxAttempts}` },
                { label: 'Started',     value: fmtDate(job.startedAt) },
                { label: 'Completed',   value: fmtDate(job.completedAt) },
                { label: 'Duration',    value: elapsed(job.startedAt, job.completedAt) },
                { label: 'Process',     value: instance.processDefinitionName },
                { label: 'Proc Status', value: <ProcessStatusBadge status={instance.status} /> },
              ].map(({ label, value }) => (
                <div key={label}>
                  <p className="text-xs text-gray-400 mb-0.5">{label}</p>
                  <p className="text-sm font-medium text-gray-900">{value}</p>
                </div>
              ))}
            </div>
            {job.errorMessage && (
              <div className="mt-4 bg-red-50 border border-red-200 rounded-lg p-3">
                <p className="text-xs font-semibold text-red-600 mb-1">Job Error</p>
                <p className="text-sm text-red-700 font-mono whitespace-pre-wrap break-all">{job.errorMessage}</p>
              </div>
            )}
          </div>

          {/* Steps log */}
          <div className="card overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-200">
              <h3 className="font-semibold text-gray-700">
                Step Execution Log ({instance.steps.length} steps
                {instanceLogs.length > 0 && `, ${instanceLogs.length} log entries`})
              </h3>
            </div>

            {instance.steps.length === 0 ? (
              <p className="text-center text-gray-400 py-10 text-sm">No step records</p>
            ) : (
              <div className="divide-y divide-gray-100">
                {[...instance.steps]
                  .sort((a, b) => {
                    if (!a.startedAt && !b.startedAt) return 0;
                    if (!a.startedAt) return 1;
                    if (!b.startedAt) return -1;
                    return new Date(a.startedAt).getTime() - new Date(b.startedAt).getTime();
                  })
                  .map((step, idx) => {
                    const stepLogList  = logsByStep.get(step.id) ?? [];
                    const retryEntries = retryLogs(stepLogList);
                    const hadRetries   = (step.attemptNumber ?? 1) > 1 || retryEntries.length > 0;
                    const isExhausted  = step.status === StepStatus.Failed && hadRetries;

                    return (
                      <div key={step.id} className={`px-4 py-4 ${isExhausted ? 'bg-red-50' : hadRetries ? 'bg-amber-50' : ''}`}>
                        <div className="flex items-start gap-3">
                          {/* Number + connector */}
                          <div className="flex flex-col items-center shrink-0">
                            <div className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold ${stepNumberBg(step)}`}>
                              {idx + 1}
                            </div>
                            {idx < instance.steps.length - 1 && (
                              <div className="w-px h-4 bg-gray-200 mt-1" />
                            )}
                          </div>

                          <div className="flex-1 min-w-0">
                            {/* Header */}
                            <div className="flex items-center flex-wrap gap-2">
                              <span className="font-semibold text-sm text-gray-900">{step.stepName}</span>
                              <StepStatusBadge status={step.status} />
                              <span className="text-xs text-gray-400 font-mono bg-gray-50 px-1.5 py-0.5 rounded">
                                {step.stepType}
                              </span>

                              {/* Attempt badge */}
                              {(step.attemptNumber ?? 1) > 1 && (
                                <span className={`inline-flex items-center gap-1 text-xs px-1.5 py-0.5 rounded font-medium ${
                                  isExhausted ? 'bg-red-100 text-red-700' : 'bg-amber-100 text-amber-700'
                                }`}>
                                  <RefreshCw size={10} />
                                  Attempts: {step.attemptNumber}
                                </span>
                              )}
                            </div>

                            {/* Timing */}
                            <div className="flex gap-4 mt-1 text-xs text-gray-400 flex-wrap">
                              <span>Step ID: <span className="font-mono">{step.stepId}</span></span>
                              <span>Started: {fmtDate(step.startedAt)}</span>
                              <span>Completed: {fmtDate(step.completedAt)}</span>
                              <span>Duration: {fmtDuration(step.durationMs)}</span>
                            </div>

                            {/* Final error */}
                            {step.errorMessage && (
                              <div className="mt-2 bg-red-50 border border-red-100 rounded px-3 py-2">
                                <p className="text-xs font-semibold text-red-500 mb-0.5">Error</p>
                                <p className="text-xs text-red-600 font-mono whitespace-pre-wrap break-all">
                                  {step.errorMessage}
                                </p>
                              </div>
                            )}

                            {/* Retry log entries */}
                            {retryEntries.length > 0 && (
                              <div className="mt-3 border border-amber-200 rounded-lg overflow-hidden">
                                <div className="px-3 py-1.5 bg-amber-50 flex items-center gap-1.5">
                                  <AlertTriangle size={12} className="text-amber-500" />
                                  <span className="text-xs font-semibold text-amber-700">
                                    Retry events ({retryEntries.length})
                                  </span>
                                </div>
                                <div className="divide-y divide-amber-100">
                                  {retryEntries.map((log) => (
                                    <div key={log.id} className="px-3 py-2 flex items-start gap-2 bg-white">
                                      <span className={`shrink-0 rounded px-1.5 py-0.5 font-semibold text-xs ${levelClass(log.level)}`}>
                                        {log.level}
                                      </span>
                                      <span className="flex-1 text-xs text-gray-700 font-mono">{log.message}</span>
                                      <span className="text-gray-300 text-xs shrink-0">{fmtDate(log.createdAt)}</span>
                                    </div>
                                  ))}
                                </div>
                              </div>
                            )}

                            {/* All other non-retry log entries */}
                            {stepLogList.filter(l => !retryEntries.includes(l)).length > 0 && (
                              <details className="mt-2">
                                <summary className="text-xs text-gray-400 cursor-pointer hover:text-gray-600 flex items-center gap-1">
                                  <Info size={11} />
                                  {stepLogList.filter(l => !retryEntries.includes(l)).length} info log(s)
                                </summary>
                                <div className="mt-1 space-y-0.5">
                                  {stepLogList.filter(l => !retryEntries.includes(l)).map((log) => (
                                    <div key={log.id} className="flex items-start gap-2 text-xs px-2 py-1 rounded bg-gray-50">
                                      <span className={`shrink-0 rounded px-1 py-0.5 font-semibold ${levelClass(log.level)}`}>
                                        {log.level}
                                      </span>
                                      <span className="flex-1 text-gray-600 font-mono">{log.message}</span>
                                      <span className="text-gray-300 shrink-0">{fmtDate(log.createdAt)}</span>
                                    </div>
                                  ))}
                                </div>
                              </details>
                            )}
                          </div>
                        </div>
                      </div>
                    );
                  })}
              </div>
            )}
          </div>

          {/* Job-level logs (not tied to any specific step) */}
          {jobLevelLogs.length > 0 && (
            <div className="card overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-200">
                <h3 className="font-semibold text-gray-700">Job-Level Logs ({jobLevelLogs.length})</h3>
              </div>
              <div className="divide-y divide-gray-100">
                {jobLevelLogs.map((log) => (
                  <div key={log.id} className="px-4 py-2.5 flex items-start gap-2">
                    <span className={`shrink-0 rounded px-1.5 py-0.5 font-semibold text-xs ${levelClass(log.level)}`}>
                      {log.level}
                    </span>
                    <span className="flex-1 text-xs text-gray-700 font-mono">{log.message}</span>
                    <span className="text-gray-300 text-xs shrink-0">{fmtDate(log.createdAt)}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
