import { KeyRound } from 'lucide-react';
import type { EditorProps } from './types';
import { useCredentials, credentialOptions } from '../../../hooks/useCredentials';

export function SqlFieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export function SqlConnectionFields({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const { credentials: sqlCredentials } = useCredentials('SqlConnectionString');
  const sqlOptions = credentialOptions(sqlCredentials);
  const connectionString = String(config.connectionString ?? '');
  const connectionName   = String(config.connectionName ?? '');

  return (
    <>
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
        <SqlFieldMsg errors={fieldErrors} field="connectionString" />
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
        <SqlFieldMsg errors={fieldErrors} field="connectionName" />
        <p className="text-xs text-gray-400 mt-1 flex items-center gap-1">
          <KeyRound className="w-3 h-3" />
          Resolved from Credential Vault first, then appsettings ConnectionStrings.
        </p>
      </div>
    </>
  );
}

export type SqlParameterRow = { name: string; value: string; direction?: string };

export function parseParameterRows(value: unknown, includeDirection = false): SqlParameterRow[] {
  if (Array.isArray(value)) {
    if (value.length === 0) {
      return includeDirection
        ? [{ name: '', value: '', direction: 'Input' }]
        : [{ name: '', value: '' }];
    }
    return value.map((item) => {
      const row = item as Record<string, unknown>;
      const base = {
        name:  String(row.name ?? ''),
        value: String(row.value ?? ''),
      };
      return includeDirection
        ? { ...base, direction: String(row.direction ?? 'Input') }
        : base;
    });
  }

  if (value && typeof value === 'object' && !Array.isArray(value)) {
    const entries = Object.entries(value as Record<string, unknown>);
    if (entries.length === 0) {
      return includeDirection
        ? [{ name: '', value: '', direction: 'Input' }]
        : [{ name: '', value: '' }];
    }
    return entries.map(([name, val]) => {
      const base = { name, value: String(val ?? '') };
      return includeDirection ? { ...base, direction: 'Input' } : base;
    });
  }

  return includeDirection
    ? [{ name: '', value: '', direction: 'Input' }]
    : [{ name: '', value: '' }];
}

export function parameterRowsToArray(rows: SqlParameterRow[], includeDirection = false) {
  return rows
    .filter((row) => row.name.trim())
    .map((row) => {
      const item: Record<string, string> = {
        name:  row.name.trim(),
        value: row.value,
      };
      if (includeDirection) item.direction = row.direction?.trim() || 'Input';
      return item;
    });
}

export const PARAMETER_DIRECTIONS = [
  { value: 'Input', label: 'Input' },
  { value: 'Output', label: 'Output' },
  { value: 'InputOutput', label: 'Input/Output' },
  { value: 'ReturnValue', label: 'Return Value' },
] as const;
