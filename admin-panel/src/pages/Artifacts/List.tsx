import { useState } from 'react';
import { Link } from 'react-router-dom';
import { RefreshCw, Download } from 'lucide-react';
import { artifactApi } from '../../api/endpoints';
import { useApi } from '../../hooks/useApi';
import PageHeader from '../../components/PageHeader';
import Pagination from '../../components/Pagination';
import LoadingSpinner from '../../components/LoadingSpinner';
import ErrorAlert from '../../components/ErrorAlert';
import { fmtDate, fmtBytes, shortId } from '../../utils/format';
import { downloadArtifact } from '../../utils/downloadArtifact';

export default function ArtifactList() {
  const [page, setPage] = useState(1);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  async function handleDownload(id: string, name: string) {
    setDownloadingId(id);
    try {
      await downloadArtifact(id, name);
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Download failed');
    } finally {
      setDownloadingId(null);
    }
  }

  const { data, loading, error, refetch } = useApi(
    () => artifactApi.list(page, 20),
    [page],
  );

  return (
    <div>
      <PageHeader
        title="Artifacts"
        subtitle="Files and data produced by workflow executions"
        actions={
          <button onClick={refetch} className="btn btn-secondary" disabled={loading}>
            <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
          </button>
        }
      />

      <div className="card overflow-hidden">
        {loading && <LoadingSpinner />}
        {error && <ErrorAlert message={error} onRetry={refetch} />}
        {!loading && !error && data && (
          <>
            <table className="w-full">
              <thead>
                <tr>
                  <th className="table-th">Name</th>
                  <th className="table-th">Content Type</th>
                  <th className="table-th">Size</th>
                  <th className="table-th">Persistent</th>
                  <th className="table-th">Process Instance</th>
                  <th className="table-th">Created</th>
                  <th className="table-th">Download</th>
                </tr>
              </thead>
              <tbody>
                {data.items.length === 0 && (
                  <tr>
                    <td colSpan={7} className="table-td text-center text-gray-400 py-12">
                      No artifacts found
                    </td>
                  </tr>
                )}
                {data.items.map((a) => (
                  <tr key={a.id} className="table-tr">
                    <td className="table-td">
                      <span className="font-medium text-gray-900">{a.name}</span>
                      {a.metadata && Object.keys(a.metadata).length > 0 && (
                        <details className="mt-0.5">
                          <summary className="text-xs text-gray-400 cursor-pointer hover:text-gray-600">
                            metadata ({Object.keys(a.metadata).length})
                          </summary>
                          <pre className="text-xs text-gray-500 mt-1 bg-gray-50 p-2 rounded max-w-xs overflow-x-auto">
                            {JSON.stringify(a.metadata, null, 2)}
                          </pre>
                        </details>
                      )}
                    </td>
                    <td className="table-td text-gray-500 text-xs font-mono">{a.contentType}</td>
                    <td className="table-td text-gray-500">{fmtBytes(a.sizeBytes)}</td>
                    <td className="table-td text-center">
                      <span className={`text-xs font-medium ${a.isPersistent ? 'text-green-600' : 'text-gray-400'}`}>
                        {a.isPersistent ? '✓' : '—'}
                      </span>
                    </td>
                    <td className="table-td">
                      <Link
                        to={`/process-instances/${a.processInstanceId}`}
                        className="font-mono text-xs text-blue-600 hover:underline"
                      >
                        {shortId(a.processInstanceId)}
                      </Link>
                    </td>
                    <td className="table-td text-gray-500">{fmtDate(a.createdAt)}</td>
                    <td className="table-td">
                      <button
                        type="button"
                        onClick={() => handleDownload(a.id, a.name)}
                        disabled={downloadingId === a.id}
                        className="btn btn-secondary btn-sm inline-flex items-center gap-1"
                      >
                        <Download size={12} />
                        {downloadingId === a.id ? 'Downloading…' : 'Download'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
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
