import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { RefreshCw, RotateCcw, XCircle, ScrollText } from 'lucide-react';
import { jobApi } from '../../api/endpoints';
import { useAuth } from '../../auth/AuthContext';
import { Permissions } from '../../auth/permissions';
import { useApi } from '../../hooks/useApi';
import { JobStatus } from '../../api/types';
import PageHeader from '../../components/PageHeader';
import Pagination from '../../components/Pagination';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import CancelJobModal from '../../components/CancelJobModal';
import { JobStatusBadge } from '../../components/StatusBadge';
import { fmtDate, elapsed, shortId } from '../../utils/format';

function canCancel(status: JobStatus) {
  return [JobStatus.Pending, JobStatus.Running, JobStatus.Retrying].includes(status);
}

export default function JobList() {
  const { hasPermission, user } = useAuth();
  const canRetry = hasPermission(Permissions.JobRetry);
  const canCancelJob = hasPermission(Permissions.JobCancel);
  const [searchParams] = useSearchParams();
  const instanceId = searchParams.get('instanceId') ?? undefined;
  const [page, setPage] = useState(1);
  const [actionId, setActionId] = useState<string | null>(null);
  const [cancelJobId, setCancelJobId] = useState<string | null>(null);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  const { data, loading, error, refetch } = useApi(
    () => jobApi.list(page, 20, instanceId),
    [page, instanceId],
  );

  async function handleRetry(id: string) {
    if (!confirm('Retry this job?')) return;
    setActionId(id);
    try { await jobApi.retry(id); refetch(); }
    catch (e: unknown) { alert(e instanceof Error ? e.message : 'Retry failed'); }
    finally { setActionId(null); }
  }

  function openCancelModal(id: string) {
    setCancelJobId(id);
    setCancelOpen(true);
  }

  function closeCancelModal() {
    if (actionId) return;
    setCancelOpen(false);
    setCancelJobId(null);
  }

  async function handleCancelConfirm(reason: string) {
    if (!cancelJobId) return;
    setActionId(cancelJobId);
    try {
      await jobApi.cancel(cancelJobId, {
        reason: reason || undefined,
        cancelledBy: user?.username ?? 'User',
      });
      setCancelOpen(false);
      setCancelJobId(null);
      setToast('Cancellation requested.');
      window.setTimeout(() => setToast(null), 3500);
      refetch();
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : 'Cancel failed');
    } finally {
      setActionId(null);
    }
  }

  return (
    <div>
      {toast && (
        <div className="fixed bottom-5 right-5 z-50 rounded-lg bg-gray-900 text-white px-4 py-3 text-sm shadow-lg">
          {toast}
        </div>
      )}

      <CancelJobModal
        open={cancelOpen}
        onClose={closeCancelModal}
        onConfirm={handleCancelConfirm}
        loading={actionId === cancelJobId}
      />

      <PageHeader
        title="Jobs"
        subtitle={instanceId ? `Filtered by instance ${shortId(instanceId)}` : 'All jobs across all instances'}
        actions={
          <button onClick={refetch} className="btn btn-secondary" disabled={loading}>
            <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
          </button>
        }
      />

      <div className="card overflow-hidden">
        {loading && <LoadingSpinner />}
        {error && <ErrorAlert message={error} onRetry={refetch} />}
        {!loading && !error && data && (
          <>
            <table className="w-full">
              <thead>
                <tr>
                  <th className="table-th">Job ID</th>
                  <th className="table-th">Instance</th>
                  <th className="table-th">Status</th>
                  <th className="table-th">Attempts</th>
                  <th className="table-th">Started</th>
                  <th className="table-th">Duration</th>
                  <th className="table-th">Error</th>
                  <th className="table-th">Actions</th>
                </tr>
              </thead>
              <tbody>
                {data.items.length === 0 && (
                  <tr>
                    <td colSpan={8} className="table-td text-center text-gray-400 py-12">
                      No jobs found
                    </td>
                  </tr>
                )}
                {data.items.map((job) => (
                  <tr key={job.id} className="table-tr">
                    <td className="table-td">
                      <Link
                        to={`/jobs/${job.id}`}
                        className="font-mono text-xs text-blue-600 hover:underline flex items-center gap-1"
                        title="View job detail"
                      >
                        <ScrollText size={11} />
                        {shortId(job.id)}
                      </Link>
                    </td>
                    <td className="table-td">
                      <Link
                        to={`/process-instances/${job.processInstanceId}`}
                        className="font-mono text-xs text-blue-600 hover:underline"
                      >
                        {shortId(job.processInstanceId)}
                      </Link>
                    </td>
                    <td className="table-td"><JobStatusBadge status={job.status} /></td>
                    <td className="table-td text-gray-500 text-center">
                      {job.attemptCount}/{job.maxAttempts}
                    </td>
                    <td className="table-td text-gray-500">{fmtDate(job.startedAt)}</td>
                    <td className="table-td text-gray-500">{elapsed(job.startedAt, job.completedAt)}</td>
                    <td className="table-td max-w-[200px]">
                      {job.errorMessage && (
                        <span className="text-xs text-red-500 truncate block" title={job.errorMessage}>
                          {job.errorMessage}
                        </span>
                      )}
                    </td>
                    <td className="table-td">
                      <div className="flex gap-1">
                        {canRetry && job.status === JobStatus.Failed && (
                          <button
                            onClick={() => handleRetry(job.id)}
                            disabled={actionId === job.id}
                            className="btn btn-primary btn-sm"
                          >
                            <RotateCcw size={12} />
                            Retry
                          </button>
                        )}
                        {canCancelJob && canCancel(job.status) && (
                          <button
                            onClick={() => openCancelModal(job.id)}
                            disabled={actionId === job.id}
                            className="btn btn-danger btn-sm"
                          >
                            <XCircle size={12} />
                            Cancel
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination
              page={data.page}
              totalPages={data.totalPages}
              totalCount={data.totalCount}
              pageSize={data.pageSize}
              onPage={setPage}
            />
          </>
        )}
      </div>
    </div>
  );
}
