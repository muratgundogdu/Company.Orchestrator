import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1 flex items-center gap-1">⚠ {msg}</p> : null;
}

export default function FolderListFilesEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const folderPath     = String(config.folderPath     ?? 'C:\\Reports');
  const searchPattern  = String(config.searchPattern  ?? '*.*');
  const recursive      = config.recursive === true || config.recursive === 'true';
  const outputVariable = String(config.outputVariable ?? 'files');

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Folder Path *</label>
        <input
          value={folderPath}
          onChange={(e) => onChange({ ...config, folderPath: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'folderPath')}
          className={`input font-mono text-xs ${fieldErrors?.folderPath ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="C:\\Reports"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="folderPath" />
        {!fieldErrors?.folderPath && (
          <p className="text-xs text-gray-400 mt-1">
            Directory to scan. Supports variables, e.g.{' '}
            <code className="bg-gray-100 px-0.5 rounded">{'{{watchFolder}}'}</code>.
          </p>
        )}
      </div>

      <div>
        <label className="label">Search Pattern</label>
        <input
          value={searchPattern}
          onChange={(e) => onChange({ ...config, searchPattern: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'searchPattern')}
          className="input font-mono text-xs"
          placeholder="*.*"
          spellCheck={false}
        />
        <p className="text-xs text-gray-400 mt-1">
          Examples: <code className="bg-gray-100 px-0.5 rounded">*.*</code>,{' '}
          <code className="bg-gray-100 px-0.5 rounded">*.xlsx</code>,{' '}
          <code className="bg-gray-100 px-0.5 rounded">*.csv</code>,{' '}
          <code className="bg-gray-100 px-0.5 rounded">report_*.pdf</code>
        </p>
      </div>

      <div className="flex items-center gap-2">
        <input
          id="folder-list-recursive"
          type="checkbox"
          checked={recursive}
          onChange={(e) => onChange({ ...config, recursive: e.target.checked })}
          className="rounded border-gray-300"
        />
        <label htmlFor="folder-list-recursive" className="text-sm text-gray-700">
          Recursive (include sub-folders)
        </label>
      </div>

      <div>
        <label className="label">Output Variable *</label>
        <input
          value={outputVariable}
          onChange={(e) => onChange({ ...config, outputVariable: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
          className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="files"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputVariable" />
        {!fieldErrors?.outputVariable && (
          <p className="text-xs text-gray-400 mt-1">
            Base name for outputs:{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable || 'files'}}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable || 'files'}_count}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable || 'files'}_first}}`}</code>
          </p>
        )}
      </div>
    </div>
  );
}
