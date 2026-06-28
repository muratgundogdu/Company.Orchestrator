import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function MailExtractValueEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const sourceVariable = String(config.sourceVariable ?? 'mailBody');
  const outputVariable = String(config.outputVariable ?? 'amount');
  const label          = String(config.label ?? '');
  const pattern        = String(config.pattern ?? '');
  const mode           = pattern.trim() ? 'regex' : 'label';

  function setMode(next: 'label' | 'regex') {
    if (next === 'label') {
      onChange({ ...config, pattern: '' });
    } else {
      onChange({ ...config, label: '' });
    }
  }

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Source Variable *</label>
        <input
          value={sourceVariable}
          onChange={(e) => onChange({ ...config, sourceVariable: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'sourceVariable')}
          className={`input font-mono text-xs ${fieldErrors?.sourceVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="mailBody"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="sourceVariable" />
      </div>

      <div>
        <label className="label">Extraction Mode *</label>
        <div className="grid grid-cols-2 rounded-lg border border-gray-200 overflow-hidden divide-x divide-gray-200">
          <button
            type="button"
            onClick={() => setMode('label')}
            className={`py-2 text-xs font-medium transition-colors ${
              mode === 'label' ? 'bg-brand-primary text-white' : 'bg-white text-content hover:bg-gray-50'
            }`}
          >
            Label
          </button>
          <button
            type="button"
            onClick={() => setMode('regex')}
            className={`py-2 text-xs font-medium transition-colors ${
              mode === 'regex' ? 'bg-brand-primary text-white' : 'bg-white text-content hover:bg-gray-50'
            }`}
          >
            Regex
          </button>
        </div>
        <FieldMsg errors={fieldErrors} field="extraction" />
      </div>

      {mode === 'label' ? (
        <div>
          <label className="label">Label *</label>
          <input
            value={label}
            onChange={(e) => onChange({ ...config, label: e.target.value, pattern: '' })}
            onFocus={(e) => onFocusField(e.currentTarget, 'label')}
            className={`input text-xs ${fieldErrors?.label ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="Tutar"
          />
          <FieldMsg errors={fieldErrors} field="label" />
          <p className="text-xs text-gray-400 mt-1">
            Finds text after the label, e.g. <code className="bg-gray-100 px-0.5 rounded">Tutar: 1.234,56</code>
          </p>
        </div>
      ) : (
        <div>
          <label className="label">Pattern *</label>
          <input
            value={pattern}
            onChange={(e) => onChange({ ...config, pattern: e.target.value, label: '' })}
            onFocus={(e) => onFocusField(e.currentTarget, 'pattern')}
            className={`input font-mono text-xs ${fieldErrors?.pattern ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="Müşteri No\\s*:\\s*(\\d+)"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="pattern" />
          <p className="text-xs text-gray-400 mt-1">
            Use a capture group for the value to extract, e.g. <code className="bg-gray-100 px-0.5 rounded">(\\d+)</code>
          </p>
        </div>
      )}

      <div>
        <label className="label">Output Variable *</label>
        <input
          value={outputVariable}
          onChange={(e) => onChange({ ...config, outputVariable: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
          className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="amount"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputVariable" />
      </div>
    </div>
  );
}
