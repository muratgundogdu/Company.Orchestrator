import { useState } from 'react';
import { Save } from 'lucide-react';
import { processDefinitionApi } from '../../api/endpoints';
import { ApiError } from '../../api/client';
import type { VersionStatus } from '../../api/types';

export interface SaveResult {
  definitionId: string;
  versionId: string;
  versionNumber: number;
  versionStatus: VersionStatus;
  definitionName: string;
}

interface SaveModalProps {
  workflowName: string;
  workflowDesc: string;
  /** Serialised workflow JSON ready to store as version content. */
  workflowJson: string;
  /** Null when saving a brand-new definition. */
  currentDefinitionId: string | null;
  onClose: () => void;
  onSaved: (result: SaveResult) => void;
}

export default function SaveModal({
  workflowName: initialName,
  workflowDesc: initialDesc,
  workflowJson,
  currentDefinitionId,
  onClose,
  onSaved,
}: SaveModalProps) {
  const isNew = !currentDefinitionId;

  const [name, setName]             = useState(initialName);
  const [description, setDescription] = useState(initialDesc);
  const [category, setCategory]     = useState('');
  const [changeNotes, setChangeNotes] = useState('');
  const [saving, setSaving]         = useState(false);
  const [error, setError]           = useState<string | null>(null);
  const [nameError, setNameError]   = useState<string | null>(null);

  async function handleSave() {
    if (!name.trim()) {
      setNameError('Name is required');
      setError(null);
      return;
    }
    setSaving(true);
    setError(null);
    setNameError(null);
    try {
      let defId = currentDefinitionId!;

      if (isNew) {
        const defRes = await processDefinitionApi.create({
          name: name.trim(),
          description: description.trim() || undefined,
          category: category.trim() || undefined,
        });
        defId = defRes.data.id;
      } else {
        // Sync name/description if changed
        await processDefinitionApi.update(currentDefinitionId!, {
          name: name.trim(),
          description: description.trim() || undefined,
        });
      }

      const vRes = await processDefinitionApi.createVersion(defId, {
        jsonDefinition: workflowJson,
        changeNotes: changeNotes.trim() || undefined,
      });

      onSaved({
        definitionId: defId,
        versionId: vRes.data.id,
        versionNumber: vRes.data.versionNumber,
        versionStatus: vRes.data.status,
        definitionName: name.trim(),
      });
    } catch (e: unknown) {
      if (e instanceof ApiError && e.field === 'name') {
        setNameError(e.message);
        setError(null);
      } else {
        setNameError(null);
        setError(e instanceof Error ? e.message : 'Save failed');
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="bg-white rounded-xl shadow-2xl w-[480px] flex flex-col overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <div>
            <h2 className="font-semibold text-gray-900">
              {isNew ? 'Save as New Definition' : 'Save New Version'}
            </h2>
            <p className="text-xs text-gray-400 mt-0.5">
              {isNew
                ? 'Creates a process definition and stores the first version.'
                : 'Appends a new version to the existing definition.'}
            </p>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-2xl leading-none">×</button>
        </div>

        {/* Body */}
        <div className="p-5 space-y-4">
          {error && (
            <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-3 py-2">
              {error}
            </div>
          )}

          <div>
            <label className="label">Name *</label>
            <input
              value={name}
              onChange={(e) => {
                setName(e.target.value);
                if (nameError) setNameError(null);
              }}
              className={`input${nameError ? ' border-red-400 focus:ring-red-400' : ''}`}
              placeholder="my-workflow"
            />
            {nameError && (
              <p className="text-xs text-red-600 mt-1">⚠ {nameError}</p>
            )}
          </div>

          {isNew && (
            <>
              <div>
                <label className="label">Description</label>
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  rows={2}
                  className="input resize-none"
                  placeholder="What does this workflow do?"
                />
              </div>

              <div>
                <label className="label">Category</label>
                <input
                  value={category}
                  onChange={(e) => setCategory(e.target.value)}
                  className="input"
                  placeholder="e.g. Reports, Mail, ETL"
                />
              </div>
            </>
          )}

          <div>
            <label className="label">Change Notes (optional)</label>
            <input
              value={changeNotes}
              onChange={(e) => setChangeNotes(e.target.value)}
              className="input"
              placeholder="What changed in this version…"
            />
          </div>
        </div>

        {/* Footer */}
        <div className="flex gap-2 justify-end px-5 py-3 border-t border-gray-200 bg-gray-50">
          <button onClick={onClose} className="btn btn-secondary">Cancel</button>
          <button onClick={handleSave} disabled={saving || !name.trim()} className="btn btn-primary">
            <Save size={13} />
            {saving ? 'Saving…' : isNew ? 'Create & Save' : 'Save New Version'}
          </button>
        </div>
      </div>
    </div>
  );
}
