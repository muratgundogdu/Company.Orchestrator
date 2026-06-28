import { FormEvent, useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { userApi } from '../../api/endpoints';
import type { UserDto } from '../../api/types';
import { ApiError } from '../../api/client';

interface UserModalProps {
  user: UserDto | null;
  onClose: () => void;
  onSaved: () => void;
}

const ALL_ROLES = ['Admin', 'Developer', 'Operator', 'Viewer'];

export default function UserModal({ user, onClose, onSaved }: UserModalProps) {
  const isEdit = user !== null;
  const [username, setUsername] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [roles, setRoles] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setUsername(user?.username ?? '');
    setDisplayName(user?.displayName ?? '');
    setEmail(user?.email ?? '');
    setPassword('');
    setIsActive(user?.isActive ?? true);
    setRoles(user?.roles ?? []);
    setError(null);
  }, [user]);

  function toggleRole(role: string) {
    setRoles((prev) =>
      prev.includes(role) ? prev.filter((r) => r !== role) : [...prev, role],
    );
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      if (isEdit && user) {
        await userApi.update(user.id, {
          displayName,
          email,
          password: password.trim() || undefined,
          isActive,
        });
        await userApi.assignRoles(user.id, { roles });
      } else {
        await userApi.create({
          username,
          displayName,
          email,
          password,
          isActive,
          roles,
        });
      }
      onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to save user');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="card w-full max-w-lg shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-5 py-4">
          <h2 className="text-lg font-semibold">{isEdit ? 'Edit User' : 'New User'}</h2>
          <button onClick={onClose} className="btn btn-ghost btn-sm"><X size={16} /></button>
        </div>

        <form onSubmit={handleSubmit} className="p-5 space-y-4">
          {error && (
            <div className="rounded-md bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">
              {error}
            </div>
          )}

          {!isEdit && (
            <div>
              <label className="block text-sm font-medium mb-1">Username</label>
              <input className="input w-full" value={username} onChange={(e) => setUsername(e.target.value)} required />
            </div>
          )}

          <div>
            <label className="block text-sm font-medium mb-1">Display Name</label>
            <input className="input w-full" value={displayName} onChange={(e) => setDisplayName(e.target.value)} required />
          </div>

          <div>
            <label className="block text-sm font-medium mb-1">Email</label>
            <input type="email" className="input w-full" value={email} onChange={(e) => setEmail(e.target.value)} required />
          </div>

          <div>
            <label className="block text-sm font-medium mb-1">
              {isEdit ? 'New Password (optional)' : 'Password'}
            </label>
            <input
              type="password"
              className="input w-full"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required={!isEdit}
            />
          </div>

          <div>
            <label className="block text-sm font-medium mb-2">Roles</label>
            <div className="flex flex-wrap gap-2">
              {ALL_ROLES.map((role) => (
                <label key={role} className="flex items-center gap-1.5 text-sm">
                  <input
                    type="checkbox"
                    checked={roles.includes(role)}
                    onChange={() => toggleRole(role)}
                  />
                  {role}
                </label>
              ))}
            </div>
          </div>

          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
            Active
          </label>

          <div className="flex justify-end gap-2 pt-2">
            <button type="button" onClick={onClose} className="btn btn-secondary">Cancel</button>
            <button type="submit" className="btn btn-primary" disabled={saving}>
              {saving ? 'Saving…' : 'Save'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
