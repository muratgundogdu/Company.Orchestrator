import type { EditorProps } from './types';

const VALUE_TYPES = [
  { value: 'string', label: 'String' },
  { value: 'number', label: 'Number' },
  { value: 'boolean', label: 'Boolean' },
  { value: 'json', label: 'JSON' },
] as const;

const MODES = [
  { value: 'literal', label: 'Literal' },
  { value: 'expression', label: 'Expression' },
] as const;

const EXPRESSION_EXAMPLES = [
  'toNumber({{currentRow.Amount}}) * 1.20',
  '"Report_" + today("yyyyMMdd") + ".xlsx"',
  'toUpper({{currentRow.CustomerName}})',
  '{{firstName}} + " " + {{lastName}}',
  'toNumber({{amount}}) > 10000',
] as const;

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function SetVariableEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const variableName = String(config.variableName ?? '');
  const value        = String(config.value ?? '');
  const valueType    = String(config.valueType ?? 'string');
  const mode         = String(config.mode ?? 'literal');
  const isExpression = mode === 'expression';

  function set(key: string, v: unknown) {
    onChange({ ...config, [key]: v });
  }

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Variable Name *</label>
        <input
          value={variableName}
          onChange={(e) => set('variableName', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'variableName')}
          className={`input font-mono text-xs ${fieldErrors?.variableName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="customerName"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="variableName" />
        {!fieldErrors?.variableName && variableName && (
          <p className="text-xs text-gray-400 mt-1">
            Downstream steps can use{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${variableName.replace(/^\{\{|\}\}$/g, '')}}}`}</code>
          </p>
        )}
      </div>

      <div>
        <label className="label">Mode *</label>
        <select
          value={mode}
          onChange={(e) => set('mode', e.target.value)}
          className={`input text-xs ${fieldErrors?.mode ? 'border-red-400 focus:ring-red-400' : ''}`}
        >
          {MODES.map((m) => (
            <option key={m.value} value={m.value}>{m.label}</option>
          ))}
        </select>
        <FieldMsg errors={fieldErrors} field="mode" />
        <p className="text-xs text-gray-400 mt-1">
          {isExpression
            ? 'Evaluate math, comparisons, string concat, and helper functions.'
            : 'Set a literal value with {{variable}} interpolation.'}
        </p>
      </div>

      <div>
        <label className="label">Value Type *</label>
        <select
          value={valueType}
          onChange={(e) => set('valueType', e.target.value)}
          className={`input text-xs ${fieldErrors?.valueType ? 'border-red-400 focus:ring-red-400' : ''}`}
        >
          {VALUE_TYPES.map((t) => (
            <option key={t.value} value={t.value}>{t.label}</option>
          ))}
        </select>
        <FieldMsg errors={fieldErrors} field="valueType" />
      </div>

      <div>
        <label className="label">{isExpression ? 'Expression *' : 'Value *'}</label>
        <textarea
          value={value}
          onChange={(e) => set('value', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'value')}
          className={`input font-mono text-xs min-h-[88px] resize-y ${fieldErrors?.value ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder={isExpression ? 'toNumber({{amount}}) * 1.20' : 'Murat  or  Report {{today}}'}
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="value" />
        {!isExpression && (
          <p className="text-xs text-gray-400 mt-1">
            Supports <code className="bg-gray-100 px-0.5 rounded">{'{{variable}}'}</code> interpolation before type conversion.
          </p>
        )}
        {isExpression && (
          <div className="rounded-lg border border-purple-200 bg-purple-50/60 px-2.5 py-2 text-xs text-purple-900 space-y-1 mt-2">
            <p className="font-medium">Expression examples</p>
            {EXPRESSION_EXAMPLES.map((example) => (
              <code key={example} className="block font-mono text-[11px] leading-relaxed">{example}</code>
            ))}
            <p className="text-purple-800/80 pt-1">
              Use the variable picker while the expression field is focused to insert{' '}
              <code className="bg-white/70 px-0.5 rounded">{'{{variable}}'}</code> tokens.
            </p>
          </div>
        )}
      </div>
    </div>
  );
}
