import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, RefreshCw, Server } from 'lucide-react';
import { workerApi } from '../../api/endpoints';
import type { WorkerDetailDto } from '../../api/types';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import { WorkerStatusBadge } from '../../components/StatusBadge';
import { fmtDate } from '../../utils/format';

const REFRESH_INTERVAL_MS = 30_000;

function DetailRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-1 py-3 border-b border-gray-100 last:border-0">
      <dt className="text-sm text-gray-500">{label}</dt>
      <dd className="text-sm font-medium text-gray-900 sm:text-right">{value}</dd>
    </div>
  );
}

function fmtMetric(value: number | null | undefined, suffix = '') {
  if (value === null || value === undefined) return '—';
  return `${value}${suffix}`;
}

export default function WorkerDetail() {
  const { workerId = '' } = useParams();
  const decodedId = decodeURIComponent(workerId);
  const [worker, setWorker] = useState<WorkerDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!decodedId) return;
    setLoading(true);
    setError(null);
    try {
      const res = await workerApi.getById(decodedId);
      setWorker(res.data);
    } catch (e) {
      setWorker(null);
      setError(e instanceof Error ? e.message : 'Failed to load worker');
    } finally {
      setLoading(false);
    }
  }, [decodedId]);

  useEffect(() => {
    load();
    const timer = window.setInterval(load, REFRESH_INTERVAL_MS);
    return () => window.clearInterval(timer);
  }, [load]);

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <Link to="/workers" className="btn btn-secondary !px-2">
            <ArrowLeft size={16} />
          </Link>
          <div>
            <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-2">
              <Server size={22} className="text-gray-400" />
              {worker?.workerName ?? decodedId}
            </h1>
            <p className="text-sm text-gray-500 font-mono mt-0.5">{decodedId}</p>
          </div>
        </div>
        <button onClick={load} className="btn btn-secondary" disabled={loading}>
          <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
        </button>
      </div>

      {loading && !worker && <LoadingSpinner />}
      {error && <ErrorAlert message={error} onRetry={load} />}

      {worker && (
        <div className="card p-6 max-w-2xl">
          <div className="flex items-center justify-between mb-4">
            <h2 className="font-semibold text-gray-800">Worker Details</h2>
            <WorkerStatusBadge status={worker.status} />
          </div>

          <dl>
            <DetailRow label="Worker Name" value={worker.workerName} />
            <DetailRow label="Machine" value={worker.machineName} />
            <DetailRow label="Version" value={<span className="font-mono">{worker.version}</span>} />
            <DetailRow label="Started At" value={fmtDate(worker.startedAtUtc)} />
            <DetailRow label="Process Id" value={worker.processId} />
            <DetailRow label="Last Seen" value={fmtDate(worker.lastHeartbeatUtc)} />
            <DetailRow label="Running Jobs" value={worker.runningJobCount} />
            <DetailRow label="CPU" value={fmtMetric(worker.cpuUsagePercent, '%')} />
            <DetailRow label="Memory" value={fmtMetric(worker.memoryUsageMb, ' MB')} />
            {worker.metadataJson && (
              <DetailRow
                label="Metadata"
                value={<pre className="text-xs font-mono whitespace-pre-wrap text-left">{worker.metadataJson}</pre>}
              />
            )}
          </dl>
        </div>
      )}
    </div>
  );
}
