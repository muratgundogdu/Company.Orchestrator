import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

type ExtractMode = 'cell' | 'headerCell' | 'lookup' | 'tableJson';

export default function MailExtractTableEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const sourceVariable = String(config.sourceVariable ?? 'mailBody');
  const outputVariable = String(config.outputVariable ?? 'tableValue');
  const mode           = (String(config.mode ?? 'cell') as ExtractMode);
  const tableIndex     = Number(config.tableIndex ?? 0);
  const rowIndex       = Number(config.rowIndex ?? 1);
  const columnIndex    = Number(config.columnIndex ?? 1);
  const lookupColumn   = String(config.lookupColumn ?? '');
  const lookupValue    = String(config.lookupValue ?? '');
  const returnColumn   = String(config.returnColumn ?? '');
  const ignoreCase     = config.ignoreCase !== false && config.ignoreCase !== 'false';

  const showRowColumn = mode === 'cell' || mode === 'headerCell';
  const showReturnCol = mode === 'headerCell' || mode === 'lookup';
  const showLookup    = mode === 'lookup';

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

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Table Index</label>
          <input
            type="number"
            min={0}
            value={tableIndex}
            onChange={(e) => onChange({ ...config, tableIndex: parseInt(e.target.value, 10) || 0 })}
            className={`input text-xs ${fieldErrors?.tableIndex ? 'border-red-400 focus:ring-red-400' : ''}`}
          />
          <FieldMsg errors={fieldErrors} field="tableIndex" />
        </div>
        <div>
          <label className="label">Mode *</label>
          <select
            value={mode}
            onChange={(e) => onChange({ ...config, mode: e.target.value })}
            className={`input text-xs ${fieldErrors?.mode ? 'border-red-400 focus:ring-red-400' : ''}`}
          >
            <option value="cell">Cell</option>
            <option value="headerCell">Header Cell</option>
            <option value="lookup">Lookup</option>
            <option value="tableJson">Table JSON</option>
          </select>
          <FieldMsg errors={fieldErrors} field="mode" />
        </div>
      </div>

      {showRowColumn && (
        <div className="grid grid-cols-2 gap-2">
          <div>
            <label className="label">Row Index *</label>
            <input
              type="number"
              min={0}
              value={rowIndex}
              onChange={(e) => onChange({ ...config, rowIndex: parseInt(e.target.value, 10) || 0 })}
              className={`input text-xs ${fieldErrors?.rowIndex ? 'border-red-400 focus:ring-red-400' : ''}`}
            />
            <FieldMsg errors={fieldErrors} field="rowIndex" />
            <p className="text-xs text-gray-400 mt-1">0 = header row</p>
          </div>
          {mode === 'cell' && (
            <div>
              <label className="label">Column Index *</label>
              <input
                type="number"
                min={0}
                value={columnIndex}
                onChange={(e) => onChange({ ...config, columnIndex: parseInt(e.target.value, 10) || 0 })}
                className={`input text-xs ${fieldErrors?.columnIndex ? 'border-red-400 focus:ring-red-400' : ''}`}
              />
              <FieldMsg errors={fieldErrors} field="columnIndex" />
            </div>
          )}
        </div>
      )}

      {showReturnCol && (
        <div>
          <label className="label">Return Column *</label>
          <input
            value={returnColumn}
            onChange={(e) => onChange({ ...config, returnColumn: e.target.value })}
            className={`input text-xs ${fieldErrors?.returnColumn ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="Amount"
          />
          <FieldMsg errors={fieldErrors} field="returnColumn" />
        </div>
      )}

      {showLookup && (
        <>
          <div>
            <label className="label">Lookup Column *</label>
            <input
              value={lookupColumn}
              onChange={(e) => onChange({ ...config, lookupColumn: e.target.value })}
              className={`input text-xs ${fieldErrors?.lookupColumn ? 'border-red-400 focus:ring-red-400' : ''}`}
              placeholder="CustomerNo"
            />
            <FieldMsg errors={fieldErrors} field="lookupColumn" />
          </div>
          <div>
            <label className="label">Lookup Value *</label>
            <input
              value={lookupValue}
              onChange={(e) => onChange({ ...config, lookupValue: e.target.value })}
              onFocus={(e) => onFocusField(e.currentTarget, 'lookupValue')}
              className={`input font-mono text-xs ${fieldErrors?.lookupValue ? 'border-red-400 focus:ring-red-400' : ''}`}
              placeholder="1001"
              spellCheck={false}
            />
            <FieldMsg errors={fieldErrors} field="lookupValue" />
          </div>
        </>
      )}

      <div>
        <label className="label">Output Variable *</label>
        <input
          value={outputVariable}
          onChange={(e) => onChange({ ...config, outputVariable: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
          className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="tableValue"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputVariable" />
      </div>

      <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
        <input
          type="checkbox"
          checked={ignoreCase}
          onChange={(e) => onChange({ ...config, ignoreCase: e.target.checked })}
          className="rounded border-gray-300"
        />
        Ignore case when matching headers and lookup values
      </label>
    </div>
  );
}
