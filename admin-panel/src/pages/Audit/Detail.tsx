import { Link, useParams } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { auditApi } from '../../api/endpoints';
import type { AuditLogDetailDto } from '../../api/types';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import { fmtDate } from '../../utils/format';

function SeverityBadge({ severity }: { severity: string }) {
  const cls = {
    Info: 'bg-blue-100 text-blue-800',
    Warning: 'bg-yellow-100 text-yellow-800',
    Error: 'bg-red-100 text-red-800',
    Critical: 'bg-red-900 text-white',
  }[severity] ?? 'bg-gray-100 text-gray-700';

  return <span className={`badge ${cls}`}>{severity}</span>;
}

function DetailRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="py-3 border-b border-gray-100 last:border-0 grid grid-cols-1 sm:grid-cols-3 gap-1">
      <dt className="text-sm text-gray-500">{label}</dt>
      <dd className="text-sm font-medium text-gray-900 sm:col-span-2 break-all">{value ?? '—'}</dd>
    </div>
  );
}

function formatJson(raw: string | null | undefined): string {
  if (!raw) return '—';
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

export default function AuditDetail() {
  const { id = '' } = useParams();
  const [item, setItem] = useState<AuditLogDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const res = await auditApi.getById(id);
      setItem(res.data);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load audit record');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { load(); }, [load]);

  return (
    <div>
      <div className="flex items-center gap-3 mb-6">
        <Link to="/audit" className="btn btn-secondary !px-2"><ArrowLeft size={16} /></Link>
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Audit Detail</h1>
          <p className="text-sm text-gray-500 font-mono">{id}</p>
        </div>
      </div>

      {loading && <LoadingSpinner />}
      {error && <ErrorAlert message={error} onRetry={load} />}

      {item && (
        <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
          <div className="card p-6">
            <h2 className="font-semibold text-gray-800 mb-4">Event</h2>
            <dl>
              <DetailRow label="Timestamp" value={fmtDate(item.timestampUtc)} />
              <DetailRow label="User" value={item.displayName ? `${item.displayName} (${item.username})` : item.username} />
              <DetailRow label="Category" value={item.category} />
              <DetailRow label="Event Type" value={item.eventType} />
              <DetailRow label="Severity" value={<SeverityBadge severity={item.severity} />} />
              <DetailRow label="Entity" value={`${item.entityName || item.entityType} (${item.entityId})`} />
              <DetailRow label="Action" value={item.action} />
              <DetailRow label="Success" value={item.success ? 'Yes' : 'No'} />
              <DetailRow label="IP Address" value={item.ipAddress} />
              <DetailRow label="User Agent" value={item.userAgent} />
              <DetailRow label="Correlation Id" value={item.correlationId} />
            </dl>
          </div>

          <div className="card p-6">
            <h2 className="font-semibold text-gray-800 mb-4">Details</h2>
            <pre className="text-xs bg-gray-50 border border-gray-200 rounded-lg p-4 overflow-auto max-h-[480px] whitespace-pre-wrap">
              {formatJson(item.detailsJson)}
            </pre>
          </div>
        </div>
      )}
    </div>
  );
}
