import { Plus, Trash2, KeyRound } from 'lucide-react';
import type { EditorProps } from './types';
import EditableNumberInput from './EditableNumberInput';
import { useCredentials, credentialOptions } from '../../../hooks/useCredentials';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

type KeyValuePair = { key: string; value: string };

function parseKeyValueMap(value: unknown): KeyValuePair[] {
  if (value && typeof value === 'object' && !Array.isArray(value)) {
    const entries = Object.entries(value as Record<string, unknown>);
    if (entries.length === 0) return [{ key: '', value: '' }];
    return entries.map(([key, val]) => ({ key, value: String(val ?? '') }));
  }
  return [{ key: '', value: '' }];
}

function keyValueMapToObject(pairs: KeyValuePair[]): Record<string, string> {
  const result: Record<string, string> = {};
  for (const pair of pairs) {
    const key = pair.key.trim();
    if (key) result[key] = pair.value;
  }
  return result;
}

export default function SqlQueryEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const { credentials: sqlCredentials } = useCredentials('SqlConnectionString');
  const sqlOptions = credentialOptions(sqlCredentials);

  const connectionString = String(config.connectionString ?? '');
  const connectionName   = String(config.connectionName   ?? '');
  const query            = String(config.query            ?? 'SELECT TOP 3 CustomerNo, CustomerName FROM Customers');
  const outputVariable   = String(config.outputVariable   ?? 'sqlResult');
  const timeoutSeconds   = Number(config.timeoutSeconds   ?? 60);
  const parameterPairs   = parseKeyValueMap(config.parameters);

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Connection String</label>
        <input
          value={connectionString}
          onChange={(e) => onChange({ ...config, connectionString: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'connectionString')}
          className={`input font-mono text-xs ${fieldErrors?.connectionString ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="Server=...;Database=...;Trusted_Connection=True;"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="connectionString" />
        <p className="text-xs text-gray-400 mt-1">
          Direct connection string takes priority over Connection Name.
        </p>
      </div>

      <div>
        <label className="label">Connection Name</label>
        <select
          value={connectionName}
          onChange={(e) => onChange({ ...config, connectionName: e.target.value })}
          className={`input text-xs mb-2 ${fieldErrors?.connectionName ? 'border-red-400 focus:ring-red-400' : ''}`}
        >
          <option value="">— Select vault credential or type below —</option>
          {sqlOptions.map((opt) => (
            <option key={opt.value} value={opt.value}>{opt.label}</option>
          ))}
        </select>
        <input
          value={connectionName}
          onChange={(e) => onChange({ ...config, connectionName: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'connectionName')}
          className={`input font-mono text-xs ${fieldErrors?.connectionName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="ReportingDb"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="connectionName" />
        <p className="text-xs text-gray-400 mt-1 flex items-center gap-1">
          <KeyRound className="w-3 h-3" />
          Resolved from Credential Vault first, then appsettings ConnectionStrings.
        </p>
      </div>

      <div>
        <label className="label">Query *</label>
        <textarea
          value={query}
          onChange={(e) => onChange({ ...config, query: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'query')}
          className={`input font-mono text-xs min-h-[120px] ${fieldErrors?.query ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="SELECT TOP 10 CustomerNo, CustomerName FROM Customers WHERE IsActive = 1"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="query" />
        <p className="text-xs text-gray-400 mt-1">
          Only SELECT queries are allowed. Use @paramName placeholders for parameters.
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
                parameters: keyValueMapToObject([...parameterPairs, { key: 'customerNo', value: '{{currentRow.CustomerNo}}' }]),
              })
            }
            className="inline-flex items-center gap-1 text-xs text-brand-primary hover:underline"
          >
            <Plus className="w-3 h-3" /> Add parameter
          </button>
        </div>
        {parameterPairs.map((pair, idx) => (
          <div key={idx} className="grid grid-cols-[1fr_1fr_auto] gap-2 items-center">
            <input
              value={pair.key}
              onChange={(e) => {
                const next = [...parameterPairs];
                next[idx] = { ...next[idx], key: e.target.value };
                onChange({ ...config, parameters: keyValueMapToObject(next) });
              }}
              onFocus={(e) => onFocusField(e.currentTarget, `parameters.${idx}.key`)}
              className="input font-mono text-xs"
              placeholder="customerNo"
              spellCheck={false}
            />
            <input
              value={pair.value}
              onChange={(e) => {
                const next = [...parameterPairs];
                next[idx] = { ...next[idx], value: e.target.value };
                onChange({ ...config, parameters: keyValueMapToObject(next) });
              }}
              onFocus={(e) => onFocusField(e.currentTarget, `parameters.${idx}.value`)}
              className="input font-mono text-xs"
              placeholder="{{currentRow.CustomerNo}}"
              spellCheck={false}
            />
            <button
              type="button"
              onClick={() => {
                const next = parameterPairs.filter((_, i) => i !== idx);
                onChange({ ...config, parameters: keyValueMapToObject(next.length ? next : [{ key: '', value: '' }]) });
              }}
              className="p-1 text-gray-400 hover:text-red-600"
              title="Remove parameter"
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </div>
        ))}
        <p className="text-xs text-gray-400">
          Maps to @paramName in the query. Values support {'{{variable}}'} interpolation.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Timeout Seconds *</label>
          <EditableNumberInput
            value={timeoutSeconds}
            min={1}
            fallback={60}
            onValueChange={(timeoutSeconds) => onChange({ ...config, timeoutSeconds })}
            onFocus={(e) => onFocusField(e.currentTarget, 'timeoutSeconds')}
            className={`input text-xs ${fieldErrors?.timeoutSeconds ? 'border-red-400 focus:ring-red-400' : ''}`}
          />
          <FieldMsg errors={fieldErrors} field="timeoutSeconds" />
        </div>
        <div>
          <label className="label">Output Variable *</label>
          <input
            value={outputVariable}
            onChange={(e) => onChange({ ...config, outputVariable: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
            className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="sqlResult"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="outputVariable" />
        </div>
      </div>

      <div className="rounded-lg border border-emerald-200 bg-emerald-50/60 px-2.5 py-2 text-xs text-emerald-900 space-y-1">
        <p>Example: SELECT * FROM Customers WHERE CustomerNo = @customerNo</p>
        <p>Example: Use sqlResult in foreach.row or datatable.join downstream.</p>
      </div>
    </div>
  );
}
