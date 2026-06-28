import type { EditorProps } from './types';

const OPERATIONS = [
  { value: 'count', label: 'Count rows' },
  { value: 'countNonEmpty', label: 'Count non-empty values' },
  { value: 'countDistinct', label: 'Count distinct values' },
  { value: 'sum', label: 'Sum' },
  { value: 'average', label: 'Average' },
  { value: 'min', label: 'Minimum' },
  { value: 'max', label: 'Maximum' },
] as const;

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function DataTableAggregateEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const sourceVariable = String(config.sourceVariable ?? 'dataTable');
  const operation      = String(config.operation      ?? 'sum');
  const column         = String(config.column         ?? 'Amount');
  const outputVariable = String(config.outputVariable ?? 'totalAmount');
  const ignoreEmpty    = config.ignoreEmpty !== false && config.ignoreEmpty !== 'false';
  const columnRequired = operation !== 'count';

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
          placeholder="dataTable"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="sourceVariable" />
        {!fieldErrors?.sourceVariable && (
          <p className="text-xs text-gray-400 mt-1">
            JSON array from excel.read-range or mail.extract-table (tableJson).
          </p>
        )}
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Operation *</label>
          <select
            value={operation}
            onChange={(e) => set('operation', e.target.value)}
            className={`input text-xs ${fieldErrors?.operation ? 'border-red-400 focus:ring-red-400' : ''}`}
          >
            {OPERATIONS.map((op) => (
              <option key={op.value} value={op.value}>{op.label}</option>
            ))}
          </select>
          <FieldMsg errors={fieldErrors} field="operation" />
        </div>
        <div>
          <label className="label">Column{columnRequired ? ' *' : ''}</label>
          <input
            value={column}
            onChange={(e) => set('column', e.target.value)}
            onFocus={(e) => onFocusField(e.currentTarget, 'column')}
            className={`input font-mono text-xs ${fieldErrors?.column ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="Amount"
            spellCheck={false}
            disabled={!columnRequired}
          />
          <FieldMsg errors={fieldErrors} field="column" />
          {!columnRequired && (
            <p className="text-xs text-gray-400 mt-1">Optional for count — uses total row count.</p>
          )}
        </div>
      </div>

      <div>
        <label className="label">Output Variable *</label>
        <input
          value={outputVariable}
          onChange={(e) => set('outputVariable', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
          className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="totalAmount"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputVariable" />
        {outputVariable && !fieldErrors?.outputVariable && (
          <p className="text-xs text-gray-400 mt-1">
            Exposes{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}_sourceCount}}`}</code>, …
          </p>
        )}
      </div>

      <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
        <input
          type="checkbox"
          checked={ignoreEmpty}
          onChange={(e) => set('ignoreEmpty', e.target.checked)}
          className="rounded border-gray-300"
        />
        Ignore empty/null values when aggregating
      </label>

      <div className="rounded-lg border border-violet-200 bg-violet-50/60 px-2.5 py-2 text-xs text-violet-900 space-y-1">
        <p>Example: Sum Amount column from dataTable → totalAmount.</p>
        <p>Example: Count distinct CustomerNo values.</p>
      </div>
    </div>
  );
}
