import { useState } from 'react';
import { Plus, Pencil, Trash2, RefreshCw } from 'lucide-react';
import { credentialApi } from '../../api/endpoints';
import type { CredentialDto } from '../../api/types';
import { useApi } from '../../hooks/useApi';
import PageHeader from '../../components/PageHeader';
import Pagination from '../../components/Pagination';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import { fmtDate } from '../../utils/format';
import CredentialModal from './CredentialModal';

export default function CredentialList() {
  const [page, setPage] = useState(1);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<CredentialDto | null>(null);

  const { data, loading, error, refetch } = useApi(
    () => credentialApi.list(page, 20),
    [page],
  );

  async function handleDelete(credential: CredentialDto) {
    if (!window.confirm(`Delete credential "${credential.name}"?`)) return;
    await credentialApi.delete(credential.id);
    refetch();
  }

  function openCreate() {
    setEditing(null);
    setModalOpen(true);
  }

  function openEdit(credential: CredentialDto) {
    setEditing(credential);
    setModalOpen(true);
  }

  return (
    <div>
      <PageHeader
        title="Credentials"
        subtitle="Secure vault for connection strings, API tokens, and other secrets"
        actions={
          <div className="flex gap-2">
            <button onClick={refetch} className="btn btn-secondary" disabled={loading}>
              <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
            </button>
            <button onClick={openCreate} className="btn btn-primary">
              <Plus size={14} /> New Credential
            </button>
          </div>
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
                  <th className="table-th">Type</th>
                  <th className="table-th">Description</th>
                  <th className="table-th">Updated At</th>
                  <th className="table-th text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {data.items.length === 0 && (
                  <tr>
                    <td colSpan={5} className="table-td text-center text-gray-400 py-12">
                      No credentials found
                    </td>
                  </tr>
                )}
                {data.items.map((c) => (
                  <tr key={c.id} className="table-tr">
                    <td className="table-td font-medium text-gray-900">{c.name}</td>
                    <td className="table-td">
                      <span className="text-xs font-mono bg-gray-100 px-1.5 py-0.5 rounded">{c.type}</span>
                    </td>
                    <td className="table-td text-gray-500">{c.description ?? '—'}</td>
                    <td className="table-td text-gray-500">{fmtDate(c.updatedAt ?? c.createdAt)}</td>
                    <td className="table-td">
                      <div className="flex justify-end gap-2">
                        <button
                          type="button"
                          onClick={() => openEdit(c)}
                          className="btn btn-secondary btn-sm"
                          title="Edit"
                        >
                          <Pencil size={14} />
                        </button>
                        <button
                          type="button"
                          onClick={() => handleDelete(c)}
                          className="btn btn-secondary btn-sm text-red-600"
                          title="Delete"
                        >
                          <Trash2 size={14} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination
              page={page}
              totalPages={data.totalPages}
              totalCount={data.totalCount}
              pageSize={data.pageSize}
              onPage={setPage}
            />
          </>
        )}
      </div>

      <CredentialModal
        open={modalOpen}
        credential={editing}
        onClose={() => setModalOpen(false)}
        onSaved={refetch}
      />
    </div>
  );
}
