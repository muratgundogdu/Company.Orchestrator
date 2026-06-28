import { Link } from 'react-router-dom';
import { RefreshCw, Download, ScrollText } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { auditApi } from '../../api/endpoints';
import type { AuditLogListItemDto, AuditQueryFilter, AuditSummaryDto } from '../../api/types';
import PageHeader from '../../components/PageHeader';
import Pagination from '../../components/Pagination';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import { fmtDate } from '../../utils/format';

const CATEGORIES = [
  'Authentication', 'Authorization', 'Workflow', 'Job', 'Credential',
  'UserManagement', 'RoleManagement', 'System', 'Worker',
];

const SEVERITIES = ['Info', 'Warning', 'Error', 'Critical'];

function SeverityBadge({ severity }: { severity: string }) {
  const cls = {
    Info: 'bg-blue-100 text-blue-800',
    Warning: 'bg-yellow-100 text-yellow-800',
    Error: 'bg-red-100 text-red-800',
    Critical: 'bg-red-900 text-white',
  }[severity] ?? 'bg-gray-100 text-gray-700';

  return <span className={`badge text-xs ${cls}`}>{severity}</span>;
}

function SuccessBadge({ success }: { success: boolean }) {
  return (
    <span className={`badge text-xs ${success ? 'badge-green' : 'badge-gray bg-red-50 text-red-700'}`}>
      {success ? 'Yes' : 'No'}
    </span>
  );
}

function toLocalInput(d: Date): string {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}

function exportCsv(items: AuditLogListItemDto[]) {
  const headers = ['Timestamp', 'Category', 'EventType', 'Severity', 'User', 'EntityType', 'EntityId', 'EntityName', 'Action', 'Success'];
  const rows = items.map((i) => [
    i.timestampUtc,
    i.category,
    i.eventType,
    i.severity,
    i.username ?? '',
    i.entityType,
    i.entityId,
    i.entityName ?? '',
    i.action,
    i.success ? 'Yes' : 'No',
  ]);
  const csv = [headers, ...rows]
    .map((row) => row.map((c) => `"${String(c).replace(/"/g, '""')}"`).join(','))
    .join('\n');
  const blob = new Blob([csv], { type: 'text/csv' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `audit-export-${new Date().toISOString().slice(0, 10)}.csv`;
  a.click();
  URL.revokeObjectURL(url);
}

export default function AuditList() {
  const [page, setPage] = useState(1);
  const [pageSize] = useState(50);
  const [fromUtc, setFromUtc] = useState(() => toLocalInput(new Date(Date.now() - 7 * 86400000)));
  const [toUtc, setToUtc] = useState(() => toLocalInput(new Date()));
  const [category, setCategory] = useState('');
  const [eventType, setEventType] = useState('');
  const [username, setUsername] = useState('');
  const [severity, setSeverity] = useState('');
  const [entityType, setEntityType] = useState('');
  const [entityId, setEntityId] = useState('');
  const [successFilter, setSuccessFilter] = useState<'all' | 'yes' | 'no'>('all');
  const [search, setSearch] = useState('');

  const [items, setItems] = useState<AuditLogListItemDto[]>([]);
  const [total, setTotal] = useState(0);
  const [summary, setSummary] = useState<AuditSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const filter = useMemo((): AuditQueryFilter => ({
    fromUtc: new Date(fromUtc).toISOString(),
    toUtc: new Date(toUtc).toISOString(),
    category: category || undefined,
    eventType: eventType || undefined,
    username: username || undefined,
    severity: severity || undefined,
    entityType: entityType || undefined,
    entityId: entityId || undefined,
    success: successFilter === 'all' ? undefined : successFilter === 'yes',
    search: search || undefined,
    page,
    pageSize,
  }), [fromUtc, toUtc, category, eventType, username, severity, entityType, entityId, successFilter, search, page, pageSize]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [listRes, summaryRes] = await Promise.all([
        auditApi.list(filter),
        auditApi.summary(filter),
      ]);
      setItems(listRes.data.items);
      setTotal(listRes.data.totalCount);
      setSummary(summaryRes.data);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load audit events');
    } finally {
      setLoading(false);
    }
  }, [filter]);

  useEffect(() => { load(); }, [load]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  return (
    <div>
      <PageHeader
        title="Audit Center"
        subtitle="Security, compliance, and operational audit trail"
        actions={
          <div className="flex gap-2">
            <button onClick={() => exportCsv(items)} className="btn btn-secondary btn-sm" disabled={items.length === 0}>
              <Download size={14} /> Export CSV
            </button>
            <button onClick={load} className="btn btn-secondary btn-sm" disabled={loading}>
              <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
            </button>
          </div>
        }
      />

      {summary && (
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-6">
          <div className="card p-4"><p className="text-xl font-bold">{summary.totalEvents}</p><p className="text-xs text-gray-500">Total Events</p></div>
          <div className="card p-4"><p className="text-xl font-bold text-red-900">{summary.criticalEvents}</p><p className="text-xs text-gray-500">Critical Events</p></div>
          <div className="card p-4"><p className="text-xl font-bold text-red-600">{summary.failedEvents}</p><p className="text-xs text-gray-500">Failed Events</p></div>
          <div className="card p-4"><p className="text-xl font-bold">{summary.uniqueUsers}</p><p className="text-xs text-gray-500">Unique Users</p></div>
        </div>
      )}

      <div className="card p-4 mb-4">
        <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-4 gap-3">
          <input type="datetime-local" className="input text-sm" value={fromUtc} onChange={(e) => { setPage(1); setFromUtc(e.target.value); }} />
          <input type="datetime-local" className="input text-sm" value={toUtc} onChange={(e) => { setPage(1); setToUtc(e.target.value); }} />
          <select className="input text-sm" value={category} onChange={(e) => { setPage(1); setCategory(e.target.value); }}>
            <option value="">All categories</option>
            {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
          <input className="input text-sm" placeholder="Event type" value={eventType} onChange={(e) => { setPage(1); setEventType(e.target.value); }} />
          <input className="input text-sm" placeholder="Username" value={username} onChange={(e) => { setPage(1); setUsername(e.target.value); }} />
          <select className="input text-sm" value={severity} onChange={(e) => { setPage(1); setSeverity(e.target.value); }}>
            <option value="">All severities</option>
            {SEVERITIES.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
          <select className="input text-sm" value={successFilter} onChange={(e) => { setPage(1); setSuccessFilter(e.target.value as 'all' | 'yes' | 'no'); }}>
            <option value="all">All outcomes</option>
            <option value="yes">Success only</option>
            <option value="no">Failed only</option>
          </select>
          <input className="input text-sm" placeholder="Entity type" value={entityType} onChange={(e) => { setPage(1); setEntityType(e.target.value); }} />
          <input className="input text-sm" placeholder="Entity ID" value={entityId} onChange={(e) => { setPage(1); setEntityId(e.target.value); }} />
          <input className="input text-sm md:col-span-2" placeholder="Search text" value={search} onChange={(e) => { setPage(1); setSearch(e.target.value); }} />
        </div>
      </div>

      <div className="card overflow-hidden">
        {loading && <LoadingSpinner />}
        {error && <ErrorAlert message={error} onRetry={load} />}
        {!loading && !error && (
          <>
            <table className="w-full">
              <thead>
                <tr>
                  <th className="table-th">Timestamp</th>
                  <th className="table-th">Category</th>
                  <th className="table-th">Event Type</th>
                  <th className="table-th">Severity</th>
                  <th className="table-th">User</th>
                  <th className="table-th">Entity</th>
                  <th className="table-th">Action</th>
                  <th className="table-th">Success</th>
                </tr>
              </thead>
              <tbody>
                {items.length === 0 && (
                  <tr><td colSpan={8} className="table-td text-center text-gray-400 py-12">No audit events found</td></tr>
                )}
                {items.map((item) => (
                  <tr key={item.id} className="table-tr">
                    <td className="table-td text-xs text-gray-500 whitespace-nowrap">
                      <Link to={`/audit/${item.id}`} className="text-blue-600 hover:underline flex items-center gap-1">
                        <ScrollText size={12} /> {fmtDate(item.timestampUtc)}
                      </Link>
                    </td>
                    <td className="table-td text-xs">{item.category}</td>
                    <td className="table-td text-xs font-medium">{item.eventType}</td>
                    <td className="table-td"><SeverityBadge severity={item.severity} /></td>
                    <td className="table-td text-xs">{item.displayName || item.username || '—'}</td>
                    <td className="table-td text-xs">
                      <div>{item.entityName || item.entityType}</div>
                      <div className="text-gray-400 font-mono truncate max-w-[120px]">{item.entityId}</div>
                    </td>
                    <td className="table-td text-xs">{item.action}</td>
                    <td className="table-td"><SuccessBadge success={item.success} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination page={page} totalPages={totalPages} totalCount={total} pageSize={pageSize} onPage={setPage} />
          </>
        )}
      </div>
    </div>
  );
}
