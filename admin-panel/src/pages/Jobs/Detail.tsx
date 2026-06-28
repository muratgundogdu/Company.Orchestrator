import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, RefreshCw, ScrollText, XCircle } from 'lucide-react';
import { jobApi } from '../../api/endpoints';
import { useAuth } from '../../auth/AuthContext';
import { Permissions } from '../../auth/permissions';
import { JobStatus } from '../../api/types';
import type { JobDto } from '../../api/types';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import CancelJobModal from '../../components/CancelJobModal';
import { JobStatusBadge } from '../../components/StatusBadge';
import { fmtDate, elapsed, shortId } from '../../utils/format';

function Toast({ message, onDone }: { message: string; onDone: () => void }) {
  useEffect(() => {
    const t = window.setTimeout(onDone, 3500);
    return () => window.clearTimeout(t);
  }, [onDone]);

  return (
    <div className="fixed bottom-5 right-5 z-50 rounded-lg bg-gray-900 text-white px-4 py-3 text-sm shadow-lg">
      {message}
    </div>
  );
}

function DetailRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-1 py-3 border-b border-gray-100 last:border-0">
      <dt className="text-sm text-gray-500 shrink-0">{label}</dt>
      <dd className="text-sm font-medium text-gray-900 sm:text-right break-all">{value ?? '—'}</dd>
    </div>
  );
}

function canCancel(status: JobStatus) {
  return [JobStatus.Pending, JobStatus.Running, JobStatus.Retrying, JobStatus.Cancelling].includes(status);
}

export default function JobDetail() {
  const { hasPermission, user } = useAuth();
  const canCancelJob = hasPermission(Permissions.JobCancel);
  const { id = '' } = useParams();
  const [job, setJob] = useState<JobDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const res = await jobApi.getById(id);
      setJob(res.data);
    } catch (e) {
      setJob(null);
      setError(e instanceof Error ? e.message : 'Failed to load job');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { load(); }, [load]);

  async function handleCancelConfirm(reason: string) {
    if (!job) return;
    setCancelling(true);
    try {
      const res = await jobApi.cancel(job.id, {
        reason: reason || undefined,
        cancelledBy: user?.username ?? 'User',
      });
      setCancelOpen(false);
      setToast('Cancellation requested.');
      setJob({ ...job, status: res.data.status === 'Cancelled' ? JobStatus.Cancelled : JobStatus.Cancelling });
      await load();
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Cancel failed');
    } finally {
      setCancelling(false);
    }
  }

  if (loading && !job) return <LoadingSpinner />;
  if (error) return <ErrorAlert message={error} onRetry={load} />;
  if (!job) return null;

  return (
    <div>
      {toast && <Toast message={toast} onDone={() => setToast(null)} />}

      <CancelJobModal
        open={cancelOpen}
        onClose={() => setCancelOpen(false)}
        onConfirm={handleCancelConfirm}
        loading={cancelling}
      />

      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <Link to="/jobs" className="btn btn-secondary !px-2">
            <ArrowLeft size={16} />
          </Link>
          <div>
            <h1 className="text-2xl font-bold text-gray-900 font-mono">{shortId(job.id)}</h1>
            <p className="text-sm text-gray-500 mt-0.5">Job detail</p>
          </div>
        </div>
        <div className="flex gap-2">
          <Link to={`/logs?jobId=${job.id}`} className="btn btn-secondary">
            <ScrollText size={14} /> View Logs
          </Link>
          {canCancelJob && canCancel(job.status) && job.status !== JobStatus.Cancelling && (
            <button
              onClick={() => setCancelOpen(true)}
              disabled={cancelling}
              className="btn btn-danger"
            >
              <XCircle size={14} /> Cancel
            </button>
          )}
          <button onClick={load} className="btn btn-secondary" disabled={loading}>
            <RefreshCw size={14} className={loading ? 'animate-spin' : ''} />
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
        <div className="card p-6">
          <div className="flex items-center justify-between mb-4">
            <h2 className="font-semibold text-gray-800">Status</h2>
            <JobStatusBadge status={job.status} />
          </div>
          <dl>
            <DetailRow label="Instance" value={
              <Link to={`/process-instances/${job.processInstanceId}`} className="text-blue-600 hover:underline font-mono text-xs">
                {shortId(job.processInstanceId)}
              </Link>
            } />
            <DetailRow label="Attempts" value={`${job.attemptCount}/${job.maxAttempts}`} />
            <DetailRow label="Started" value={fmtDate(job.startedAt)} />
            <DetailRow label="Completed" value={fmtDate(job.completedAt)} />
            <DetailRow label="Duration" value={elapsed(job.startedAt, job.completedAt)} />
            {job.errorMessage && (
              <DetailRow label="Error" value={<span className="text-red-600">{job.errorMessage}</span>} />
            )}
          </dl>
        </div>

        <div className="card p-6">
          <h2 className="font-semibold text-gray-800 mb-4">Cancellation</h2>
          <dl>
            <DetailRow label="Cancel Requested At" value={fmtDate(job.cancelRequestedAtUtc)} />
            <DetailRow label="Cancelled At" value={fmtDate(job.cancelledAtUtc)} />
            <DetailRow label="Cancelled By" value={job.cancelledBy} />
            <DetailRow label="Cancel Reason" value={job.cancelReason} />
          </dl>
        </div>
      </div>
    </div>
  );
}
