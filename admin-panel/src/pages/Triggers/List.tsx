import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Plus, RefreshCw, Zap, ZapOff, History, GitBranch } from 'lucide-react';
import { triggerApi } from '../../api/endpoints';
import { useApi } from '../../hooks/useApi';
import PageHeader from '../../components/PageHeader';
import Pagination from '../../components/Pagination';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import { ActiveBadge } from '../../components/StatusBadge';
import { fmtDate } from '../../utils/format';

// ── Trigger type badge ───────────────────────────────────────────────────────

const TYPE_COLORS: Record<string, string> = {
  Scheduled:     'bg-purple-100 text-purple-800',
  FolderWatcher: 'bg-blue-100 text-blue-800',
  Api:           'bg-orange-100 text-orange-800',
  Manual:        'bg-gray-100 text-gray-700',
};

function TypeBadge({ type }: { type: string }) {
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${TYPE_COLORS[type] ?? 'bg-gray-100 text-gray-700'}`}>
      {type}
    </span>
  );
}

// ── Last result badge ────────────────────────────────────────────────────────

const RESULT_COLORS: Record<string, string> = {
  Completed:  'bg-green-100 text-green-800',
  Failed:     'bg-red-100 text-red-800',
  Skipped:    'bg-yellow-100 text-yellow-800',
  Pending:    'bg-gray-100 text-gray-600',
  Processing: 'bg-blue-100 text-blue-800',
};

function LastResultBadge({
  status,
  error,
}: {
  status: string | null;
  error: string | null;
}) {
  if (!status) return <span className="text-gray-300 text-xs">—</span>;

  const badge = (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${RESULT_COLORS[status] ?? 'bg-gray-100 text-gray-700'}`}
    >
      {status}
    </span>
  );

  if (status === 'Failed' && error) {
    return (
      <span className="relative group cursor-default inline-block">
        {badge}
        {/* Tooltip */}
        <span className="absolute bottom-full left-0 mb-1 w-56 bg-gray-900 text-white text-xs rounded px-2 py-1 opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none z-50 shadow-lg whitespace-normal break-words">
          {error}
        </span>
      </span>
    );
  }

  return badge;
}

// ── Config / cron summary ────────────────────────────────────────────────────

function ConfigSummary({ type, cronExpression, configJson }: {
  type: string;
  cronExpression: string | null;
  configJson: string | null;
}) {
  if (type === 'Scheduled' && cronExpression) {
    return <code className="text-xs text-purple-700 font-mono bg-purple-50 px-1 rounded">{cronExpression}</code>;
  }
  if (type === 'FolderWatcher' && configJson) {
    try {
      const cfg = JSON.parse(configJson) as { folderPath?: string; filePattern?: string };
      return (
        <span className="text-xs text-gray-500">
          {cfg.folderPath ?? '—'}
          {cfg.filePattern ? ` (${cfg.filePattern})` : ''}
        </span>
      );
    } catch {
      return <span className="text-xs text-gray-400 font-mono truncate">{configJson.slice(0, 40)}</span>;
    }
  }
  return <span className="text-gray-300 text-xs">—</span>;
}

// ── Process Definition link ──────────────────────────────────────────────────

function DefLink({ id }: { id: string }) {
  return (
    <Link
      to={`/process-definitions/${id}`}
      className="font-mono text-xs text-blue-600 hover:underline"
      title={id}
    >
      {id.slice(0, 8)}…
    </Link>
  );
}

// ── Main component ───────────────────────────────────────────────────────────

export default function TriggerList() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [actionId, setActionId] = useState<string | null>(null);

  const { data, loading, error, refetch } = useApi(
    () => triggerApi.list(page, 20),
    [page],
  );

  async function toggleActive(id: string, currentlyActive: boolean) {
    setActionId(id);
    try {
      if (currentlyActive) {
        await triggerApi.deactivate(id);
      } else {
        await triggerApi.activate(id);
      }
      refetch();
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : 'Action failed');
    } finally {
      setActionId(null);
    }
  }

  return (
    <div>
      <PageHeader
        title="Triggers"
        subtitle="Automate workflow starts with file watchers, schedules, and API triggers"
        actions={
          <>
            <button onClick={refetch} className="btn btn-secondary" disabled={loading}>
              <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
            </button>
            <Link to="/triggers/create" className="btn btn-primary">
              <Plus size={14} />
              New Trigger
            </Link>
          </>
        }
      />

      <div className="card overflow-hidden">
        {loading && <LoadingSpinner />}
        {error && <ErrorAlert message={error} onRetry={refetch} />}
        {!loading && !error && data && (
          <>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[900px]">
                <thead>
                  <tr>
                    <th className="table-th">Name</th>
                    <th className="table-th">Type</th>
                    <th className="table-th">Process Definition</th>
                    <th className="table-th">Status</th>
                    <th className="table-th">Cron / Config</th>
                    <th className="table-th">Last Triggered</th>
                    <th className="table-th">Next Scheduled</th>
                    <th className="table-th">Last Result</th>
                    <th className="table-th">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.length === 0 && (
                    <tr>
                      <td colSpan={9} className="table-td text-center text-gray-400 py-12">
                        No triggers found.{' '}
                        <Link to="/triggers/create" className="text-blue-600 hover:underline">
                          Create one
                        </Link>
                      </td>
                    </tr>
                  )}
                  {data.items.map((t) => (
                    <tr key={t.id} className="table-tr">
                      <td className="table-td">
                        <span className="font-medium text-gray-900">{t.name}</span>
                      </td>
                      <td className="table-td">
                        <TypeBadge type={t.type} />
                      </td>
                      <td className="table-td">
                        <DefLink id={t.processDefinitionId} />
                      </td>
                      <td className="table-td">
                        <ActiveBadge active={t.isActive} />
                      </td>
                      <td className="table-td max-w-[180px] truncate">
                        <ConfigSummary
                          type={t.type}
                          cronExpression={t.cronExpression}
                          configJson={t.configJson}
                        />
                      </td>
                      <td className="table-td text-gray-500 text-sm">
                        {fmtDate(t.lastTriggeredAt)}
                      </td>
                      <td className="table-td text-gray-500 text-sm">
                        {fmtDate(t.nextScheduledAt)}
                      </td>
                      <td className="table-td">
                        <LastResultBadge
                          status={t.lastEventStatus}
                          error={t.lastEventError}
                        />
                      </td>
                      <td className="table-td">
                        <div className="flex gap-1 flex-wrap">
                          <button
                            onClick={() => toggleActive(t.id, t.isActive)}
                            disabled={actionId === t.id}
                            className={`btn btn-sm ${t.isActive ? 'btn-secondary' : 'btn-success'}`}
                            title={t.isActive ? 'Deactivate trigger' : 'Activate trigger'}
                          >
                            {t.isActive
                              ? <><ZapOff size={12} /> Deactivate</>
                              : <><Zap size={12} /> Activate</>}
                          </button>
                          <Link
                            to={`/triggers/${t.id}/events`}
                            className="btn btn-secondary btn-sm"
                            title="View trigger execution history"
                          >
                            <History size={12} /> History
                          </Link>
                          <button
                            onClick={() =>
                              navigate(`/workflow-designer?definitionId=${t.processDefinitionId}`)
                            }
                            className="btn btn-secondary btn-sm"
                            title="Open linked workflow in Designer"
                          >
                            <GitBranch size={12} /> Designer
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
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
