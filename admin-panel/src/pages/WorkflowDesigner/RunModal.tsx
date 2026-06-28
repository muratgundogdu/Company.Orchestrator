import { useState } from 'react';
import { Play, ExternalLink, CheckCircle } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { processInstanceApi } from '../../api/endpoints';
import { VersionStatus } from '../../api/types';
import { VersionStatusBadge } from '../../components/StatusBadge';

interface RunModalProps {
  definitionId: string;
  definitionName: string;
  versionNumber: number | null;
  versionStatus: VersionStatus | null;
  onClose: () => void;
}

export default function RunModal({
  definitionId,
  definitionName,
  versionNumber,
  versionStatus,
  onClose,
}: RunModalProps) {
  const navigate = useNavigate();
  const [correlationId, setCorrelationId] = useState('');
  const [inputData, setInputData]         = useState('{}');
  const [running, setRunning]             = useState(false);
  const [error, setError]                 = useState<string | null>(null);
  const [instanceId, setInstanceId]       = useState<string | null>(null);

  async function handleRun() {
    if (inputData.trim()) {
      try { JSON.parse(inputData); } catch {
        setError('Input Data is not valid JSON');
        return;
      }
    }
    setRunning(true);
    setError(null);
    try {
      const parsedInput = inputData.trim() && inputData.trim() !== '{}' ? inputData.trim() : undefined;
      const res = await processInstanceApi.start({
        processDefinitionId: definitionId,
        correlationId: correlationId.trim() || undefined,
        inputData: parsedInput,
        triggeredBy: 'workflow-designer',
      });
      setInstanceId(res.data.id);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to start process');
    } finally {
      setRunning(false);
    }
  }

  // ── Success screen ─────────────────────────────────────────────────────────────
  if (instanceId) {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
        <div className="bg-white rounded-xl shadow-2xl w-[400px] p-8 text-center">
          <div className="flex h-16 w-16 items-center justify-center rounded-full bg-green-100 mx-auto mb-4">
            <CheckCircle size={28} className="text-green-600" />
          </div>
          <h3 className="text-lg font-semibold text-gray-900 mb-1">Process Started!</h3>
          <p className="text-sm text-gray-500 mb-1">
            <strong>{definitionName}</strong> is now running.
          </p>
          <p className="text-xs text-gray-400 font-mono mb-6 truncate">{instanceId}</p>
          <div className="flex gap-2 justify-center">
            <button onClick={onClose} className="btn btn-secondary">Close</button>
            <button
              onClick={() => navigate(`/process-instances/${instanceId}`)}
              className="btn btn-primary"
            >
              <ExternalLink size={13} />
              View Instance
            </button>
          </div>
        </div>
      </div>
    );
  }

  // ── Run form ───────────────────────────────────────────────────────────────────
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="bg-white rounded-xl shadow-2xl w-[500px] flex flex-col overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <div className="flex items-center gap-2">
            <div className="flex h-8 w-8 items-center justify-center rounded-full bg-emerald-100">
              <Play size={15} className="text-emerald-600" />
            </div>
            <h2 className="font-semibold text-gray-900">Run Workflow</h2>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-2xl leading-none">×</button>
        </div>

        {/* Body */}
        <div className="p-5 space-y-4">
          {/* Definition info */}
          <div className="flex items-center justify-between bg-gray-50 rounded-lg px-4 py-3">
            <div>
              <p className="font-medium text-gray-900">{definitionName}</p>
              {versionNumber != null && (
                <p className="text-xs text-gray-400 mt-0.5">Version {versionNumber}</p>
              )}
            </div>
            {versionStatus != null && (
              <VersionStatusBadge status={versionStatus} />
            )}
          </div>

          {versionStatus != null && versionStatus !== VersionStatus.Published && (
            <div className="flex items-start gap-2 bg-amber-50 border border-amber-200 text-amber-700 text-xs rounded-lg px-3 py-2">
              <span className="text-base leading-none">⚠</span>
              <span>
                This version is <strong>not published</strong>. It will still run but consider
                publishing before production use.
              </span>
            </div>
          )}

          {error && (
            <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-3 py-2">
              {error}
            </div>
          )}

          <div>
            <label className="label">Correlation ID <span className="text-gray-400">(optional)</span></label>
            <input
              value={correlationId}
              onChange={(e) => setCorrelationId(e.target.value)}
              className="input font-mono text-sm"
              placeholder="e.g. run-2026-06-19-001"
            />
            <p className="text-xs text-gray-400 mt-1">
              Used to group related instances. Leave blank to auto-generate.
            </p>
          </div>

          <div>
            <label className="label">Input Data <span className="text-gray-400">(JSON)</span></label>
            <textarea
              value={inputData}
              onChange={(e) => setInputData(e.target.value)}
              rows={5}
              className="input font-mono text-xs resize-y"
              spellCheck={false}
            />
          </div>
        </div>

        {/* Footer */}
        <div className="flex gap-2 justify-end px-5 py-3 border-t border-gray-200 bg-gray-50">
          <button onClick={onClose} className="btn btn-secondary">Cancel</button>
          <button onClick={handleRun} disabled={running} className="btn btn-success">
            <Play size={13} />
            {running ? 'Starting…' : 'Run Now'}
          </button>
        </div>
      </div>
    </div>
  );
}
