import { memo } from 'react';
import { Handle, Position, type NodeProps } from 'reactflow';
import type { WorkflowNodeData } from './types';
import { useValidation } from './ValidationContext';

const W = 164;
const H = 84;

function ConditionNode({ id, data, selected }: NodeProps<WorkflowNodeData>) {
  const { errorNodeIds, warningNodeIds } = useValidation();
  const hasError   = errorNodeIds.has(id);
  const hasWarning = !hasError && warningNodeIds.has(id);

  const stroke = selected
    ? '#2563eb'
    : hasError   ? '#dc2626'
    : hasWarning ? '#d97706'
    : '#c2410c';

  const shadowFilter = selected
    ? 'drop-shadow(0 0 6px rgba(37,99,235,0.5))'
    : hasError   ? 'drop-shadow(0 0 6px rgba(220,38,38,0.45))'
    : hasWarning ? 'drop-shadow(0 0 6px rgba(217,119,6,0.45))'
    : 'drop-shadow(0 2px 4px rgba(0,0,0,0.15))';

  return (
    <div style={{ width: W, height: H, position: 'relative' }}>
      {/* Diamond polygon */}
      <svg
        width={W}
        height={H}
        style={{ position: 'absolute', inset: 0, overflow: 'visible', pointerEvents: 'none' }}
      >
        <polygon
          points={`${W / 2},3 ${W - 3},${H / 2} ${W / 2},${H - 3} 3,${H / 2}`}
          fill="#ea580c"
          stroke={stroke}
          strokeWidth={selected || hasError || hasWarning ? 2.5 : 1.5}
          style={{ filter: shadowFilter }}
        />
      </svg>

      {/* Node text */}
      <div
        style={{
          position: 'absolute', inset: 0,
          display: 'flex', flexDirection: 'column',
          alignItems: 'center', justifyContent: 'center',
          gap: 2, pointerEvents: 'none',
        }}
      >
        <span style={{ fontSize: 9, fontWeight: 800, color: 'rgba(255,255,255,0.9)', letterSpacing: '0.08em', textTransform: 'uppercase' }}>
          ◆ IF
        </span>
        <span style={{ fontSize: 9, color: 'rgba(255,255,255,0.75)', maxWidth: 72, textAlign: 'center', lineHeight: 1.2, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {data.name}
        </span>
      </div>

      {/* ── Input handle (left vertex) ── */}
      <Handle
        type="target"
        position={Position.Left}
        id="in"
        style={{ background: '#c2410c', border: '2px solid white', width: 10, height: 10, left: -5 }}
      />

      {/* ── True handle (right vertex) — green ── */}
      <Handle
        type="source"
        position={Position.Right}
        id="true"
        style={{ background: '#16a34a', border: '2px solid white', width: 10, height: 10, right: -5 }}
      />
      {/* "T" label beside True handle */}
      <div style={{ position: 'absolute', right: -20, top: '50%', transform: 'translateY(-50%)', fontSize: 9, fontWeight: 800, color: '#16a34a', pointerEvents: 'none', userSelect: 'none' }}>
        T
      </div>

      {/* ── False handle (bottom vertex) — red ── */}
      <Handle
        type="source"
        position={Position.Bottom}
        id="false"
        style={{ background: '#dc2626', border: '2px solid white', width: 10, height: 10, bottom: -5 }}
      />
      {/* "F" label below False handle */}
      <div style={{ position: 'absolute', bottom: -17, left: '50%', transform: 'translateX(-50%)', fontSize: 9, fontWeight: 800, color: '#dc2626', pointerEvents: 'none', userSelect: 'none' }}>
        F
      </div>

      {/* Validation badge */}
      {(hasError || hasWarning) && (
        <div style={{
          position: 'absolute', top: -2, right: W / 2 - 8,
          width: 16, height: 16, borderRadius: '50%',
          background: hasError ? '#dc2626' : '#d97706',
          border: '2px solid white',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 9, color: 'white', fontWeight: 800,
          pointerEvents: 'none', userSelect: 'none', zIndex: 10,
        }}>
          !
        </div>
      )}
    </div>
  );
}

export default memo(ConditionNode);
