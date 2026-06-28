import { Plus, Trash2 } from 'lucide-react';
import type { EditorProps } from './types';
import EditableNumberInput from './EditableNumberInput';
import {
  SqlConnectionFields,
  SqlFieldMsg,
  PARAMETER_DIRECTIONS,
  parseParameterRows,
  parameterRowsToArray,
} from './sqlCommon';

export default function SqlStoredProcedureEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const procedureName  = String(config.procedureName ?? '');
  const outputVariable = String(config.outputVariable ?? 'procResult');
  const timeoutSeconds = Number(config.timeoutSeconds ?? 300);
  const parameterRows  = parseParameterRows(config.parameters, true);

  return (
    <div className="space-y-3">
      <SqlConnectionFields {...{ config, onChange, onFocusField, fieldErrors }} />

      <div>
        <label className="label">Procedure Name *</label>
        <input
          value={procedureName}
          onChange={(e) => onChange({ ...config, procedureName: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'procedureName')}
          className={`input font-mono text-xs ${fieldErrors?.procedureName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="usp_ProcessCustomer"
          spellCheck={false}
        />
        <SqlFieldMsg errors={fieldErrors} field="procedureName" />
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <p className="text-xs font-medium text-gray-600">Parameters</p>
          <button
            type="button"
            onClick={() =>
              onChange({
                ...config,
                parameters: parameterRowsToArray(
                  [...parameterRows, { name: 'CustomerNo', value: '{{currentRow.CustomerNo}}', direction: 'Input' }],
                  true,
                ),
              })
            }
            className="inline-flex items-center gap-1 text-xs text-brand-primary hover:underline"
          >
            <Plus className="w-3 h-3" /> Add parameter
          </button>
        </div>
        {parameterRows.map((row, idx) => (
          <div key={idx} className="grid grid-cols-[1fr_1fr_120px_auto] gap-2 items-center">
            <input
              value={row.name}
              onChange={(e) => {
                const next = [...parameterRows];
                next[idx] = { ...next[idx], name: e.target.value };
                onChange({ ...config, parameters: parameterRowsToArray(next, true) });
              }}
              onFocus={(e) => onFocusField(e.currentTarget, `parameters.${idx}.name`)}
              className="input font-mono text-xs"
              placeholder="CustomerNo"
              spellCheck={false}
            />
            <input
              value={row.value}
              onChange={(e) => {
                const next = [...parameterRows];
                next[idx] = { ...next[idx], value: e.target.value };
                onChange({ ...config, parameters: parameterRowsToArray(next, true) });
              }}
              onFocus={(e) => onFocusField(e.currentTarget, `parameters.${idx}.value`)}
              className="input font-mono text-xs"
              placeholder="{{currentRow.CustomerNo}}"
              spellCheck={false}
            />
            <select
              value={row.direction ?? 'Input'}
              onChange={(e) => {
                const next = [...parameterRows];
                next[idx] = { ...next[idx], direction: e.target.value };
                onChange({ ...config, parameters: parameterRowsToArray(next, true) });
              }}
              className="input text-xs"
            >
              {PARAMETER_DIRECTIONS.map((d) => (
                <option key={d.value} value={d.value}>{d.label}</option>
              ))}
            </select>
            <button
              type="button"
              onClick={() => {
                const next = parameterRows.filter((_, i) => i !== idx);
                onChange({
                  ...config,
                  parameters: parameterRowsToArray(
                    next.length ? next : [{ name: '', value: '', direction: 'Input' }],
                    true,
                  ),
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
          Output parameters expose {'{{outputVariable_ParamName}}'} after execution.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Output Variable *</label>
          <input
            value={outputVariable}
            onChange={(e) => onChange({ ...config, outputVariable: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
            className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="procResult"
            spellCheck={false}
          />
          <SqlFieldMsg errors={fieldErrors} field="outputVariable" />
        </div>
        <div>
          <label className="label">Timeout Seconds</label>
          <EditableNumberInput
            value={timeoutSeconds}
            min={1}
            fallback={300}
            onValueChange={(timeoutSeconds) => onChange({ ...config, timeoutSeconds })}
            onFocus={(e) => onFocusField(e.currentTarget, 'timeoutSeconds')}
            className={`input text-xs ${fieldErrors?.timeoutSeconds ? 'border-red-400 focus:ring-red-400' : ''}`}
          />
          <SqlFieldMsg errors={fieldErrors} field="timeoutSeconds" />
        </div>
      </div>

      {!fieldErrors?.outputVariable && outputVariable && (
        <p className="text-xs text-gray-400">
          Exposes {'{{' + outputVariable + '_returnValue}}'}, {'{{' + outputVariable + '_resultCount}}'}, result rows, and output parameters.
        </p>
      )}

      <div className="rounded-lg border border-indigo-200 bg-indigo-50/60 px-2.5 py-2 text-xs text-indigo-900 space-y-1">
        <p>First result set is stored as a DataTable JSON array (same as SQL Query).</p>
        <p>Example output: {'{{procResult_TotalAmount}}'}, {'{{procResult_returnValue}}'}</p>
      </div>
    </div>
  );
}
