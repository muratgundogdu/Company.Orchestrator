import type { EditorProps } from './types';
import { DelimiterFields, ENCODING_OPTIONS, FieldMsg } from './CsvReadEditor';

export default function CsvWriteEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const sourceVariable  = String(config.sourceVariable ?? 'csvData');
  const outputName        = String(config.outputName ?? 'output.csv');
  const delimiter         = String(config.delimiter ?? ',');
  const customDelimiter   = String(config.customDelimiter ?? delimiter);
  const encoding          = String(config.encoding ?? 'UTF-8');
  const includeHeaders    = config.includeHeaders !== false && config.includeHeaders !== 'false';
  const quoteValues       = config.quoteValues !== false && config.quoteValues !== 'false';

  function set(key: string, value: unknown) {
    onChange({ ...config, [key]: value });
  }

  function setDelimiter(nextDelimiter: string, nextCustom: string) {
    onChange({ ...config, delimiter: nextDelimiter, customDelimiter: nextCustom });
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
          placeholder="csvData"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="sourceVariable" />
        <p className="text-xs text-gray-400 mt-1">
          DataTable JSON from csv.read, sql.query, excel.read-range, json.parse (table), etc.
        </p>
      </div>

      <div>
        <label className="label">Output Name *</label>
        <input
          value={outputName}
          onChange={(e) => set('outputName', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputName')}
          className={`input font-mono text-xs ${fieldErrors?.outputName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="output.csv"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputName" />
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

      <div className="space-y-2">
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input type="checkbox" checked={includeHeaders} onChange={(e) => set('includeHeaders', e.target.checked)} className="rounded border-gray-300" />
          Include column headers
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input type="checkbox" checked={quoteValues} onChange={(e) => set('quoteValues', e.target.checked)} className="rounded border-gray-300" />
          Quote values when needed
        </label>
      </div>
    </div>
  );
}
