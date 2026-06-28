import { useState } from 'react';
import { Plus, Pencil, Trash2, RefreshCw } from 'lucide-react';
import { userApi } from '../../api/endpoints';
import type { UserDto } from '../../api/types';
import { useApi } from '../../hooks/useApi';
import PageHeader from '../../components/PageHeader';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import { fmtDate } from '../../utils/format';
import UserModal from './UserModal';

export default function UserList() {
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<UserDto | null>(null);

  const { data, loading, error, refetch } = useApi(() => userApi.list(), []);

  async function handleDelete(user: UserDto) {
    if (!window.confirm(`Delete user "${user.username}"?`)) return;
    await userApi.delete(user.id);
    refetch();
  }

  function openCreate() {
    setEditing(null);
    setModalOpen(true);
  }

  function openEdit(user: UserDto) {
    setEditing(user);
    setModalOpen(true);
  }

  return (
    <div>
      <PageHeader
        title="Users"
        subtitle="Manage user accounts and role assignments"
        actions={
          <div className="flex gap-2">
            <button onClick={refetch} className="btn btn-secondary" disabled={loading}>
              <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
            </button>
            <button onClick={openCreate} className="btn btn-primary">
              <Plus size={14} /> New User
            </button>
          </div>
        }
      />

      <div className="card overflow-hidden">
        {loading && <LoadingSpinner />}
        {error && <ErrorAlert message={error} onRetry={refetch} />}
        {!loading && !error && data && (
          <table className="w-full">
            <thead>
              <tr>
                <th className="table-th">Username</th>
                <th className="table-th">Display Name</th>
                <th className="table-th">Email</th>
                <th className="table-th">Roles</th>
                <th className="table-th">Status</th>
                <th className="table-th text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {data.length === 0 && (
                <tr>
                  <td colSpan={6} className="table-td text-center text-gray-400 py-12">
                    No users found
                  </td>
                </tr>
              )}
              {data.map((u) => (
                <tr key={u.id} className="table-tr">
                  <td className="table-td font-medium">{u.username}</td>
                  <td className="table-td">{u.displayName}</td>
                  <td className="table-td text-gray-600">{u.email}</td>
                  <td className="table-td">
                    <div className="flex flex-wrap gap-1">
                      {u.roles.map((r) => (
                        <span key={r} className="badge badge-gray text-xs">{r}</span>
                      ))}
                    </div>
                  </td>
                  <td className="table-td">
                    <span className={`badge ${u.isActive ? 'badge-green' : 'badge-gray'}`}>
                      {u.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td className="table-td text-right">
                    <div className="flex justify-end gap-1">
                      <button onClick={() => openEdit(u)} className="btn btn-ghost btn-sm" title="Edit">
                        <Pencil size={14} />
                      </button>
                      <button onClick={() => handleDelete(u)} className="btn btn-ghost btn-sm text-red-600" title="Delete">
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {modalOpen && (
        <UserModal
          user={editing}
          onClose={() => setModalOpen(false)}
          onSaved={() => { setModalOpen(false); refetch(); }}
        />
      )}
    </div>
  );
}
