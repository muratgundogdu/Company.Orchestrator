import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Plus, RefreshCw } from 'lucide-react';
import { processDefinitionApi } from '../../api/endpoints';
import { useAuth } from '../../auth/AuthContext';
import { Permissions } from '../../auth/permissions';
import { useApi } from '../../hooks/useApi';
import PageHeader from '../../components/PageHeader';
import Pagination from '../../components/Pagination';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import { ActiveBadge } from '../../components/StatusBadge';
import { fmtDate } from '../../utils/format';

export default function ProcessDefinitionList() {
  const { hasPermission } = useAuth();
  const canEdit = hasPermission(Permissions.WorkflowEdit);
  const [page, setPage] = useState(1);
  const navigate = useNavigate();

  const { data, loading, error, refetch } = useApi(
    () => processDefinitionApi.list(page, 20),
    [page],
  );

  return (
    <div>
      <PageHeader
        title="Process Definitions"
        subtitle="Manage workflow definitions and their versions"
        actions={
          <>
            <button onClick={refetch} className="btn btn-secondary" disabled={loading}>
              <RefreshCw size={14} className={loading ? 'animate-spin' : ''} />
              Refresh
            </button>
            {canEdit && (
              <button onClick={() => navigate('/process-definitions/new')} className="btn btn-primary">
                <Plus size={14} />
                New Definition
              </button>
            )}
          </>
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
                  <th className="table-th">Name</th>
                  <th className="table-th">Category</th>
                  <th className="table-th">Status</th>
                  <th className="table-th">Versions</th>
                  <th className="table-th">Latest</th>
                  <th className="table-th">Created</th>
                </tr>
              </thead>
              <tbody>
                {data.items.length === 0 && (
                  <tr>
                    <td colSpan={6} className="table-td text-center text-gray-400 py-12">
                      No process definitions found
                    </td>
                  </tr>
                )}
                {data.items.map((def) => (
                  <tr key={def.id} className="table-tr">
                    <td className="table-td">
                      <Link
                        to={`/process-definitions/${def.id}`}
                        className="font-medium text-blue-600 hover:underline"
                      >
                        {def.name}
                      </Link>
                      {def.description && (
                        <p className="text-xs text-gray-400 mt-0.5 max-w-xs truncate">{def.description}</p>
                      )}
                    </td>
                    <td className="table-td text-gray-500">{def.category ?? '—'}</td>
                    <td className="table-td"><ActiveBadge active={def.isActive} /></td>
                    <td className="table-td text-gray-500">{def.versionCount}</td>
                    <td className="table-td text-gray-500">
                      {def.latestVersionNumber != null ? `v${def.latestVersionNumber}` : '—'}
                    </td>
                    <td className="table-td text-gray-500">{fmtDate(def.createdAt)}</td>
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
