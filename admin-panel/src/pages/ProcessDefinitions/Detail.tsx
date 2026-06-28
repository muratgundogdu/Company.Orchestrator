import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, RefreshCw, CheckCircle, Plus } from 'lucide-react';
import { processDefinitionApi } from '../../api/endpoints';
import { useApi } from '../../hooks/useApi';
import { VersionStatus } from '../../api/types';
import PageHeader from '../../components/PageHeader';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import { ActiveBadge, VersionStatusBadge } from '../../components/StatusBadge';
import { fmtDate } from '../../utils/format';

type Tab = 'info' | 'versions';

const PLACEHOLDER_DEF = JSON.stringify(
  {
    name: 'my-workflow',
    version: '1',
    steps: [
      {
        id: 'step-1',
        type: 'delay',
        name: 'Wait',
        config: { milliseconds: 500 },
      },
    ],
  },
  null,
  2,
);

export default function ProcessDefinitionDetail() {
  const { id } = useParams<{ id: string }>();
  const [tab, setTab] = useState<Tab>('info');

  const {
    data: def,
    loading: defLoading,
    error: defError,
    refetch: refetchDef,
  } = useApi(() => processDefinitionApi.getById(id!), [id]);

  const {
    data: versions,
    loading: vLoading,
    error: vError,
    refetch: refetchVersions,
  } = useApi(() => processDefinitionApi.getVersions(id!), [id]);

  // Create version form
  const [jsonDef, setJsonDef] = useState(PLACEHOLDER_DEF);
  const [changeNotes, setChangeNotes] = useState('');
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);
  const [showCreateForm, setShowCreateForm] = useState(false);

  const [publishingId, setPublishingId] = useState<string | null>(null);

  async function handlePublish(versionId: string) {
    if (!id) return;
    if (!confirm('Publish this version? Any currently published version will be superseded.')) return;
    setPublishingId(versionId);
    try {
      await processDefinitionApi.publishVersion(id, versionId);
      refetchVersions();
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : 'Publish failed');
    } finally {
      setPublishingId(null);
    }
  }

  async function handleCreateVersion() {
    if (!id) return;
    setCreating(true);
    setCreateError(null);
    try {
      await processDefinitionApi.createVersion(id, {
        jsonDefinition: jsonDef,
        changeNotes: changeNotes || undefined,
      });
      setShowCreateForm(false);
      setJsonDef(PLACEHOLDER_DEF);
      setChangeNotes('');
      refetchVersions();
    } catch (e: unknown) {
      setCreateError(e instanceof Error ? e.message : 'Failed to create version');
    } finally {
      setCreating(false);
    }
  }

  if (defLoading) return <LoadingSpinner />;
  if (defError)   return <ErrorAlert message={defError} onRetry={refetchDef} />;
  if (!def)       return null;

  return (
    <div>
      <PageHeader
        title={def.name}
        subtitle={def.description ?? undefined}
        actions={
          <Link to="/process-definitions" className="btn btn-secondary">
            <ArrowLeft size={14} /> Back
          </Link>
        }
      />

      {/* Tabs */}
      <div className="flex gap-0 border-b border-gray-200 mb-6">
        {(['info', 'versions'] as Tab[]).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-5 py-2.5 text-sm font-medium border-b-2 transition-colors capitalize ${
              tab === t
                ? 'border-brand-primary text-brand-primary'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            {t}
            {t === 'versions' && versions && (
              <span className="ml-1.5 rounded-full bg-gray-100 px-1.5 text-xs text-gray-600">
                {versions.length}
              </span>
            )}
          </button>
        ))}
      </div>

      {/* ── Info tab ─────────────────────────────────────────────────────── */}
      {tab === 'info' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <div className="card p-6 space-y-4">
            <h3 className="font-semibold text-gray-700 text-sm uppercase tracking-wide">Details</h3>
            {[
              { label: 'ID',          value: <span className="font-mono text-xs">{def.id}</span> },
              { label: 'Name',        value: def.name },
              { label: 'Description', value: def.description ?? '—' },
              { label: 'Category',    value: def.category ?? '—' },
              { label: 'Status',      value: <ActiveBadge active={def.isActive} /> },
              { label: 'Versions',    value: def.versionCount },
              { label: 'Latest',      value: def.latestVersionNumber != null ? `v${def.latestVersionNumber}` : '—' },
              { label: 'Created',     value: fmtDate(def.createdAt) },
            ].map(({ label, value }) => (
              <div key={label} className="flex items-start justify-between gap-4">
                <span className="text-sm text-gray-500 w-28 shrink-0">{label}</span>
                <span className="text-sm text-gray-900 font-medium text-right">{value}</span>
              </div>
            ))}
          </div>

          <div className="card p-6">
            <h3 className="font-semibold text-gray-700 text-sm uppercase tracking-wide mb-4">Quick Actions</h3>
            <div className="space-y-2">
              <Link
                to={`/process-instances?definitionId=${def.id}`}
                className="btn btn-secondary w-full justify-center"
              >
                View Instances
              </Link>
              <button
                onClick={() => { setTab('versions'); setShowCreateForm(true); }}
                className="btn btn-primary w-full justify-center"
              >
                <Plus size={14} />
                New Version
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Versions tab ──────────────────────────────────────────────────── */}
      {tab === 'versions' && (
        <div className="space-y-4">
          <div className="flex justify-end">
            <button
              onClick={() => setShowCreateForm(!showCreateForm)}
              className="btn btn-primary"
            >
              <Plus size={14} />
              New Version
            </button>
          </div>

          {/* Create version form */}
          {showCreateForm && (
            <div className="card p-5 space-y-4 border-blue-200 bg-blue-50/30">
              <h3 className="font-semibold text-gray-800">Create New Version</h3>
              {createError && (
                <p className="text-sm text-red-600 bg-red-50 rounded px-3 py-2">{createError}</p>
              )}
              <div>
                <label className="label">JSON Definition</label>
                <textarea
                  value={jsonDef}
                  onChange={(e) => setJsonDef(e.target.value)}
                  rows={12}
                  className="input font-mono text-xs"
                />
              </div>
              <div>
                <label className="label">Change Notes (optional)</label>
                <input
                  value={changeNotes}
                  onChange={(e) => setChangeNotes(e.target.value)}
                  className="input"
                  placeholder="What changed in this version…"
                />
              </div>
              <div className="flex gap-2 justify-end">
                <button onClick={() => setShowCreateForm(false)} className="btn btn-secondary">
                  Cancel
                </button>
                <button onClick={handleCreateVersion} disabled={creating} className="btn btn-primary">
                  {creating ? <RefreshCw size={14} className="animate-spin" /> : <Plus size={14} />}
                  Create Version
                </button>
              </div>
            </div>
          )}

          {/* Versions list */}
          <div className="card overflow-hidden">
            {vLoading && <LoadingSpinner />}
            {vError && <ErrorAlert message={vError} onRetry={refetchVersions} />}
            {!vLoading && !vError && versions && (
              <table className="w-full">
                <thead>
                  <tr>
                    <th className="table-th">Version</th>
                    <th className="table-th">Status</th>
                    <th className="table-th">Change Notes</th>
                    <th className="table-th">Published</th>
                    <th className="table-th">Created</th>
                    <th className="table-th">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {versions.length === 0 && (
                    <tr>
                      <td colSpan={6} className="table-td text-center text-gray-400 py-10">
                        No versions yet
                      </td>
                    </tr>
                  )}
                  {versions.map((v) => (
                    <tr key={v.id} className="table-tr">
                      <td className="table-td font-semibold">v{v.versionNumber}</td>
                      <td className="table-td"><VersionStatusBadge status={v.status} /></td>
                      <td className="table-td max-w-xs truncate text-gray-500">{v.changeNotes ?? '—'}</td>
                      <td className="table-td text-gray-500">{fmtDate(v.publishedAt)}</td>
                      <td className="table-td text-gray-500">{fmtDate(v.createdAt)}</td>
                      <td className="table-td">
                        {v.status === VersionStatus.Draft && (
                          <button
                            onClick={() => handlePublish(v.id)}
                            disabled={publishingId === v.id}
                            className="btn btn-success btn-sm"
                          >
                            <CheckCircle size={12} />
                            {publishingId === v.id ? 'Publishing…' : 'Publish'}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
