import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Briefcase,
  CheckCircle,
  AlertCircle,
  Play,
  XCircle,
  Clock,
  RefreshCw,
  Server,
  TrendingUp,
  Ban,
} from 'lucide-react';
import { dashboardApi } from '../api/endpoints';
import type { DashboardKpiDto, ThroughputHourDto } from '../api/types';
import ErrorAlert from '../components/ErrorAlert';
import LoadingSpinner from '../components/LoadingSpinner';
import { fmtDate } from '../utils/format';

type RangePreset = '1h' | '24h' | '7d' | '30d' | 'custom';

const REFRESH_INTERVAL_MS = 60_000;

interface StatCardProps {
  label: string;
  value: number | string;
  icon: React.ReactNode;
  color: string;
  to?: string;
  sub?: string;
}

function StatCard({ label, value, icon, color, to, sub }: StatCardProps) {
  const inner = (
    <div className={`card p-4 flex items-center gap-3 ${to ? 'hover:shadow-md transition-shadow' : ''}`}>
      <div className={`flex h-10 w-10 items-center justify-center rounded-lg ${color} shrink-0`}>
        {icon}
      </div>
      <div className="min-w-0">
        <p className="text-xl font-bold text-gray-900">{value}</p>
        <p className="text-xs text-gray-500 mt-0.5">{label}</p>
        {sub && <p className="text-[11px] text-gray-400 mt-0.5">{sub}</p>}
      </div>
    </div>
  );
  return to ? <Link to={to}>{inner}</Link> : inner;
}

function fmtDuration(seconds: number): string {
  if (seconds <= 0) return '—';
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  const mins = Math.floor(seconds / 60);
  const secs = Math.round(seconds % 60);
  return `${mins}m ${secs}s`;
}

function toIso(d: Date): string {
  return d.toISOString();
}

function toLocalInputValue(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function parseLocalInput(value: string): Date {
  return new Date(value);
}

function ThroughputChart({ data }: { data: ThroughputHourDto[] }) {
  const max = useMemo(
    () => Math.max(1, ...data.map((d) => d.succeeded + d.failed + d.cancelled)),
    [data],
  );

  if (data.length === 0) {
    return <p className="text-sm text-gray-400 text-center py-8">No throughput data</p>;
  }

  const showLabels = data.length <= 48;

  return (
    <div className="overflow-x-auto">
      <div className="flex items-end gap-1 min-h-[160px] pb-6 pt-2" style={{ minWidth: Math.max(data.length * 28, 320) }}>
        {data.map((bucket) => {
          const total = bucket.succeeded + bucket.failed + bucket.cancelled;
          const heightPct = (total / max) * 100;
          const succeededPct = total > 0 ? (bucket.succeeded / total) * 100 : 0;
          const failedPct = total > 0 ? (bucket.failed / total) * 100 : 0;
          const cancelledPct = total > 0 ? (bucket.cancelled / total) * 100 : 0;

          return (
            <div key={bucket.hourUtc} className="flex flex-col items-center flex-1 min-w-[20px] max-w-[36px]">
              <div
                className="w-full flex flex-col-reverse rounded-t overflow-hidden bg-gray-100"
                style={{ height: `${Math.max(heightPct, total > 0 ? 4 : 0)}%`, minHeight: total > 0 ? 4 : 0, maxHeight: 120 }}
                title={`${fmtDate(bucket.hourUtc)} — OK: ${bucket.succeeded}, Failed: ${bucket.failed}, Cancelled: ${bucket.cancelled}`}
              >
                {bucket.succeeded > 0 && (
                  <div className="bg-green-500 w-full" style={{ height: `${succeededPct}%` }} />
                )}
                {bucket.failed > 0 && (
                  <div className="bg-red-500 w-full" style={{ height: `${failedPct}%` }} />
                )}
                {bucket.cancelled > 0 && (
                  <div className="bg-amber-400 w-full" style={{ height: `${cancelledPct}%` }} />
                )}
              </div>
              {showLabels && (
                <span className="text-[9px] text-gray-400 mt-1 -rotate-45 origin-top-left whitespace-nowrap">
                  {new Date(bucket.hourUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                </span>
              )}
            </div>
          );
        })}
      </div>
      <div className="flex gap-4 text-xs text-gray-500 mt-2">
        <span className="flex items-center gap-1"><span className="h-2 w-2 rounded-sm bg-green-500" /> Succeeded</span>
        <span className="flex items-center gap-1"><span className="h-2 w-2 rounded-sm bg-red-500" /> Failed</span>
        <span className="flex items-center gap-1"><span className="h-2 w-2 rounded-sm bg-amber-400" /> Cancelled</span>
      </div>
    </div>
  );
}

export default function Dashboard() {
  const [preset, setPreset] = useState<RangePreset>('24h');
  const [customFrom, setCustomFrom] = useState(() => toLocalInputValue(new Date(Date.now() - 24 * 3600_000)));
  const [customTo, setCustomTo] = useState(() => toLocalInputValue(new Date()));
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [kpi, setKpi] = useState<DashboardKpiDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastRefresh, setLastRefresh] = useState<Date | null>(null);

  const range = useMemo(() => {
    const to = preset === 'custom' ? parseLocalInput(customTo) : new Date();
    let from: Date;
    switch (preset) {
      case '1h': from = new Date(to.getTime() - 3600_000); break;
      case '7d': from = new Date(to.getTime() - 7 * 24 * 3600_000); break;
      case '30d': from = new Date(to.getTime() - 30 * 24 * 3600_000); break;
      case 'custom': from = parseLocalInput(customFrom); break;
      default: from = new Date(to.getTime() - 24 * 3600_000);
    }
    return { fromUtc: toIso(from), toUtc: toIso(to) };
  }, [preset, customFrom, customTo]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await dashboardApi.kpi(range.fromUtc, range.toUtc);
      setKpi(res.data);
      setLastRefresh(new Date());
    } catch (e) {
      setKpi(null);
      setError(e instanceof Error ? e.message : 'Failed to load dashboard KPIs');
    } finally {
      setLoading(false);
    }
  }, [range.fromUtc, range.toUtc]);

  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    if (!autoRefresh) return undefined;
    const timer = window.setInterval(load, REFRESH_INTERVAL_MS);
    return () => window.clearInterval(timer);
  }, [autoRefresh, load]);

  const noActivity = kpi && kpi.jobs.total === 0;

  return (
    <div>
      {/* Header + filters */}
      <div className="flex flex-col lg:flex-row lg:items-start lg:justify-between gap-4 mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Dashboard</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            {lastRefresh ? `Last refreshed: ${fmtDate(lastRefresh.toISOString())}` : 'Loading…'}
            {kpi && (
              <span className="ml-2 text-gray-400">
                · {fmtDate(kpi.range.fromUtc)} → {fmtDate(kpi.range.toUtc)}
              </span>
            )}
          </p>
        </div>

        <div className="flex flex-wrap items-end gap-2">
          <div className="flex rounded-lg border border-gray-200 overflow-hidden">
            {(['1h', '24h', '7d', '30d', 'custom'] as RangePreset[]).map((p) => (
              <button
                key={p}
                onClick={() => setPreset(p)}
                className={`px-3 py-1.5 text-xs font-medium transition-colors ${
                  preset === p
                    ? 'bg-brand-primary text-white'
                    : 'bg-white text-gray-600 hover:bg-gray-50'
                }`}
              >
                {p === '1h' ? '1h' : p === '24h' ? '24h' : p === '7d' ? '7d' : p === '30d' ? '30d' : 'Custom'}
              </button>
            ))}
          </div>

          {preset === 'custom' && (
            <>
              <input
                type="datetime-local"
                className="input text-xs py-1.5"
                value={customFrom}
                onChange={(e) => setCustomFrom(e.target.value)}
              />
              <input
                type="datetime-local"
                className="input text-xs py-1.5"
                value={customTo}
                onChange={(e) => setCustomTo(e.target.value)}
              />
            </>
          )}

          <label className="flex items-center gap-1.5 text-xs text-gray-600 px-2">
            <input type="checkbox" checked={autoRefresh} onChange={(e) => setAutoRefresh(e.target.checked)} />
            Auto 60s
          </label>

          <button onClick={load} disabled={loading} className="btn btn-secondary btn-sm">
            <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
          </button>
        </div>
      </div>

      {error && <ErrorAlert message={error} onRetry={load} />}

      {loading && !kpi && <LoadingSpinner />}

      {kpi && (
        <>
          {noActivity && (
            <div className="card p-6 mb-6 text-center text-gray-500 text-sm">
              No job activity found for this time range.
            </div>
          )}

          {/* KPI cards */}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-4 mb-6">
            <StatCard
              label="Total Jobs"
              value={kpi.jobs.total}
              icon={<Briefcase size={18} className="text-brand-primary" />}
              color="bg-blue-50"
              to="/jobs"
            />
            <StatCard
              label="Success Rate"
              value={`${kpi.jobs.successRate}%`}
              sub={`${kpi.jobs.succeeded} succeeded`}
              icon={<TrendingUp size={18} className="text-green-600" />}
              color="bg-green-50"
            />
            <StatCard
              label="Failed Jobs"
              value={kpi.jobs.failed}
              sub={`${kpi.jobs.failureRate}% failure rate`}
              icon={<AlertCircle size={18} className="text-red-600" />}
              color="bg-red-50"
              to="/jobs"
            />
            <StatCard
              label="Running Jobs"
              value={kpi.jobs.running}
              icon={<Play size={18} className="text-indigo-600" />}
              color="bg-indigo-50"
              to="/jobs"
            />
            <StatCard
              label="Cancelled Jobs"
              value={kpi.jobs.cancelled}
              icon={<Ban size={18} className="text-amber-600" />}
              color="bg-amber-50"
              to="/jobs"
            />
            <StatCard
              label="Avg Duration"
              value={fmtDuration(kpi.jobs.averageDurationSeconds)}
              icon={<Clock size={18} className="text-gray-600" />}
              color="bg-gray-100"
            />
            <StatCard
              label="Workers Online"
              value={kpi.workers.online}
              sub={kpi.workers.warning > 0 ? `${kpi.workers.warning} warning` : undefined}
              icon={<Server size={18} className="text-green-600" />}
              color="bg-green-50"
              to="/workers"
            />
            <StatCard
              label="Workers Offline"
              value={kpi.workers.offline}
              sub={`${kpi.workers.runningJobs} jobs running`}
              icon={<Server size={18} className="text-red-600" />}
              color="bg-red-50"
              to="/workers"
            />
          </div>

          {/* Throughput chart */}
          <div className="card p-5 mb-6">
            <h2 className="font-semibold text-gray-800 mb-4">Throughput by Hour</h2>
            <ThroughputChart data={kpi.throughputByHour} />
          </div>

          <div className="grid grid-cols-1 xl:grid-cols-2 gap-6 mb-6">
            {/* Top workflows */}
            <div className="card overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-200">
                <h2 className="font-semibold text-gray-800">Top Workflows</h2>
              </div>
              <table className="w-full">
                <thead>
                  <tr>
                    <th className="table-th">Workflow</th>
                    <th className="table-th text-right">Runs</th>
                    <th className="table-th text-right">OK</th>
                    <th className="table-th text-right">Failed</th>
                    <th className="table-th text-right">Avg</th>
                  </tr>
                </thead>
                <tbody>
                  {kpi.topWorkflows.length === 0 && (
                    <tr><td colSpan={5} className="table-td text-center text-gray-400 py-8">No workflows</td></tr>
                  )}
                  {kpi.topWorkflows.map((w) => (
                    <tr key={w.processDefinitionId} className="table-tr">
                      <td className="table-td">
                        <Link to={`/process-definitions/${w.processDefinitionId}`} className="text-blue-600 hover:underline font-medium text-sm">
                          {w.name}
                        </Link>
                      </td>
                      <td className="table-td text-right text-gray-600">{w.runCount}</td>
                      <td className="table-td text-right text-green-600">{w.successCount}</td>
                      <td className="table-td text-right text-red-600">{w.failedCount}</td>
                      <td className="table-td text-right text-gray-500 text-xs">{fmtDuration(w.averageDurationSeconds)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Failing workflows */}
            <div className="card overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-200">
                <h2 className="font-semibold text-gray-800">Most Failing Workflows</h2>
              </div>
              <table className="w-full">
                <thead>
                  <tr>
                    <th className="table-th">Workflow</th>
                    <th className="table-th text-right">Failed</th>
                    <th className="table-th">Last Failed</th>
                  </tr>
                </thead>
                <tbody>
                  {kpi.failingWorkflows.length === 0 && (
                    <tr><td colSpan={3} className="table-td text-center text-gray-400 py-8">No failures</td></tr>
                  )}
                  {kpi.failingWorkflows.map((w) => (
                    <tr key={w.processDefinitionId} className="table-tr">
                      <td className="table-td">
                        <Link to={`/process-definitions/${w.processDefinitionId}`} className="text-blue-600 hover:underline font-medium text-sm">
                          {w.name}
                        </Link>
                      </td>
                      <td className="table-td text-right text-red-600 font-medium">{w.failedCount}</td>
                      <td className="table-td text-gray-500 text-xs">{fmtDate(w.lastFailedAtUtc)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Recent failures */}
          <div className="card overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-200 flex items-center justify-between">
              <h2 className="font-semibold text-gray-800 flex items-center gap-2">
                <XCircle size={16} className="text-red-500" /> Recent Failures
              </h2>
              <Link to="/jobs" className="text-xs text-blue-600 hover:underline">View all jobs</Link>
            </div>
            <table className="w-full">
              <thead>
                <tr>
                  <th className="table-th">Time</th>
                  <th className="table-th">Workflow</th>
                  <th className="table-th">Error</th>
                  <th className="table-th text-right">Links</th>
                </tr>
              </thead>
              <tbody>
                {kpi.recentFailures.length === 0 && (
                  <tr><td colSpan={4} className="table-td text-center text-gray-400 py-8">No recent failures</td></tr>
                )}
                {kpi.recentFailures.map((f) => (
                  <tr key={f.jobId} className="table-tr">
                    <td className="table-td text-gray-500 text-xs whitespace-nowrap">{fmtDate(f.failedAtUtc)}</td>
                    <td className="table-td font-medium text-sm">{f.processName}</td>
                    <td className="table-td text-red-600 text-xs max-w-md truncate" title={f.error ?? undefined}>
                      {f.error ?? '—'}
                    </td>
                    <td className="table-td text-right whitespace-nowrap">
                      <Link to={`/jobs/${f.jobId}`} className="text-xs text-blue-600 hover:underline mr-2">Job</Link>
                      <Link to={`/process-instances/${f.processInstanceId}`} className="text-xs text-blue-600 hover:underline">Instance</Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Instance summary */}
          <div className="mt-6 grid grid-cols-2 sm:grid-cols-4 gap-3">
            <StatCard
              label="Instances"
              value={kpi.instances.total}
              icon={<CheckCircle size={16} className="text-gray-600" />}
              color="bg-gray-50"
              to="/process-instances"
            />
            <StatCard
              label="Instances Succeeded"
              value={kpi.instances.succeeded}
              icon={<CheckCircle size={16} className="text-green-600" />}
              color="bg-green-50"
            />
            <StatCard
              label="Instances Failed"
              value={kpi.instances.failed}
              icon={<AlertCircle size={16} className="text-red-600" />}
              color="bg-red-50"
            />
            <StatCard
              label="Instances Cancelled"
              value={kpi.instances.cancelled}
              icon={<Ban size={16} className="text-amber-600" />}
              color="bg-amber-50"
            />
          </div>
        </>
      )}
    </div>
  );
}
