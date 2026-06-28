import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, RefreshCw, ExternalLink } from 'lucide-react';
import { triggerApi } from '../../api/endpoints';
import { useApi } from '../../hooks/useApi';
import PageHeader from '../../components/PageHeader';
import Pagination from '../../components/Pagination';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import { fmtDate } from '../../utils/format';

// ── Status badge ─────────────────────────────────────────────────────────────

const STATUS_COLORS: Record<string, string> = {
  Completed:  'bg-green-100 text-green-800',
  Failed:     'bg-red-100 text-red-800',
  Skipped:    'bg-yellow-100 text-yellow-800',
  Pending:    'bg-gray-100 text-gray-600',
  Processing: 'bg-blue-100 text-blue-800',
};

function StatusBadge({ status }: { status: string }) {
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold ${STATUS_COLORS[status] ?? 'bg-gray-100 text-gray-700'}`}>
      {status}
    </span>
  );
}

// ── Duration helper ───────────────────────────────────────────────────────────

function calcDuration(createdAt: string, completedAt: string | null): string {
  if (!completedAt) return '—';
  const ms = new Date(completedAt).getTime() - new Date(createdAt).getTime();
  if (ms < 0) return '—';
  if (ms < 1000) return `${ms}ms`;
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)}s`;
  const mins = Math.floor(ms / 60_000);
  const secs = Math.floor((ms % 60_000) / 1000);
  return `${mins}m ${secs}s`;
}

// ── Event type helper ─────────────────────────────────────────────────────────

function detectEventType(eventKey: string): string {
  if (eventKey.startsWith('scheduled:')) return 'Scheduled';
  if (eventKey.startsWith('fw-') || eventKey.includes('|')) return 'FolderWatcher';
  if (eventKey.startsWith('api-')) return 'API';
  return 'Manual';
}

const EVENT_TYPE_COLORS: Record<string, string> = {
  Scheduled:    'bg-purple-100 text-purple-800',
  FolderWatcher:'bg-blue-100 text-blue-800',
  API:          'bg-orange-100 text-orange-800',
  Manual:       'bg-gray-100 text-gray-700',
};

function EventTypeBadge({ eventKey }: { eventKey: string }) {
  const type = detectEventType(eventKey);
  return (
    <span className={`inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium ${EVENT_TYPE_COLORS[type] ?? 'bg-gray-100 text-gray-700'}`}>
      {type}
    </span>
  );
}

// ── Trigger type summary row ──────────────────────────────────────────────────

function TriggerInfoBanner({ name, type, cronExpression }: {
  name: string;
  type: string;
  cronExpression: string | null;
}) {
  return (
    <div className="px-4 py-3 bg-gray-50 border-b border-gray-200 flex items-center gap-3">
      <span className="text-sm font-medium text-gray-700">{name}</span>
      <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${
        type === 'Scheduled' ? 'bg-purple-100 text-purple-800' :
        type === 'FolderWatcher' ? 'bg-blue-100 text-blue-800' :
        'bg-gray-100 text-gray-700'
      }`}>{type}</span>
      {cronExpression && (
        <code className="text-xs text-purple-700 font-mono bg-purple-50 px-1 rounded">
          {cronExpression}
        </code>
      )}
    </div>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

export default function TriggerEvents() {
  const { id } = useParams<{ id: string }>();
  const [page, setPage] = useState(1);

  const { data: trigger } = useApi(() => triggerApi.getById(id!), [id]);

  const { data, loading, error, refetch } = useApi(
    () => triggerApi.getEvents(id!, page, 50),
    [id, page],
  );

  return (
    <div>
      <PageHeader
        title={trigger ? `History — ${trigger.name}` : 'Trigger History'}
        subtitle={`Trigger ID: ${id?.slice(0, 8)}…`}
        actions={
          <>
            <button onClick={refetch} className="btn btn-secondary" disabled={loading}>
              <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
            </button>
            <Link to="/triggers" className="btn btn-secondary">
              <ArrowLeft size={14} /> Back to Triggers
            </Link>
          </>
        }
      />

      <div className="card overflow-hidden">
        {trigger && (
          <TriggerInfoBanner
            name={trigger.name}
            type={trigger.type}
            cronExpression={trigger.cronExpression}
          />
        )}

        {loading && <LoadingSpinner />}
        {error && <ErrorAlert message={error} onRetry={refetch} />}
        {!loading && !error && data && (
          <>
            {data.totalCount === 0 ? (
              <div className="px-6 py-12 text-center text-gray-400 text-sm">
                No execution history recorded for this trigger yet.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[900px]">
                  <thead>
                    <tr>
                      <th className="table-th">Trigger Name</th>
                      <th className="table-th">Event Type</th>
                      <th className="table-th">File Name</th>
                      <th className="table-th">Status</th>
                      <th className="table-th">Process Instance</th>
                      <th className="table-th">Created At</th>
                      <th className="table-th">Completed At</th>
                      <th className="table-th">Duration</th>
                      <th className="table-th">Error</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.items.map((ev) => (
                      <tr key={ev.id} className="table-tr">
                        <td className="table-td">
                          <span className="font-medium text-gray-900 text-sm">
                            {trigger?.name ?? '—'}
                          </span>
                        </td>
                        <td className="table-td">
                          <EventTypeBadge eventKey={ev.eventKey} />
                        </td>
                        <td className="table-td">
                          {ev.fileName ? (
                            <span
                              className="text-sm text-gray-700 font-medium truncate block max-w-[140px]"
                              title={ev.filePath || ev.fileName}
                            >
                              {ev.fileName}
                            </span>
                          ) : (
                            <span className="text-gray-300 text-xs">—</span>
                          )}
                        </td>
                        <td className="table-td">
                          <StatusBadge status={ev.status} />
                        </td>
                        <td className="table-td">
                          {ev.processInstanceId ? (
                            <Link
                              to={`/process-instances/${ev.processInstanceId}`}
                              className="inline-flex items-center gap-1 font-mono text-xs text-blue-600 hover:underline"
                            >
                              {ev.processInstanceId.slice(0, 8)}…
                              <ExternalLink size={10} />
                            </Link>
                          ) : (
                            <span className="text-gray-300 text-xs">—</span>
                          )}
                        </td>
                        <td className="table-td text-gray-500 text-sm whitespace-nowrap">
                          {fmtDate(ev.createdAt)}
                        </td>
                        <td className="table-td text-gray-500 text-sm whitespace-nowrap">
                          {fmtDate(ev.completedAt)}
                        </td>
                        <td className="table-td text-gray-600 text-sm font-mono">
                          {calcDuration(ev.createdAt, ev.completedAt)}
                        </td>
                        <td className="table-td max-w-[180px]">
                          {ev.errorMessage ? (
                            <span
                              className="text-xs text-red-600 block truncate cursor-default"
                              title={ev.errorMessage}
                            >
                              {ev.errorMessage}
                            </span>
                          ) : (
                            <span className="text-gray-300 text-xs">—</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            {data.totalCount > 0 && (
              <Pagination
                page={data.page}
                totalPages={data.totalPages}
                totalCount={data.totalCount}
                pageSize={data.pageSize}
                onPage={setPage}
              />
            )}
          </>
        )}
      </div>
    </div>
  );
}
