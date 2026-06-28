import { Plus, Trash2 } from 'lucide-react';
import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

type ColumnMapping = { sourceColumn: string; targetColumn: string };

function parseMappings(value: unknown): ColumnMapping[] {
  if (!Array.isArray(value) || value.length === 0) {
    return [{ sourceColumn: 'CustomerName', targetColumn: 'CustomerName' }];
  }
  return value.map((item) => {
    const row = item as Record<string, unknown>;
    return {
      sourceColumn: String(row.sourceColumn ?? ''),
      targetColumn: String(row.targetColumn ?? ''),
    };
  });
}

function columnsToString(value: unknown, fallback: string): string {
  if (Array.isArray(value)) return value.map(String).join(', ');
  if (typeof value === 'string') return value;
  return fallback;
}

export default function DataTableJoinEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const leftVariable   = String(config.leftVariable   ?? 'ordersTable');
  const rightVariable  = String(config.rightVariable  ?? 'customersTable');
  const leftKeyColumns = columnsToString(config.leftKeyColumns, 'CustomerNo');
  const rightKeyColumns = columnsToString(config.rightKeyColumns, 'CustomerNo');
  const joinType       = String(config.joinType       ?? 'left');
  const outputVariable = String(config.outputVariable ?? 'joinedTable');
  const notFoundValue  = String(config.notFoundValue  ?? '');
  const ignoreCase     = config.ignoreCase !== false && config.ignoreCase !== 'false';
  const trimValues     = config.trimValues !== false && config.trimValues !== 'false';
  const mappings       = parseMappings(config.rightColumnsToAdd);

  function setColumns(key: 'leftKeyColumns' | 'rightKeyColumns', raw: string) {
    const cols = raw.split(',').map((s) => s.trim()).filter(Boolean);
    onChange({ ...config, [key]: cols.length === 1 ? cols[0] : cols });
  }

  function setMappings(next: ColumnMapping[]) {
    onChange({ ...config, rightColumnsToAdd: next });
  }

  return (
    <div className="space-y-3">
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Left Variable *</label>
          <input
            value={leftVariable}
            onChange={(e) => onChange({ ...config, leftVariable: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'leftVariable')}
            className={`input font-mono text-xs ${fieldErrors?.leftVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="ordersTable"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="leftVariable" />
        </div>
        <div>
          <label className="label">Right Variable *</label>
          <input
            value={rightVariable}
            onChange={(e) => onChange({ ...config, rightVariable: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'rightVariable')}
            className={`input font-mono text-xs ${fieldErrors?.rightVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="customersTable"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="rightVariable" />
        </div>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Left Key Columns *</label>
          <input
            value={leftKeyColumns}
            onChange={(e) => setColumns('leftKeyColumns', e.target.value)}
            onFocus={(e) => onFocusField(e.currentTarget, 'leftKeyColumns')}
            className={`input font-mono text-xs ${fieldErrors?.leftKeyColumns ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="CustomerNo, Region"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="leftKeyColumns" />
        </div>
        <div>
          <label className="label">Right Key Columns *</label>
          <input
            value={rightKeyColumns}
            onChange={(e) => setColumns('rightKeyColumns', e.target.value)}
            onFocus={(e) => onFocusField(e.currentTarget, 'rightKeyColumns')}
            className={`input font-mono text-xs ${fieldErrors?.rightKeyColumns ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="CustomerNo, Region"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="rightKeyColumns" />
        </div>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Join Type *</label>
          <select
            value={joinType}
            onChange={(e) => onChange({ ...config, joinType: e.target.value })}
            className={`input text-xs ${fieldErrors?.joinType ? 'border-red-400 focus:ring-red-400' : ''}`}
          >
            <option value="left">Left join</option>
            <option value="inner">Inner join</option>
          </select>
          <FieldMsg errors={fieldErrors} field="joinType" />
        </div>
        <div>
          <label className="label">Output Variable *</label>
          <input
            value={outputVariable}
            onChange={(e) => onChange({ ...config, outputVariable: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
            className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="joinedTable"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="outputVariable" />
        </div>
      </div>

      <div>
        <label className="label">Not Found Value</label>
        <input
          value={notFoundValue}
          onChange={(e) => onChange({ ...config, notFoundValue: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'notFoundValue')}
          className="input font-mono text-xs"
          placeholder=""
          spellCheck={false}
        />
        <p className="text-xs text-gray-400 mt-1">Used for left join rows with no matching right row.</p>
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <p className="text-xs font-medium text-gray-600">Right Columns To Add *</p>
          <button
            type="button"
            onClick={() => setMappings([...mappings, { sourceColumn: 'Region', targetColumn: 'CustomerRegion' }])}
            className="inline-flex items-center gap-1 text-xs text-brand-primary hover:underline"
          >
            <Plus className="w-3 h-3" /> Add mapping
          </button>
        </div>
        <FieldMsg errors={fieldErrors} field="rightColumnsToAdd" />
        {mappings.map((mapping, idx) => (
          <div key={idx} className="grid grid-cols-[1fr_1fr_auto] gap-2 items-center">
            <input
              value={mapping.sourceColumn}
              onChange={(e) => {
                const next = [...mappings];
                next[idx] = { ...next[idx], sourceColumn: e.target.value };
                setMappings(next);
              }}
              className={`input font-mono text-xs ${fieldErrors?.[`rightColumnsToAdd.${idx}.sourceColumn`] ? 'border-red-400 focus:ring-red-400' : ''}`}
              placeholder="CustomerName"
              spellCheck={false}
            />
            <input
              value={mapping.targetColumn}
              onChange={(e) => {
                const next = [...mappings];
                next[idx] = { ...next[idx], targetColumn: e.target.value };
                setMappings(next);
              }}
              className={`input font-mono text-xs ${fieldErrors?.[`rightColumnsToAdd.${idx}.targetColumn`] ? 'border-red-400 focus:ring-red-400' : ''}`}
              placeholder="CustomerName"
              spellCheck={false}
            />
            <button
              type="button"
              onClick={() => setMappings(mappings.filter((_, i) => i !== idx))}
              className="p-1 text-gray-400 hover:text-red-600"
              title="Remove mapping"
              disabled={mappings.length <= 1}
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </div>
        ))}
        <p className="text-xs text-gray-400">Source column from right table → target column added to output.</p>
      </div>

      <div className="space-y-2">
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={ignoreCase}
            onChange={(e) => onChange({ ...config, ignoreCase: e.target.checked })}
            className="rounded border-gray-300"
          />
          Ignore case when matching keys
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={trimValues}
            onChange={(e) => onChange({ ...config, trimValues: e.target.checked })}
            className="rounded border-gray-300"
          />
          Trim whitespace before matching
        </label>
      </div>

      <div className="rounded-lg border border-indigo-200 bg-indigo-50/60 px-2.5 py-2 text-xs text-indigo-900 space-y-1">
        <p>Example: Join ordersTable to customersTable on CustomerNo.</p>
        <p>Example: Composite keys CustomerNo + Region for branch lookups.</p>
      </div>
    </div>
  );
}
