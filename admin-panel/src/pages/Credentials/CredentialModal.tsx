import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { credentialApi } from '../../api/endpoints';
import { CREDENTIAL_TYPES, type CredentialDto } from '../../api/types';
import { ApiError } from '../../api/client';

interface CredentialModalProps {
  open: boolean;
  credential: CredentialDto | null;
  onClose: () => void;
  onSaved: () => void;
}

export default function CredentialModal({ open, credential, onClose, onSaved }: CredentialModalProps) {
  const isEdit = credential !== null;
  const [name, setName] = useState('');
  const [type, setType] = useState<string>('BearerToken');
  const [description, setDescription] = useState('');
  const [secretValue, setSecretValue] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldError, setFieldError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setName(credential?.name ?? '');
    setType(credential?.type ?? 'BearerToken');
    setDescription(credential?.description ?? '');
    setSecretValue('');
    setError(null);
    setFieldError(null);
  }, [open, credential]);

  if (!open) return null;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);
    setFieldError(null);

    try {
      if (isEdit && credential) {
        await credentialApi.update(credential.id, {
          name,
          type,
          description: description || undefined,
          secretValue: secretValue.trim() ? secretValue : undefined,
          isActive: credential.isActive,
        });
      } else {
        if (!secretValue.trim()) {
          setFieldError('Secret value is required');
          setSaving(false);
          return;
        }
        await credentialApi.create({
          name,
          type,
          description: description || undefined,
          secretValue,
        });
      }
      onSaved();
      onClose();
    } catch (err) {
      if (err instanceof ApiError && err.field) {
        setFieldError(err.message);
      } else {
        setError(err instanceof Error ? err.message : 'Failed to save credential');
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="card w-full max-w-lg shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-5 py-4">
          <h2 className="text-lg font-semibold text-gray-900">
            {isEdit ? 'Edit Credential' : 'Create Credential'}
          </h2>
          <button type="button" onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="px-5 py-4 space-y-4">
          {error && <p className="text-sm text-red-600">{error}</p>}

          <div>
            <label className="label">Name *</label>
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="input w-full"
              placeholder="ReportingDb"
              required
            />
          </div>

          <div>
            <label className="label">Type *</label>
            <select value={type} onChange={(e) => setType(e.target.value)} className="input w-full">
              {CREDENTIAL_TYPES.map((t) => (
                <option key={t} value={t}>{t}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="label">Description</label>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              className="input w-full min-h-[70px]"
              placeholder="Optional description"
            />
          </div>

          <div>
            <label className="label">
              Secret Value {isEdit ? '' : '*'}
            </label>
            <input
              type="password"
              value={secretValue}
              onChange={(e) => setSecretValue(e.target.value)}
              className={`input w-full font-mono text-xs ${fieldError ? 'border-red-400' : ''}`}
              placeholder={isEdit ? 'Leave blank to keep existing secret' : 'Enter secret value'}
              autoComplete="new-password"
            />
            {fieldError && <p className="text-xs text-red-600 mt-1">⚠ {fieldError}</p>}
            {isEdit && !fieldError && (
              <p className="text-xs text-gray-400 mt-1">
                Secret values are write-only. Leave blank to keep the existing secret.
              </p>
            )}
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button type="button" onClick={onClose} className="btn btn-secondary" disabled={saving}>
              Cancel
            </button>
            <button type="submit" className="btn btn-primary" disabled={saving}>
              {saving ? 'Saving…' : isEdit ? 'Update' : 'Create'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
