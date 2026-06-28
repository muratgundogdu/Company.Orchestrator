import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function JsonWriteFileEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const sourceVariable = String(config.sourceVariable ?? 'jsonData');
  const outputName     = String(config.outputName ?? 'output.json');
  const prettyPrint    = config.prettyPrint !== false && config.prettyPrint !== 'false';

  function set(key: string, value: unknown) {
    onChange({ ...config, [key]: value });
  }

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Source Variable *</label>
        <input
          value={sourceVariable}
          onChange={(e) => set('sourceVariable', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'sourceVariable')}
          className={`input font-mono text-xs ${fieldErrors?.sourceVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="jsonData"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="sourceVariable" />
        <p className="text-xs text-gray-400 mt-1">
          JSON string, DataTable array, or plain text (e.g. apiResult_body, sqlResult).
        </p>
      </div>

      <div>
        <label className="label">Output Name *</label>
        <input
          value={outputName}
          onChange={(e) => set('outputName', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputName')}
          className={`input font-mono text-xs ${fieldErrors?.outputName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="output.json"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputName" />
      </div>

      <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
        <input
          type="checkbox"
          checked={prettyPrint}
          onChange={(e) => set('prettyPrint', e.target.checked)}
          className="rounded border-gray-300"
        />
        Pretty print JSON (indented)
      </label>

      <div className="rounded-lg border border-indigo-200 bg-indigo-50/60 px-2.5 py-2 text-xs text-indigo-900 space-y-1">
        <p>Example: apiResult_body → api-response.json attachment.</p>
        <p>Example: sqlResult DataTable → sql-result.json archive file.</p>
      </div>
    </div>
  );
}
