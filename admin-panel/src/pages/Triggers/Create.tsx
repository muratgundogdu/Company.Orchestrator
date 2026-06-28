import { useState, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { ArrowLeft, Save } from 'lucide-react';
import { triggerApi, processDefinitionApi } from '../../api/endpoints';
import type { ProcessDefinitionDto } from '../../api/types';
import PageHeader from '../../components/PageHeader';

type TriggerType = 'FolderWatcher' | 'Scheduled' | 'Manual' | 'Api';

interface FolderWatcherConfig {
  folderPath: string;
  filePattern: string;
  processExistingFiles: boolean;
}

export default function TriggerCreate() {
  const navigate = useNavigate();

  const [definitions, setDefinitions] = useState<ProcessDefinitionDto[]>([]);
  const [loadingDefs, setLoadingDefs] = useState(true);

  const [name, setName] = useState('');
  const [definitionId, setDefinitionId] = useState('');
  const [type, setType] = useState<TriggerType>('FolderWatcher');
  const [isActive, setIsActive] = useState(true);
  const [cronExpression, setCronExpression] = useState('');
  const [folderConfig, setFolderConfig] = useState<FolderWatcherConfig>({
    folderPath: 'C:\\Temp\\AlterOneInput',
    filePattern: '*.xlsx',
    processExistingFiles: false,
  });
  const [rawConfigJson, setRawConfigJson] = useState('');
  const [useRawJson, setUseRawJson] = useState(false);
  const [defaultInputData, setDefaultInputData] = useState('');

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    processDefinitionApi.list(1, 100).then((r) => {
      setDefinitions(r.data.items);
      if (r.data.items.length > 0) setDefinitionId(r.data.items[0].id);
    }).finally(() => setLoadingDefs(false));
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!definitionId) { setError('Select a process definition'); return; }

    let configJson: string | undefined;
    if (type === 'FolderWatcher') {
      configJson = useRawJson
        ? rawConfigJson || undefined
        : JSON.stringify(folderConfig);
    } else if (type === 'Scheduled') {
      configJson = undefined;
    }

    setSubmitting(true);
    setError(null);
    try {
      await triggerApi.create({
        name,
        processDefinitionId: definitionId,
        type,
        isActive,
        cronExpression: type === 'Scheduled' ? cronExpression || undefined : undefined,
        configJson,
        defaultInputData: defaultInputData || undefined,
      });
      navigate('/triggers');
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Create failed');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div>
      <PageHeader
        title="Create Trigger"
        subtitle="Configure a new trigger to start workflows automatically"
        actions={
          <Link to="/triggers" className="btn btn-secondary">
            <ArrowLeft size={14} /> Back
          </Link>
        }
      />

      <form onSubmit={handleSubmit} className="max-w-2xl space-y-6">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-3">
            {error}
          </div>
        )}

        <div className="card p-6 space-y-5">
          <h3 className="font-semibold text-gray-700">Basic Settings</h3>

          <div>
            <label className="label" htmlFor="name">Trigger Name *</label>
            <input
              id="name"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="input"
              placeholder="e.g. Watch Excel Reports Folder"
              maxLength={200}
            />
          </div>

          <div>
            <label className="label" htmlFor="definition">Process Definition *</label>
            <select
              id="definition"
              required
              value={definitionId}
              onChange={(e) => setDefinitionId(e.target.value)}
              className="input"
              disabled={loadingDefs}
            >
              <option value="">
                {loadingDefs ? 'Loading definitions…' : '— Select a definition —'}
              </option>
              {definitions.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}{d.latestVersionNumber != null ? ` (v${d.latestVersionNumber})` : ''}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="label" htmlFor="type">Trigger Type *</label>
            <select
              id="type"
              value={type}
              onChange={(e) => setType(e.target.value as TriggerType)}
              className="input"
            >
              <option value="FolderWatcher">FolderWatcher</option>
              <option value="Scheduled">Scheduled (Cron)</option>
              <option value="Manual">Manual</option>
              <option value="Api">API</option>
            </select>
          </div>

          <div className="flex items-center gap-3">
            <input
              type="checkbox"
              id="isActive"
              checked={isActive}
              onChange={(e) => setIsActive(e.target.checked)}
              className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <label htmlFor="isActive" className="text-sm text-gray-700 font-medium">
              Activate immediately
            </label>
          </div>
        </div>

        {/* FolderWatcher config */}
        {type === 'FolderWatcher' && (
          <div className="card p-6 space-y-5">
            <div className="flex items-center justify-between">
              <h3 className="font-semibold text-gray-700">Folder Watcher Settings</h3>
              <button
                type="button"
                onClick={() => setUseRawJson(!useRawJson)}
                className="text-xs text-blue-600 hover:underline"
              >
                {useRawJson ? 'Use form' : 'Use raw JSON'}
              </button>
            </div>

            {!useRawJson ? (
              <>
                <div>
                  <label className="label" htmlFor="folderPath">Folder Path *</label>
                  <input
                    id="folderPath"
                    value={folderConfig.folderPath}
                    onChange={(e) => setFolderConfig({ ...folderConfig, folderPath: e.target.value })}
                    className="input font-mono text-sm"
                    placeholder="C:\Temp\AlterOneInput"
                  />
                </div>
                <div>
                  <label className="label" htmlFor="filePattern">File Pattern</label>
                  <input
                    id="filePattern"
                    value={folderConfig.filePattern}
                    onChange={(e) => setFolderConfig({ ...folderConfig, filePattern: e.target.value })}
                    className="input font-mono text-sm"
                    placeholder="*.xlsx"
                  />
                  <p className="mt-1 text-xs text-gray-400">Wildcards supported: *.xlsx, report-*.csv, etc.</p>
                </div>
                <div className="flex items-center gap-3">
                  <input
                    type="checkbox"
                    id="processExisting"
                    checked={folderConfig.processExistingFiles}
                    onChange={(e) =>
                      setFolderConfig({ ...folderConfig, processExistingFiles: e.target.checked })
                    }
                    className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                  />
                  <label htmlFor="processExisting" className="text-sm text-gray-700">
                    Process existing files on activation
                  </label>
                </div>

                <div className="rounded-md bg-gray-50 border border-gray-200 p-3">
                  <p className="text-xs text-gray-500 mb-1">Preview (ConfigJson):</p>
                  <pre className="text-xs font-mono text-gray-600">
                    {JSON.stringify(folderConfig, null, 2)}
                  </pre>
                </div>
              </>
            ) : (
              <div>
                <label className="label" htmlFor="rawJson">Config JSON</label>
                <textarea
                  id="rawJson"
                  value={rawConfigJson}
                  onChange={(e) => setRawConfigJson(e.target.value)}
                  rows={8}
                  className="input font-mono text-xs"
                  placeholder={JSON.stringify(
                    { folderPath: 'C:\\Temp\\Input', filePattern: '*.xlsx', processExistingFiles: false },
                    null,
                    2,
                  )}
                />
              </div>
            )}
          </div>
        )}

        {/* Scheduled cron config */}
        {type === 'Scheduled' && (
          <div className="card p-6 space-y-4">
            <h3 className="font-semibold text-gray-700">Schedule Settings</h3>
            <div>
              <label className="label" htmlFor="cron">Cron Expression *</label>
              <input
                id="cron"
                value={cronExpression}
                onChange={(e) => setCronExpression(e.target.value)}
                className="input font-mono text-sm"
                placeholder="0 9 * * 1-5"
              />
              <p className="mt-1 text-xs text-gray-400">
                e.g.{' '}
                <code className="bg-gray-100 px-1 rounded">0 9 * * 1-5</code> = weekdays at 9am
              </p>
            </div>
          </div>
        )}

        {/* Default input data */}
        <div className="card p-6 space-y-4">
          <h3 className="font-semibold text-gray-700">Default Input (optional)</h3>
          <div>
            <label className="label" htmlFor="inputData">Default Input Data (JSON)</label>
            <textarea
              id="inputData"
              value={defaultInputData}
              onChange={(e) => setDefaultInputData(e.target.value)}
              rows={4}
              className="input font-mono text-xs"
              placeholder='{"key": "value"}'
            />
            <p className="mt-1 text-xs text-gray-400">
              Merged into the process instance input on each trigger.
            </p>
          </div>
        </div>

        <div className="flex gap-3 justify-end">
          <Link to="/triggers" className="btn btn-secondary">Cancel</Link>
          <button type="submit" disabled={submitting} className="btn btn-primary">
            <Save size={14} />
            {submitting ? 'Creating…' : 'Create Trigger'}
          </button>
        </div>
      </form>
    </div>
  );
}
