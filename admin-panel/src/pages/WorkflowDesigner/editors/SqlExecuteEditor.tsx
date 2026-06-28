import { Plus, Trash2 } from 'lucide-react';
import type { EditorProps } from './types';
import EditableNumberInput from './EditableNumberInput';
import {
  SqlConnectionFields,
  SqlFieldMsg,
  parseParameterRows,
  parameterRowsToArray,
} from './sqlCommon';

export default function SqlExecuteEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const sql            = String(config.sql ?? '');
  const timeoutSeconds = Number(config.timeoutSeconds ?? 300);
  const parameterRows  = parseParameterRows(config.parameters, false);

  return (
    <div className="space-y-3">
      <SqlConnectionFields {...{ config, onChange, onFocusField, fieldErrors }} />

      <div>
        <label className="label">SQL *</label>
        <textarea
          value={sql}
          onChange={(e) => onChange({ ...config, sql: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'sql')}
          className={`input font-mono text-xs min-h-[120px] ${fieldErrors?.sql ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="UPDATE Customers SET Status = @status WHERE CustomerNo = @customerNo"
          spellCheck={false}
        />
        <SqlFieldMsg errors={fieldErrors} field="sql" />
        <p className="text-xs text-gray-400 mt-1">
          INSERT, UPDATE, DELETE, and MERGE only. Use @paramName placeholders — values are never concatenated into SQL.
        </p>
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <p className="text-xs font-medium text-gray-600">Parameters</p>
          <button
            type="button"
            onClick={() =>
              onChange({
                ...config,
                parameters: parameterRowsToArray([...parameterRows, { name: 'customerNo', value: '{{currentRow.CustomerNo}}' }]),
              })
            }
            className="inline-flex items-center gap-1 text-xs text-brand-primary hover:underline"
          >
            <Plus className="w-3 h-3" /> Add parameter
          </button>
        </div>
        {parameterRows.map((row, idx) => (
          <div key={idx} className="grid grid-cols-[1fr_1fr_auto] gap-2 items-center">
            <input
              value={row.name}
              onChange={(e) => {
                const next = [...parameterRows];
                next[idx] = { ...next[idx], name: e.target.value };
                onChange({ ...config, parameters: parameterRowsToArray(next) });
              }}
              onFocus={(e) => onFocusField(e.currentTarget, `parameters.${idx}.name`)}
              className="input font-mono text-xs"
              placeholder="status"
              spellCheck={false}
            />
            <input
              value={row.value}
              onChange={(e) => {
                const next = [...parameterRows];
                next[idx] = { ...next[idx], value: e.target.value };
                onChange({ ...config, parameters: parameterRowsToArray(next) });
              }}
              onFocus={(e) => onFocusField(e.currentTarget, `parameters.${idx}.value`)}
              className="input font-mono text-xs"
              placeholder="Processed  or  {{currentRow.CustomerNo}}"
              spellCheck={false}
            />
            <button
              type="button"
              onClick={() => {
                const next = parameterRows.filter((_, i) => i !== idx);
                onChange({
                  ...config,
                  parameters: parameterRowsToArray(next.length ? next : [{ name: '', value: '' }]),
                });
              }}
              className="p-1 text-gray-400 hover:text-red-600"
              title="Remove parameter"
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </div>
        ))}
        <p className="text-xs text-gray-400">
          Maps to @paramName in SQL. Values support {'{{variable}}'} interpolation.
        </p>
      </div>

      <div>
        <label className="label">Timeout Seconds</label>
        <EditableNumberInput
          value={timeoutSeconds}
          min={1}
          fallback={300}
          onValueChange={(timeoutSeconds) => onChange({ ...config, timeoutSeconds })}
          onFocus={(e) => onFocusField(e.currentTarget, 'timeoutSeconds')}
          className={`input text-xs w-32 ${fieldErrors?.timeoutSeconds ? 'border-red-400 focus:ring-red-400' : ''}`}
        />
        <SqlFieldMsg errors={fieldErrors} field="timeoutSeconds" />
      </div>

      <div className="rounded-lg border border-sky-200 bg-sky-50/60 px-2.5 py-2 text-xs text-sky-900 space-y-1">
        <p>Outputs: {'{{rowsAffected}}'}, {'{{executionSucceeded}}'}, {'{{executionDurationMs}}'}</p>
        <p>Example: UPDATE Orders SET Exported = 1 WHERE OrderNo = @orderNo</p>
      </div>
    </div>
  );
}
