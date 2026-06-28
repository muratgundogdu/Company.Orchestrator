import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { RefreshCw, Server } from 'lucide-react';
import { workerApi } from '../../api/endpoints';
import type { WorkerListItemDto } from '../../api/types';
import PageHeader from '../../components/PageHeader';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import { WorkerStatusBadge } from '../../components/StatusBadge';
import { fmtDate } from '../../utils/format';

const REFRESH_INTERVAL_MS = 30_000;

function fmtMetric(value: number | null | undefined, suffix = '') {
  if (value === null || value === undefined) return '—';
  return `${value}${suffix}`;
}

export default function WorkerList() {
  const [workers, setWorkers] = useState<WorkerListItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastRefresh, setLastRefresh] = useState<Date | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await workerApi.list();
      setWorkers(res.data);
      setLastRefresh(new Date());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load workers');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
    const timer = window.setInterval(load, REFRESH_INTERVAL_MS);
    return () => window.clearInterval(timer);
  }, [load]);

  return (
    <div>
      <PageHeader
        title="Workers"
        subtitle={
          lastRefresh
            ? `Auto-refreshes every 30s · Last updated ${fmtDate(lastRefresh.toISOString())}`
            : 'Monitor worker health and job load'
        }
        actions={
          <button onClick={load} className="btn btn-secondary" disabled={loading}>
            <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
          </button>
        }
      />

      <div className="card overflow-hidden">
        {loading && workers.length === 0 && <LoadingSpinner />}
        {error && <ErrorAlert message={error} onRetry={load} />}
        {!error && (
          <table className="w-full">
            <thead>
              <tr>
                <th className="table-th">Worker Name</th>
                <th className="table-th">Machine</th>
                <th className="table-th">Version</th>
                <th className="table-th">Status</th>
                <th className="table-th">Last Seen</th>
                <th className="table-th">Running Jobs</th>
                <th className="table-th">CPU %</th>
                <th className="table-th">Memory MB</th>
              </tr>
            </thead>
            <tbody>
              {!loading && workers.length === 0 && (
                <tr>
                  <td colSpan={8} className="table-td text-center text-gray-400 py-12">
                    <Server size={28} className="mx-auto mb-2 opacity-40" />
                    No workers registered yet. Start a worker process to see it here.
                  </td>
                </tr>
              )}
              {workers.map((worker) => (
                <tr key={worker.workerId} className="table-tr">
                  <td className="table-td">
                    <Link
                      to={`/workers/${encodeURIComponent(worker.workerId)}`}
                      className="font-medium text-blue-600 hover:underline"
                    >
                      {worker.workerName}
                    </Link>
                    <div className="text-xs text-gray-400 font-mono mt-0.5">{worker.workerId}</div>
                  </td>
                  <td className="table-td text-gray-600">{worker.machineName}</td>
                  <td className="table-td text-gray-500 font-mono text-xs">{worker.version}</td>
                  <td className="table-td"><WorkerStatusBadge status={worker.status} /></td>
                  <td className="table-td text-gray-500">{fmtDate(worker.lastHeartbeatUtc)}</td>
                  <td className="table-td text-gray-700 font-medium">{worker.runningJobCount}</td>
                  <td className="table-td text-gray-500">{fmtMetric(worker.cpuUsagePercent, '%')}</td>
                  <td className="table-td text-gray-500">{fmtMetric(worker.memoryUsageMb, ' MB')}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
