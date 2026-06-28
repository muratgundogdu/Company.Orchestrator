import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function ExcelWriteDataTableEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const inputArtifactName   = String(config.inputArtifactName   ?? 'report.xlsx');
  const sheetName           = String(config.sheetName           ?? 'Data');
  const sourceVariable      = String(config.sourceVariable      ?? 'sqlResult');
  const startCell           = String(config.startCell           ?? 'A1');
  const includeHeaders      = config.includeHeaders !== false && config.includeHeaders !== 'false';
  const clearExistingData   = config.clearExistingData === true || config.clearExistingData === 'true';
  const createSheetIfMissing = config.createSheetIfMissing !== false && config.createSheetIfMissing !== 'false';

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
          placeholder="report.xlsx"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="inputArtifactName" />
        <p className="text-xs text-gray-400 mt-1">
          Existing workbook artifact to update in place.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Sheet Name *</label>
          <input
            value={sheetName}
            onChange={(e) => set('sheetName', e.target.value)}
            onFocus={(e) => onFocusField(e.currentTarget, 'sheetName')}
            className={`input text-xs ${fieldErrors?.sheetName ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="Customers"
          />
          <FieldMsg errors={fieldErrors} field="sheetName" />
        </div>
        <div>
          <label className="label">Start Cell *</label>
          <input
            value={startCell}
            onChange={(e) => set('startCell', e.target.value.toUpperCase())}
            onFocus={(e) => onFocusField(e.currentTarget, 'startCell')}
            className={`input font-mono text-xs ${fieldErrors?.startCell ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="A1"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="startCell" />
        </div>
      </div>

      <div>
        <label className="label">Source Variable *</label>
        <input
          value={sourceVariable}
          onChange={(e) => set('sourceVariable', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'sourceVariable')}
          className={`input font-mono text-xs ${fieldErrors?.sourceVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="sqlResult"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="sourceVariable" />
        <p className="text-xs text-gray-400 mt-1">
          DataTable JSON from sql.query, excel.read-range, datatable.join, json.parse (table), etc.
        </p>
      </div>

      <div className="space-y-2">
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={includeHeaders}
            onChange={(e) => set('includeHeaders', e.target.checked)}
            className="rounded border-gray-300"
          />
          Include column headers
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={clearExistingData}
            onChange={(e) => set('clearExistingData', e.target.checked)}
            className="rounded border-gray-300"
          />
          Clear existing sheet data before writing
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={createSheetIfMissing}
            onChange={(e) => set('createSheetIfMissing', e.target.checked)}
            className="rounded border-gray-300"
          />
          Create sheet if missing
        </label>
      </div>

      <div className="rounded-lg border border-emerald-200 bg-emerald-50/60 px-2.5 py-2 text-xs text-emerald-900 space-y-1">
        <p>Example: SQL Query → sqlResult → write to report.xlsx sheet Customers at A1.</p>
        <p>Outputs: {'{{rowsWritten}}'}, {'{{columnsWritten}}'}, {'{{outputArtifactName}}'}.</p>
      </div>
    </div>
  );
}
