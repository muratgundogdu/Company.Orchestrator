import { useEffect, useState } from 'react';
import { RefreshCw, Save } from 'lucide-react';
import { roleApi } from '../../api/endpoints';
import type { PermissionDto, RoleDto } from '../../api/types';
import PageHeader from '../../components/PageHeader';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';

export default function RoleList() {
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [permissions, setPermissions] = useState<PermissionDto[]>([]);
  const [selected, setSelected] = useState<Record<string, Set<string>>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [savingRoleId, setSavingRoleId] = useState<string | null>(null);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const [rolesRes, permsRes] = await Promise.all([
        roleApi.list(),
        roleApi.listPermissions(),
      ]);
      setRoles(rolesRes.data);
      setPermissions(permsRes.data);
      const map: Record<string, Set<string>> = {};
      for (const role of rolesRes.data) {
        map[role.id] = new Set(role.permissions);
      }
      setSelected(map);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load roles');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  function togglePermission(roleId: string, permission: string) {
    setSelected((prev) => {
      const next = new Set(prev[roleId] ?? []);
      if (next.has(permission)) next.delete(permission);
      else next.add(permission);
      return { ...prev, [roleId]: next };
    });
  }

  async function saveRole(role: RoleDto) {
    setSavingRoleId(role.id);
    try {
      const perms = Array.from(selected[role.id] ?? []);
      const res = await roleApi.updatePermissions(role.id, { permissions: perms });
      setRoles((prev) => prev.map((r) => (r.id === role.id ? res.data : r)));
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Failed to save role');
    } finally {
      setSavingRoleId(null);
    }
  }

  return (
    <div>
      <PageHeader
        title="Roles & Permissions"
        subtitle="Configure role permission assignments"
        actions={
          <button onClick={load} className="btn btn-secondary" disabled={loading}>
            <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
          </button>
        }
      />

      {loading && <LoadingSpinner />}
      {error && <ErrorAlert message={error} onRetry={load} />}

      {!loading && !error && (
        <div className="space-y-4">
          {roles.map((role) => (
            <div key={role.id} className="card p-5">
              <div className="flex items-center justify-between mb-4">
                <div>
                  <h3 className="font-semibold text-gray-900">{role.name}</h3>
                  {role.description && (
                    <p className="text-sm text-gray-500">{role.description}</p>
                  )}
                </div>
                <button
                  onClick={() => saveRole(role)}
                  className="btn btn-primary btn-sm"
                  disabled={savingRoleId === role.id}
                >
                  <Save size={14} /> {savingRoleId === role.id ? 'Saving…' : 'Save'}
                </button>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2">
                {permissions.map((perm) => (
                  <label key={perm.id} className="flex items-start gap-2 text-sm p-2 rounded hover:bg-gray-50">
                    <input
                      type="checkbox"
                      className="mt-0.5"
                      checked={selected[role.id]?.has(perm.name) ?? false}
                      onChange={() => togglePermission(role.id, perm.name)}
                      disabled={role.name === 'Admin'}
                    />
                    <span>
                      <span className="font-medium">{perm.name}</span>
                      {perm.description && (
                        <span className="block text-xs text-gray-500">{perm.description}</span>
                      )}
                    </span>
                  </label>
                ))}
              </div>
              {role.name === 'Admin' && (
                <p className="text-xs text-gray-400 mt-3">Admin role always retains all permissions.</p>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
