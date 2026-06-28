import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { RefreshCw, XCircle } from 'lucide-react';
import { processInstanceApi } from '../../api/endpoints';
import { useApi } from '../../hooks/useApi';
import PageHeader from '../../components/PageHeader';
import Pagination from '../../components/Pagination';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import { ProcessStatusBadge } from '../../components/StatusBadge';
import { fmtDate, elapsed } from '../../utils/format';

export default function ProcessInstanceList() {
  const [searchParams] = useSearchParams();
  const definitionId = searchParams.get('definitionId') ?? undefined;
  const [page, setPage] = useState(1);
  const [cancellingId, setCancellingId] = useState<string | null>(null);

  const { data, loading, error, refetch } = useApi(
    () => processInstanceApi.list(page, 20, definitionId),
    [page, definitionId],
  );

  async function handleCancel(id: string) {
    if (!confirm('Cancel this process instance?')) return;
    setCancellingId(id);
    try {
      await processInstanceApi.cancel(id);
      refetch();
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : 'Cancel failed');
    } finally {
      setCancellingId(null);
    }
  }

  return (
    <div>
      <PageHeader
        title="Process Instances"
        subtitle={definitionId ? `Filtered by definition: ${definitionId.slice(0, 8)}…` : 'All running and historical instances'}
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
                  <th className="table-th">Definition</th>
                  <th className="table-th">Ver</th>
                  <th className="table-th">Status</th>
                  <th className="table-th">Triggered By</th>
                  <th className="table-th">Started</th>
                  <th className="table-th">Duration</th>
                  <th className="table-th">Actions</th>
                </tr>
              </thead>
              <tbody>
                {data.items.length === 0 && (
                  <tr>
                    <td colSpan={7} className="table-td text-center text-gray-400 py-12">
                      No instances found
                    </td>
                  </tr>
                )}
                {data.items.map((inst) => (
                  <tr key={inst.id} className="table-tr">
                    <td className="table-td">
                      <Link
                        to={`/process-instances/${inst.id}`}
                        className="font-medium text-blue-600 hover:underline"
                      >
                        {inst.processDefinitionName}
                      </Link>
                      {inst.correlationId && (
                        <p className="text-xs text-gray-400 mt-0.5 font-mono">{inst.correlationId}</p>
                      )}
                    </td>
                    <td className="table-td text-gray-500">v{inst.versionNumber}</td>
                    <td className="table-td"><ProcessStatusBadge status={inst.status} /></td>
                    <td className="table-td text-gray-500 max-w-[120px] truncate">
                      {inst.triggeredBy ?? '—'}
                    </td>
                    <td className="table-td text-gray-500">{fmtDate(inst.startedAt)}</td>
                    <td className="table-td text-gray-500">{elapsed(inst.startedAt, inst.completedAt)}</td>
                    <td className="table-td">
                      <div className="flex gap-1">
                        <Link to={`/process-instances/${inst.id}`} className="btn btn-secondary btn-sm">
                          View
                        </Link>
                        {[0, 1].includes(inst.status) && (
                          <button
                            onClick={() => handleCancel(inst.id)}
                            disabled={cancellingId === inst.id}
                            className="btn btn-danger btn-sm"
                          >
                            <XCircle size={12} />
                            {cancellingId === inst.id ? '…' : 'Cancel'}
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
