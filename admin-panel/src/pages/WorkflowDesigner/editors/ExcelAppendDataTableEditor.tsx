import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function ExcelAppendDataTableEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const inputArtifactName     = String(config.inputArtifactName     ?? 'history.xlsx');
  const sheetName             = String(config.sheetName             ?? 'History');
  const sourceVariable        = String(config.sourceVariable        ?? 'sqlResult');
  const createSheetIfMissing  = config.createSheetIfMissing !== false && config.createSheetIfMissing !== 'false';
  const includeHeadersIfEmpty = config.includeHeadersIfEmpty !== false && config.includeHeadersIfEmpty !== 'false';
  const matchColumnsByName    = config.matchColumnsByName !== false && config.matchColumnsByName !== 'false';

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
          placeholder="history.xlsx"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="inputArtifactName" />
        <p className="text-xs text-gray-400 mt-1">
          Existing workbook artifact — new rows are appended after the last used row.
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
            placeholder="History"
          />
          <FieldMsg errors={fieldErrors} field="sheetName" />
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
        </div>
      </div>

      <div className="space-y-2">
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={createSheetIfMissing}
            onChange={(e) => set('createSheetIfMissing', e.target.checked)}
            className="rounded border-gray-300"
          />
          Create sheet if missing
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={includeHeadersIfEmpty}
            onChange={(e) => set('includeHeadersIfEmpty', e.target.checked)}
            className="rounded border-gray-300"
          />
          Include headers when sheet is empty
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={matchColumnsByName}
            onChange={(e) => set('matchColumnsByName', e.target.checked)}
            className="rounded border-gray-300"
          />
          Match columns by header name
        </label>
      </div>

      <div className="rounded-lg border border-teal-200 bg-teal-50/60 px-2.5 py-2 text-xs text-teal-900 space-y-1">
        <p>Example: Append daily SQL results to history.xlsx without overwriting prior rows.</p>
        <p>Outputs: {'{{rowsAppended}}'}, {'{{columnsWritten}}'}, {'{{lastRowAfterAppend}}'}.</p>
      </div>
    </div>
  );
}
