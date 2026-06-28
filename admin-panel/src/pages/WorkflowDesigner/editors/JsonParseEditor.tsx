import type { EditorProps } from './types';

const OUTPUT_MODES = [
  { value: 'value', label: 'Value — single scalar as string' },
  { value: 'json',  label: 'JSON — object or array as JSON string' },
  { value: 'table', label: 'Table — array of objects as DataTable' },
] as const;

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function JsonParseEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const sourceVariable = String(config.sourceVariable ?? 'apiResult_body');
  const path           = String(config.path           ?? '$[0].name');
  const outputVariable = String(config.outputVariable ?? 'firstUserName');
  const outputMode     = String(config.outputMode     ?? 'value');

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
          placeholder="apiResult_body"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="sourceVariable" />
        <p className="text-xs text-gray-400 mt-1">
          Workflow variable containing a JSON string (e.g. from http.request body).
        </p>
      </div>

      <div>
        <label className="label">JSON Path *</label>
        <input
          value={path}
          onChange={(e) => set('path', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'path')}
          className={`input font-mono text-xs ${fieldErrors?.path ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="$[0].name"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="path" />
        <p className="text-xs text-gray-400 mt-1">
          Examples: $.name, $.data.id, $[0].name, $.items[0].amount, $.items[*].id
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
            {OUTPUT_MODES.map((mode) => (
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
            placeholder="firstUserName"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="outputVariable" />
        </div>
      </div>

      <div className="rounded-lg border border-violet-200 bg-violet-50/60 px-2.5 py-2 text-xs text-violet-900 space-y-1">
        <p>Example: apiResult_body + $[0].name → firstUserName (value mode).</p>
        <p>Example: $.items[*].id in json mode returns a JSON array of ids.</p>
        <p>Example: $ in table mode converts a root JSON array into a DataTable.</p>
      </div>
    </div>
  );
}
