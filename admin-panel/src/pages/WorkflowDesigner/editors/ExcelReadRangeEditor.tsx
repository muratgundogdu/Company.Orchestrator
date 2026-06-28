import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function ExcelReadRangeEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const inputArtifactName = String(config.inputArtifactName ?? '');
  const sheetName         = String(config.sheetName         ?? 'Data');
  const range             = String(config.range             ?? 'A1:D100');
  const outputVariable    = String(config.outputVariable    ?? 'dataTable');
  const hasHeader         = config.hasHeader !== false && config.hasHeader !== 'false';
  const trimValues        = config.trimValues !== false && config.trimValues !== 'false';
  const includeEmptyRows  = config.includeEmptyRows === true || config.includeEmptyRows === 'true';

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Input Artifact Name *</label>
        <input
          value={inputArtifactName}
          onChange={(e) => onChange({ ...config, inputArtifactName: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'inputArtifactName')}
          className={`input font-mono text-xs ${fieldErrors?.inputArtifactName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="source-file"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="inputArtifactName" />
        {!fieldErrors?.inputArtifactName && (
          <p className="text-xs text-gray-400 mt-1">
            Artifact name or variable, e.g.{' '}
            <code className="bg-gray-100 px-0.5 rounded">source-file</code> or{' '}
            <code className="bg-gray-100 px-0.5 rounded">{'{{mailArtifacts_0}}'}</code>.
          </p>
        )}
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Sheet Name *</label>
          <input
            value={sheetName}
            onChange={(e) => onChange({ ...config, sheetName: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'sheetName')}
            className={`input text-xs ${fieldErrors?.sheetName ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="Data"
          />
          <FieldMsg errors={fieldErrors} field="sheetName" />
        </div>
        <div>
          <label className="label">Range</label>
          <input
            value={range}
            onChange={(e) => onChange({ ...config, range: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'range')}
            className="input font-mono text-xs"
            placeholder="A1:D100"
            spellCheck={false}
          />
          <p className="text-xs text-gray-400 mt-1">Leave blank to use the sheet used range.</p>
        </div>
      </div>

      <div>
        <label className="label">Output Variable *</label>
        <input
          value={outputVariable}
          onChange={(e) => onChange({ ...config, outputVariable: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
          className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="dataTable"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputVariable" />
        {!fieldErrors?.outputVariable && outputVariable && (
          <p className="text-xs text-gray-400 mt-1">
            Exposes{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}_count}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}_first}}`}</code>, …
          </p>
        )}
      </div>

      <div className="rounded-lg border border-emerald-200 bg-emerald-50/60 px-2.5 py-2 text-xs text-emerald-900 space-y-1">
        <p>Example: Read customer rows into a DataTable for ForEach Row or Group By.</p>
        <p>Example: Use <code className="bg-white/70 px-0.5 rounded">{`{{${outputVariable || 'dataTable'}_count}}`}</code> in Mail Send body.</p>
      </div>

      <div className="space-y-2">
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={hasHeader}
            onChange={(e) => onChange({ ...config, hasHeader: e.target.checked })}
            className="rounded border-gray-300"
          />
          First row contains column headers
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={trimValues}
            onChange={(e) => onChange({ ...config, trimValues: e.target.checked })}
            className="rounded border-gray-300"
          />
          Trim whitespace from cell values
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={includeEmptyRows}
            onChange={(e) => onChange({ ...config, includeEmptyRows: e.target.checked })}
            className="rounded border-gray-300"
          />
          Include fully empty rows
        </label>
      </div>
    </div>
  );
}
