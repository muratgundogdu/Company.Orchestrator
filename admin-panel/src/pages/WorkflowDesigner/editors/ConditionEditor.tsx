import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

type Operator = 'exists' | '==' | '!=' | '>' | '<';

const OPERATORS: { value: Operator; label: string }[] = [
  { value: 'exists', label: 'Variable Exists' },
  { value: '==',     label: 'Equals'          },
  { value: '!=',     label: 'Not Equals'      },
  { value: '>',      label: 'Greater Than'    },
  { value: '<',      label: 'Less Than'       },
];

export default function ConditionEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const cond     = (config.condition as Record<string, unknown>) ?? {};
  const left     = String(cond.left     ?? '');
  const operator = String(cond.operator ?? '>') as Operator;
  const right    = String(cond.right    ?? '0');

  function patch(k: string, v: unknown) {
    onChange({ ...config, condition: { ...cond, [k]: v } });
  }

  const showRight = operator !== 'exists';

  // Preview string
  const preview = showRight
    ? `${left || '<left>'} ${operator} ${right || '<right>'}`
    : `${left || '<left>'} exists`;

  return (
    <div className="space-y-3">

      {/* Left value */}
      <div>
        <label className="label">Left Value *</label>
        <input
          value={left}
          onChange={(e) => patch('left', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'condition.left')}
          className={`input font-mono text-xs ${fieldErrors?.['condition.left'] ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="{{mailArtifacts_count}}"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="condition.left" />
        {!fieldErrors?.['condition.left'] && (
          <p className="text-xs text-gray-400 mt-1">
            Use the variable picker to insert an upstream value.
          </p>
        )}
      </div>

      {/* Operator */}
      <div>
        <label className="label">Operator</label>
        <select
          value={operator}
          onChange={(e) => patch('operator', e.target.value)}
          className="input"
        >
          {OPERATORS.map((op) => (
            <option key={op.value} value={op.value}>{op.label}</option>
          ))}
        </select>
      </div>

      {/* Right value (hidden for "exists") */}
      {showRight && (
        <div>
          <label className="label">Right Value</label>
          <input
            value={right}
            onChange={(e) => patch('right', e.target.value)}
            onFocus={(e) => onFocusField(e.currentTarget, 'condition.right')}
            className="input font-mono text-xs"
            placeholder="0"
            spellCheck={false}
          />
          <p className="text-xs text-gray-400 mt-1">
            Literal value or <code className="bg-gray-100 px-0.5 rounded">{'{{variable}}'}</code>.
          </p>
        </div>
      )}

      {/* Expression preview */}
      <div className="rounded-lg border border-gray-200 bg-gray-50 px-3 py-2">
        <p className="text-xs text-gray-500 mb-0.5 font-medium">Expression preview</p>
        <code className="text-xs text-blue-700 font-mono font-semibold">{preview}</code>
      </div>

      {/* Routing legend */}
      <div className="rounded-lg border border-gray-200 bg-white px-3 py-2.5 space-y-1.5">
        <p className="text-xs font-semibold text-gray-600 mb-1">Output handles</p>
        <div className="flex items-center gap-2">
          <span className="flex h-3.5 w-3.5 items-center justify-center rounded-full bg-green-600 text-white font-bold" style={{ fontSize: 7 }}>T</span>
          <span className="text-xs text-gray-500">True — drag from green handle</span>
          {fieldErrors?.['branches.true'] && (
            <span className="text-xs text-red-600 font-medium">⚠ {fieldErrors['branches.true']}</span>
          )}
        </div>
        <div className="flex items-center gap-2">
          <span className="flex h-3.5 w-3.5 items-center justify-center rounded-full bg-red-600 text-white font-bold" style={{ fontSize: 7 }}>F</span>
          <span className="text-xs text-gray-500">False — drag from red handle</span>
          {fieldErrors?.['branches.false'] && (
            <span className="text-xs text-red-600 font-medium">⚠ {fieldErrors['branches.false']}</span>
          )}
        </div>
      </div>

    </div>
  );
}
