import type { EditorProps } from './types';

const DELIMITER_OPTIONS = [
  { value: ',', label: 'Comma (,)' },
  { value: ';', label: 'Semicolon (;)' },
  { value: '\t', label: 'Tab' },
  { value: '|', label: 'Pipe (|)' },
  { value: 'custom', label: 'Custom…' },
] as const;

const ENCODING_OPTIONS = ['UTF-8', 'UTF-8-BOM', 'Windows-1252', 'ISO-8859-1'] as const;

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

function DelimiterFields({
  delimiter,
  customDelimiter,
  onChange,
  onFocusField,
  fieldErrors,
}: {
  delimiter: string;
  customDelimiter: string;
  onChange: (delimiter: string, customDelimiter: string) => void;
  onFocusField: EditorProps['onFocusField'];
  fieldErrors?: Record<string, string>;
}) {
  const presetValues = DELIMITER_OPTIONS.map((o) => o.value).filter((v) => v !== 'custom');
  const mode = (presetValues as readonly string[]).includes(delimiter) ? delimiter : 'custom';

  return (
    <div className="grid grid-cols-2 gap-2">
      <div>
        <label className="label">Delimiter *</label>
        <select
          value={mode}
          onChange={(e) => {
            const next = e.target.value;
            if (next === 'custom') {
              onChange(customDelimiter || ',', customDelimiter);
            } else {
              onChange(next, customDelimiter);
            }
          }}
          className={`input text-xs ${fieldErrors?.delimiter ? 'border-red-400 focus:ring-red-400' : ''}`}
        >
          {DELIMITER_OPTIONS.map((opt) => (
            <option key={opt.value} value={opt.value}>{opt.label}</option>
          ))}
        </select>
        <FieldMsg errors={fieldErrors} field="delimiter" />
      </div>
      {mode === 'custom' && (
        <div>
          <label className="label">Custom Delimiter *</label>
          <input
            value={customDelimiter}
            onChange={(e) => onChange(e.target.value, e.target.value)}
            onFocus={(ev) => onFocusField(ev.currentTarget, 'customDelimiter')}
            className={`input font-mono text-xs ${fieldErrors?.customDelimiter ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder=","
            maxLength={1}
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="customDelimiter" />
        </div>
      )}
    </div>
  );
}

export default function CsvReadEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const inputArtifactName = String(config.inputArtifactName ?? 'source-file');
  const delimiter         = String(config.delimiter ?? ',');
  const customDelimiter   = String(config.customDelimiter ?? delimiter);
  const encoding          = String(config.encoding ?? 'UTF-8');
  const outputVariable    = String(config.outputVariable ?? 'csvData');
  const hasHeader         = config.hasHeader !== false && config.hasHeader !== 'false';
  const trimValues        = config.trimValues !== false && config.trimValues !== 'false';
  const includeEmptyRows  = config.includeEmptyRows === true || config.includeEmptyRows === 'true';

  function set(key: string, value: unknown) {
    onChange({ ...config, [key]: value });
  }

  function setDelimiter(nextDelimiter: string, nextCustom: string) {
    onChange({ ...config, delimiter: nextDelimiter, customDelimiter: nextCustom });
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
      </div>

      <DelimiterFields
        delimiter={delimiter}
        customDelimiter={customDelimiter}
        onChange={setDelimiter}
        onFocusField={onFocusField}
        fieldErrors={fieldErrors}
      />

      <div>
        <label className="label">Encoding</label>
        <select
          value={encoding}
          onChange={(e) => set('encoding', e.target.value)}
          className="input text-xs"
        >
          {ENCODING_OPTIONS.map((enc) => (
            <option key={enc} value={enc}>{enc}</option>
          ))}
        </select>
      </div>

      <div>
        <label className="label">Output Variable *</label>
        <input
          value={outputVariable}
          onChange={(e) => set('outputVariable', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
          className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="csvData"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputVariable" />
      </div>

      <div className="space-y-2">
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input type="checkbox" checked={hasHeader} onChange={(e) => set('hasHeader', e.target.checked)} className="rounded border-gray-300" />
          First row contains column headers
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input type="checkbox" checked={trimValues} onChange={(e) => set('trimValues', e.target.checked)} className="rounded border-gray-300" />
          Trim whitespace from values
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input type="checkbox" checked={includeEmptyRows} onChange={(e) => set('includeEmptyRows', e.target.checked)} className="rounded border-gray-300" />
          Include fully empty rows
        </label>
      </div>
    </div>
  );
}

export { DELIMITER_OPTIONS, ENCODING_OPTIONS, DelimiterFields, FieldMsg };
