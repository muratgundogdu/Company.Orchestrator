import { memo } from 'react';
import { Handle, Position, type NodeProps } from 'reactflow';
import type { WorkflowNodeData } from './types';
import { useValidation } from './ValidationContext';

// Flat-top hexagon dimensions
const W = 180;
const H = 96;

// Hexagon vertices: flat-top (left/right are flat edges; top/bottom are points)
const HEX_POINTS = [
  `${W * 0.25},3`,
  `${W * 0.75},3`,
  `${W - 3},${H / 2}`,
  `${W * 0.75},${H - 3}`,
  `${W * 0.25},${H - 3}`,
  `3,${H / 2}`,
].join(' ');

const TEAL  = '#0891b2'; // cyan-600
const GREEN = '#16a34a';
const RED   = '#dc2626';

function ForEachNode({ id, data, selected }: NodeProps<WorkflowNodeData>) {
  const { errorNodeIds, warningNodeIds } = useValidation();
  const hasError   = errorNodeIds.has(id);
  const hasWarning = !hasError && warningNodeIds.has(id);

  const stroke = selected
    ? '#2563eb'
    : hasError   ? RED
    : hasWarning ? '#d97706'
    : TEAL;

  const shadowFilter = selected
    ? 'drop-shadow(0 0 6px rgba(37,99,235,0.5))'
    : hasError   ? 'drop-shadow(0 0 6px rgba(220,38,38,0.45))'
    : hasWarning ? 'drop-shadow(0 0 6px rgba(217,119,6,0.45))'
    : `drop-shadow(0 0 4px rgba(8,145,178,0.35))`;

  const isRow      = data.stepType === 'foreach.row';
  const isFile     = data.stepType === 'foreach.file';
  const collection = isRow
    ? String(data.config?.collectionVariable ?? 'dataTable')
    : isFile
    ? String(data.config?.collectionVariable ?? 'files')
    : String(data.config?.collection ?? '{{collection}}');
  const itemVar = isRow
    ? String(data.config?.rowVariable ?? 'currentRow')
    : isFile
    ? String(data.config?.fileVariable ?? 'currentFile')
    : String(data.config?.itemVariable ?? 'currentItem');
  const indexVar = String(data.config?.indexVariable ?? 'currentIndex');

  return (
    <div style={{ width: W, height: H, position: 'relative' }}>
      {/* Hexagon fill */}
      <svg
        width={W}
        height={H}
        style={{ position: 'absolute', inset: 0, overflow: 'visible', pointerEvents: 'none' }}
      >
        <polygon
          points={HEX_POINTS}
          fill={TEAL}
          stroke={stroke}
          strokeWidth={selected || hasError || hasWarning ? 2.5 : 1.5}
          style={{ filter: shadowFilter }}
        />
      </svg>

      {/* Content */}
      <div
        style={{
          position: 'absolute', inset: 0,
          display: 'flex', flexDirection: 'column',
          alignItems: 'center', justifyContent: 'center',
          gap: 3, pointerEvents: 'none',
          paddingLeft: 10, paddingRight: 10,
        }}
      >
        <span style={{ fontSize: 9, fontWeight: 800, color: 'rgba(255,255,255,0.9)', letterSpacing: '0.1em', textTransform: 'uppercase' }}>
          ⟳ {isFile ? 'FOR EACH FILE' : isRow ? 'FOR EACH ROW' : 'FOR EACH'}
        </span>
        <span style={{
          fontSize: 9, color: 'rgba(255,255,255,0.85)',
          maxWidth: 120, textAlign: 'center', lineHeight: 1.3,
          overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
          fontFamily: 'monospace',
        }}>
          {collection}
        </span>
        <span style={{ fontSize: 8, color: 'rgba(255,255,255,0.65)', textAlign: 'center', lineHeight: 1.3 }}>
          → {itemVar} [{indexVar}]
        </span>
      </div>

      {/* ── INPUT handle (left) ── */}
      <Handle
        type="target"
        position={Position.Left}
        id="in"
        style={{ background: TEAL, border: '2px solid white', width: 10, height: 10, left: -5, top: '50%' }}
      />

      {/* ── LOOP-BACK handle (top-center) — receives edge from last body step ── */}
      <Handle
        type="target"
        position={Position.Top}
        id="loop-back"
        style={{
          background: '#64748b', border: '2px dashed white',
          width: 10, height: 10, top: -5, left: '50%',
        }}
      />
      <div style={{
        position: 'absolute', top: -18, left: '50%', transform: 'translateX(-50%)',
        fontSize: 8, color: '#64748b', whiteSpace: 'nowrap',
        pointerEvents: 'none', userSelect: 'none',
      }}>
        ↵ back
      </div>

      {/* ── LOOP BODY handle (right) ── */}
      <Handle
        type="source"
        position={Position.Right}
        id="loop-body"
        style={{ background: TEAL, border: '2px solid white', width: 10, height: 10, right: -5, top: '50%' }}
      />
      <div style={{
        position: 'absolute', right: -32, top: '50%', transform: 'translateY(-50%)',
        fontSize: 8, fontWeight: 700, color: TEAL,
        pointerEvents: 'none', userSelect: 'none',
      }}>
        Body
      </div>

      {/* ── COMPLETED handle (bottom-center) ── */}
      <Handle
        type="source"
        position={Position.Bottom}
        id="completed"
        style={{ background: GREEN, border: '2px solid white', width: 10, height: 10, bottom: -5, left: '40%' }}
      />
      <div style={{
        position: 'absolute', bottom: -17, left: '40%', transform: 'translateX(-50%)',
        fontSize: 8, fontWeight: 700, color: GREEN,
        pointerEvents: 'none', userSelect: 'none',
      }}>
        Done
      </div>

      {/* ── FAILURE handle (bottom-right) ── */}
      <Handle
        type="source"
        position={Position.Bottom}
        id="failure"
        style={{ background: RED, border: '2px solid white', width: 8, height: 8, bottom: -4, left: '65%' }}
      />

      {/* Validation badge */}
      {(hasError || hasWarning) && (
        <div style={{
          position: 'absolute', top: 2, left: W / 2 - 8,
          width: 16, height: 16, borderRadius: '50%',
          background: hasError ? RED : '#d97706',
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

export default memo(ForEachNode);
