import type { EditorProps } from './types';

export const JSON_OUTPUT_MODES = [
  { value: 'value', label: 'Value — single scalar as string' },
  { value: 'json',  label: 'JSON — object or array as JSON string' },
  { value: 'table', label: 'Table — array of objects as DataTable' },
] as const;

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function JsonReadFileEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const inputArtifactName = String(config.inputArtifactName ?? 'source-file');
  const path              = String(config.path ?? '$.items');
  const outputVariable    = String(config.outputVariable ?? 'jsonData');
  const outputMode        = String(config.outputMode ?? 'json');

  function set(key: string, value: unknown) {
    onChange({ ...config, [key]: value });
  }

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Input Artifact Name *</label>
        <input
          value={inputArtifactName}
          onChange={(e) => set('inputArtifactName', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'inputArtifactName')}
          className={`input font-mono text-xs ${fieldErrors?.inputArtifactName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="source-file"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="inputArtifactName" />
        <p className="text-xs text-gray-400 mt-1">
          JSON file artifact from folder.read-file or a prior step.
        </p>
      </div>

      <div>
        <label className="label">JSON Path</label>
        <input
          value={path}
          onChange={(e) => set('path', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'path')}
          className="input font-mono text-xs"
          placeholder="$.items"
          spellCheck={false}
        />
        <p className="text-xs text-gray-400 mt-1">
          Leave empty to use the whole JSON document. Examples: $.items, $[0].name, $.data[*].id
        </p>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Output Mode *</label>
          <select
            value={outputMode}
            onChange={(e) => set('outputMode', e.target.value)}
            className={`input text-xs ${fieldErrors?.outputMode ? 'border-red-400 focus:ring-red-400' : ''}`}
          >
            {JSON_OUTPUT_MODES.map((mode) => (
              <option key={mode.value} value={mode.value}>{mode.label}</option>
            ))}
          </select>
          <FieldMsg errors={fieldErrors} field="outputMode" />
        </div>
        <div>
          <label className="label">Output Variable *</label>
          <input
            value={outputVariable}
            onChange={(e) => set('outputVariable', e.target.value)}
            onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
            className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="jsonData"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="outputVariable" />
        </div>
      </div>

      <div className="rounded-lg border border-violet-200 bg-violet-50/60 px-2.5 py-2 text-xs text-violet-900 space-y-1">
        <p>Example: path=$.items + table mode → DataTable for For Each Row or Mail Send.</p>
        <p>Example: empty path + json mode → whole file as JSON string.</p>
      </div>
    </div>
  );
}
