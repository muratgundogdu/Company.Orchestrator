import { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { getNodeDef } from './utils';
import type { WorkflowNodeData } from './types';
import { useValidation } from './ValidationContext';

function WorkflowNode({ id, data, selected }: NodeProps<WorkflowNodeData>) {
  const def = getNodeDef(data.stepType);
  const { errorNodeIds, warningNodeIds } = useValidation();

  const hasError   = errorNodeIds.has(id);
  const hasWarning = !hasError && warningNodeIds.has(id);

  const borderColor = selected
    ? '#2563eb'
    : hasError   ? '#dc2626'
    : hasWarning ? '#d97706'
    : def.color;

  const boxShadow = selected
    ? '0 0 0 3px rgba(37,99,235,0.25), 0 4px 12px rgba(0,0,0,0.15)'
    : hasError   ? '0 0 0 3px rgba(220,38,38,0.20), 0 4px 12px rgba(0,0,0,0.12)'
    : hasWarning ? '0 0 0 3px rgba(217,119,6,0.20), 0 4px 12px rgba(0,0,0,0.12)'
    : '0 2px 8px rgba(0,0,0,0.10)';

  return (
    <div style={{ position: 'relative' }}>
      {/* Target handle — left */}
      <Handle
        type="target"
        position={Position.Left}
        style={{
          background: def.color,
          width: 10,
          height: 10,
          border: '2px solid white',
        }}
      />

      <div
        style={{
          minWidth: 190,
          maxWidth: 220,
          border: `2px solid ${borderColor}`,
          borderRadius: 10,
          background: 'white',
          overflow: 'hidden',
          boxShadow,
          transition: 'box-shadow 0.15s, border-color 0.15s',
        }}
      >
        {/* Colored header */}
        <div
          style={{
            background: def.color,
            padding: '7px 10px',
            display: 'flex',
            alignItems: 'center',
            gap: 6,
          }}
        >
          <span style={{ fontSize: 15, lineHeight: 1 }}>{def.emoji}</span>
          <span
            style={{
              color: 'white',
              fontSize: 10,
              fontWeight: 700,
              textTransform: 'uppercase',
              letterSpacing: '0.06em',
              whiteSpace: 'nowrap',
            }}
          >
            {def.label}
          </span>
        </div>

        {/* Body */}
        <div style={{ padding: '8px 10px 9px' }}>
          <p
            style={{
              margin: 0,
              fontSize: 13,
              fontWeight: 600,
              color: '#111827',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            {data.name}
          </p>
          <p
            style={{
              margin: '2px 0 0',
              fontSize: 10,
              color: '#9ca3af',
              fontFamily: 'monospace',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            {data.stepType}
          </p>
        </div>
      </div>

      {/* Source handle — right (success path) */}
      <Handle
        type="source"
        position={Position.Right}
        style={{
          background: def.color,
          width: 10,
          height: 10,
          border: '2px solid white',
        }}
      />

      {/* Failure handle — bottom center (red, optional error routing) */}
      <Handle
        type="source"
        position={Position.Bottom}
        id="failure"
        title="On Error — connect to an error-handler step"
        style={{
          background: '#dc2626',
          width: 8,
          height: 8,
          border: '2px solid white',
          bottom: -4,
        }}
      />

      {/* Validation badge */}
      {(hasError || hasWarning) && (
        <div
          style={{
            position: 'absolute', top: -7, right: -7,
            width: 16, height: 16, borderRadius: '50%',
            background: hasError ? '#dc2626' : '#d97706',
            border: '2px solid white',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 9, color: 'white', fontWeight: 800,
            pointerEvents: 'none', userSelect: 'none',
          }}
        >
          !
        </div>
      )}
    </div>
  );
}

export default memo(WorkflowNode);
