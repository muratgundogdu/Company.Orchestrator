import { useEffect, useState } from 'react';
import { FolderOpen, Copy, Trash2, RefreshCw, Search, GitBranch } from 'lucide-react';
import { processDefinitionApi } from '../../api/endpoints';
import type { ProcessDefinitionDto } from '../../api/types';
import { VersionStatus } from '../../api/types';
import { ActiveBadge } from '../../components/StatusBadge';
import { fmtDate } from '../../utils/format';

export interface OpenResult {
  definitionId: string;
  versionId: string;
  versionNumber: number;
  versionStatus: VersionStatus;
  versionJson: string;
  definitionName: string;
  definitionDesc: string;
}

interface OpenModalProps {
  currentDefinitionId: string | null;
  onClose: () => void;
  onOpen: (result: OpenResult) => void;
}

export default function OpenModal({ currentDefinitionId, onClose, onOpen }: OpenModalProps) {
  const [definitions, setDefinitions] = useState<ProcessDefinitionDto[]>([]);
  const [loading, setLoading]         = useState(true);
  const [search, setSearch]           = useState('');
  const [actionId, setActionId]       = useState<string | null>(null);
  const [error, setError]             = useState<string | null>(null);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const res = await processDefinitionApi.list(1, 100);
      setDefinitions(res.data.items);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to load definitions');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  const filtered = definitions.filter(
    (d) =>
      d.name.toLowerCase().includes(search.toLowerCase()) ||
      (d.category ?? '').toLowerCase().includes(search.toLowerCase()) ||
      (d.description ?? '').toLowerCase().includes(search.toLowerCase()),
  );

  async function handleOpen(def: ProcessDefinitionDto) {
    setActionId(def.id);
    try {
      const versionsRes = await processDefinitionApi.getVersions(def.id);
      const versions = [...versionsRes.data].sort((a, b) => b.versionNumber - a.versionNumber);

      if (versions.length === 0) {
        alert(`"${def.name}" has no versions yet.\nCreate a version first via the Process Definitions page.`);
        return;
      }
      const v = versions[0];
      onOpen({
        definitionId: def.id,
        versionId: v.id,
        versionNumber: v.versionNumber,
        versionStatus: v.status,
        versionJson: v.jsonDefinition,
        definitionName: def.name,
        definitionDesc: def.description ?? '',
      });
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : 'Failed to open definition');
    } finally {
      setActionId(null);
    }
  }

  async function handleClone(def: ProcessDefinitionDto) {
    setActionId(`${def.id}-clone`);
    try {
      const newDefRes = await processDefinitionApi.create({
        name: `${def.name} (copy)`,
        description: def.description ?? undefined,
        category: def.category ?? undefined,
      });
      if (def.versionCount > 0) {
        const versionsRes = await processDefinitionApi.getVersions(def.id);
        const sorted = [...versionsRes.data].sort((a, b) => b.versionNumber - a.versionNumber);
        if (sorted.length > 0) {
          await processDefinitionApi.createVersion(newDefRes.data.id, {
            jsonDefinition: sorted[0].jsonDefinition,
            changeNotes: `Cloned from "${def.name}" v${sorted[0].versionNumber}`,
          });
        }
      }
      await load();
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : 'Clone failed');
    } finally {
      setActionId(null);
    }
  }

  async function handleDelete(def: ProcessDefinitionDto) {
    if (!confirm(`Delete "${def.name}" and all its versions?\nThis cannot be undone.`)) return;
    setActionId(`${def.id}-delete`);
    try {
      await processDefinitionApi.delete(def.id);
      setDefinitions((ds) => ds.filter((d) => d.id !== def.id));
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : 'Delete failed');
    } finally {
      setActionId(null);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="bg-white rounded-xl shadow-2xl w-[760px] max-h-[82vh] flex flex-col overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <div className="flex items-center gap-2">
            <GitBranch size={18} className="text-brand-primary" />
            <h2 className="font-semibold text-gray-900">Open Workflow</h2>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-2xl leading-none">×</button>
        </div>

        {/* Search */}
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <div className="relative">
            <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="input pl-8"
              placeholder="Search by name, category or description…"
              autoFocus
            />
          </div>
        </div>

        {/* Table */}
        <div className="flex-1 overflow-y-auto">
          {loading ? (
            <div className="flex items-center justify-center py-16 text-gray-400 text-sm gap-2">
              <RefreshCw size={16} className="animate-spin" /> Loading definitions…
            </div>
          ) : error ? (
            <div className="flex flex-col items-center py-12 gap-3 text-red-500">
              <p className="text-sm">{error}</p>
              <button onClick={load} className="btn btn-secondary btn-sm">
                <RefreshCw size={12} /> Retry
              </button>
            </div>
          ) : filtered.length === 0 ? (
            <div className="text-center py-16 text-gray-400 text-sm">
              {search ? 'No definitions match your search.' : 'No workflow definitions exist yet.'}
            </div>
          ) : (
            <table className="w-full">
              <thead className="sticky top-0 bg-white z-10">
                <tr>
                  <th className="table-th">Name</th>
                  <th className="table-th">Category</th>
                  <th className="table-th">Active</th>
                  <th className="table-th">Versions</th>
                  <th className="table-th">Created</th>
                  <th className="table-th">Actions</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((def) => (
                  <tr
                    key={def.id}
                    className={`table-tr ${def.id === currentDefinitionId ? 'bg-blue-50/60' : ''}`}
                  >
                    <td className="table-td">
                      <p className="font-medium text-gray-900">{def.name}</p>
                      {def.description && (
                        <p className="text-xs text-gray-400 truncate max-w-[220px]">{def.description}</p>
                      )}
                      {def.id === currentDefinitionId && (
                        <span className="text-xs text-blue-600 font-medium">● Currently open</span>
                      )}
                    </td>
                    <td className="table-td text-gray-500">{def.category ?? '—'}</td>
                    <td className="table-td"><ActiveBadge active={def.isActive} /></td>
                    <td className="table-td">
                      <span className="text-gray-700">{def.versionCount}</span>
                      {def.latestVersionNumber != null && (
                        <span className="ml-1 text-gray-400 text-xs">v{def.latestVersionNumber}</span>
                      )}
                    </td>
                    <td className="table-td text-gray-500">{fmtDate(def.createdAt)}</td>
                    <td className="table-td">
                      <div className="flex gap-1">
                        <button
                          onClick={() => handleOpen(def)}
                          disabled={actionId === def.id}
                          className="btn btn-primary btn-sm"
                          title="Load latest version into designer"
                        >
                          <FolderOpen size={12} />
                          {actionId === def.id ? '…' : 'Open'}
                        </button>
                        <button
                          onClick={() => handleClone(def)}
                          disabled={actionId !== null}
                          className="btn btn-secondary btn-sm"
                          title="Duplicate definition + latest version"
                        >
                          {actionId === `${def.id}-clone`
                            ? <RefreshCw size={12} className="animate-spin" />
                            : <Copy size={12} />}
                        </button>
                        <button
                          onClick={() => handleDelete(def)}
                          disabled={actionId !== null}
                          className="btn btn-danger btn-sm"
                          title="Delete definition and all versions"
                        >
                          {actionId === `${def.id}-delete`
                            ? <RefreshCw size={12} className="animate-spin" />
                            : <Trash2 size={12} />}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Footer */}
        <div className="px-5 py-3 border-t border-gray-200 bg-gray-50 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <span className="text-xs text-gray-400">
              {filtered.length} of {definitions.length} definition{definitions.length !== 1 ? 's' : ''}
            </span>
            <button onClick={load} className="btn btn-secondary btn-sm" disabled={loading}>
              <RefreshCw size={12} className={loading ? 'animate-spin' : ''} />
              Refresh
            </button>
          </div>
          <button onClick={onClose} className="btn btn-secondary">Close</button>
        </div>
      </div>
    </div>
  );
}

