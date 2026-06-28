import { Edge, MarkerType, Node } from 'reactflow';
import { NODE_DEFS } from './nodeDefinitions';
import type { AvailableVariable, WorkflowDefinition, WorkflowNodeData, WorkflowStep } from './types';

// ── ID generation ──────────────────────────────────────────────────────────────

export function generateNodeId(stepType: string): string {
  const prefix = stepType.replace(/\./g, '-');
  const suffix = Math.random().toString(36).slice(2, 6);
  return `${prefix}-${suffix}`;
}

// ── Auto-layout ────────────────────────────────────────────────────────────────

/**
 * Computes (x, y) positions for each step using a left-to-right topological layout.
 * Supports both `onSuccess` (forward ref) and `dependsOn` (backward ref) schemas.
 */
export function autoLayout(
  steps: WorkflowStep[],
): Map<string, { x: number; y: number }> {
  const successorsOf = new Map<string, string[]>();
  const predecessorsOf = new Map<string, string[]>();

  steps.forEach((s) => {
    successorsOf.set(s.id, []);
    predecessorsOf.set(s.id, []);
  });

  const addEdge = (from: string, to: string) => {
    if (!successorsOf.has(from) || !predecessorsOf.has(to)) return;
    successorsOf.get(from)!.push(to);
    predecessorsOf.get(to)!.push(from);
  };

  // Build a set of foreach step IDs so we can detect loop-back edges during layout
  const forEachStepIds = new Set(steps.filter(s => isForeachStepType(s.type)).map(s => s.id));

  steps.forEach((step) => {
    // condition.if: trueStepId / falseStepId are forward refs
    if (step.trueStepId)  addEdge(step.id, step.trueStepId);
    if (step.falseStepId) addEdge(step.id, step.falseStepId);

    // foreach.loop: loopStepId / completedStepId are forward refs
    if (step.loopStepId)      addEdge(step.id, step.loopStepId);
    if (step.completedStepId) addEdge(step.id, step.completedStepId);

    // Phase 10: failure routing
    if (step.onFailureStepId) addEdge(step.id, step.onFailureStepId);

    // Regular forward refs: nextStepId (canonical), onSuccess (legacy)
    const forwardRef = step.nextStepId ?? step.onSuccess;
    if (forwardRef) {
      const targets = Array.isArray(forwardRef) ? forwardRef : [forwardRef];
      targets.forEach((t) => {
        // Skip loop-back edges (body step → foreach node) to prevent BFS cycles in layout
        if (!forEachStepIds.has(t)) addEdge(step.id, t);
      });
    }
    if (step.dependsOn) {
      const deps = Array.isArray(step.dependsOn) ? step.dependsOn : [step.dependsOn];
      deps.forEach((d) => addEdge(d, step.id));
    }
  });

  // BFS from roots to assign levels
  const levels = new Map<string, number>();
  const roots = steps.filter((s) => predecessorsOf.get(s.id)!.length === 0);
  const queue: Array<{ id: string; level: number }> = roots.map((r) => ({
    id: r.id,
    level: 0,
  }));

  while (queue.length > 0) {
    const { id, level } = queue.shift()!;
    if ((levels.get(id) ?? -1) >= level) continue;
    levels.set(id, level);
    successorsOf.get(id)!.forEach((next) => queue.push({ id: next, level: level + 1 }));
  }

  // Default level 0 for any orphan nodes
  steps.forEach((s) => { if (!levels.has(s.id)) levels.set(s.id, 0); });

  // Count per level for vertical stacking
  const countPerLevel = new Map<number, number>();
  steps.forEach((s) => {
    const lvl = levels.get(s.id)!;
    countPerLevel.set(lvl, (countPerLevel.get(lvl) ?? 0) + 1);
  });

  const indexPerLevel = new Map<number, number>();
  const positions = new Map<string, { x: number; y: number }>();

  const sorted = [...steps].sort((a, b) => levels.get(a.id)! - levels.get(b.id)!);

  sorted.forEach((step) => {
    const lvl = levels.get(step.id)!;
    const idx = indexPerLevel.get(lvl) ?? 0;
    const total = countPerLevel.get(lvl)!;
    const NODE_H = 150;
    const centerY = 300;

    positions.set(step.id, {
      x: lvl * 290 + 60,
      y: centerY - ((total - 1) * NODE_H) / 2 + idx * NODE_H,
    });

    indexPerLevel.set(lvl, idx + 1);
  });

  return positions;
}

// ── Foreach step helpers ───────────────────────────────────────────────────────

function isForeachStepType(stepType: string): boolean {
  return stepType === 'foreach.loop' || stepType === 'foreach.row' || stepType === 'foreach.file';
}


/**
 * Topologically sorts nodes so that every node appears after all its predecessors.
 * Multiple roots are ordered left-to-right then top-to-bottom (by canvas position)
 * for a stable, visually-intuitive result.
 */
function topoSort(nodes: Node<WorkflowNodeData>[], edges: Edge[]): Node<WorkflowNodeData>[] {
  // Build adjacency structures
  const inDegree  = new Map<string, number>(nodes.map((n) => [n.id, 0]));
  const children  = new Map<string, string[]>(nodes.map((n) => [n.id, []]));

  for (const e of edges) {
    // Skip loop-back edges (last body step → foreach node) to prevent cycle in topoSort
    if (e.targetHandle === 'loop-back') continue;
    if (inDegree.has(e.source) && inDegree.has(e.target)) {
      children.get(e.source)!.push(e.target);
      inDegree.set(e.target, (inDegree.get(e.target) ?? 0) + 1);
    }
  }

  const nodeById = new Map(nodes.map((n) => [n.id, n]));

  // Seed queue with roots, sorted left→right then top→bottom for stable order
  const queue: Node<WorkflowNodeData>[] = nodes
    .filter((n) => inDegree.get(n.id) === 0)
    .sort((a, b) => {
      const dx = (a.position?.x ?? 0) - (b.position?.x ?? 0);
      return dx !== 0 ? dx : (a.position?.y ?? 0) - (b.position?.y ?? 0);
    });

  const sorted: Node<WorkflowNodeData>[] = [];
  const visited = new Set<string>();

  while (queue.length > 0) {
    const node = queue.shift()!;
    if (visited.has(node.id)) continue;
    visited.add(node.id);
    sorted.push(node);

    // Reduce in-degree of successors; enqueue those that become ready
    const ready: Node<WorkflowNodeData>[] = [];
    for (const childId of children.get(node.id) ?? []) {
      const deg = (inDegree.get(childId) ?? 1) - 1;
      inDegree.set(childId, deg);
      if (deg === 0) {
        const child = nodeById.get(childId);
        if (child) ready.push(child);
      }
    }
    // Keep ready siblings in left→right, top→bottom order before enqueuing
    ready.sort((a, b) => {
      const dx = (a.position?.x ?? 0) - (b.position?.x ?? 0);
      return dx !== 0 ? dx : (a.position?.y ?? 0) - (b.position?.y ?? 0);
    });
    queue.push(...ready);
  }

  // Append any nodes not reached (cycles / isolated) in their original order
  for (const n of nodes) {
    if (!visited.has(n.id)) sorted.push(n);
  }

  return sorted;
}

/**
 * Converts the React Flow graph into a backend-compatible workflow JSON.
 * Steps are topologically sorted so the backend can execute them in array order.
 * Edges (source → target) map to `nextStepId` on the source step.
 */
export function buildWorkflowDefinition(
  name: string,
  version: string,
  description: string,
  nodes: Node<WorkflowNodeData>[],
  edges: Edge[],
): WorkflowDefinition {
  const sorted = topoSort(nodes, edges);

  const steps: WorkflowStep[] = sorted.map((node) => {
    // Failure edge is common to all node types — always separate it out first.
    const failureEdge = edges.find((e) => e.source === node.id && e.sourceHandle === 'failure');

    const retryField = node.data.retry ? { retry: node.data.retry } : {};

    // condition.if: emit trueStepId / falseStepId based on sourceHandle
    if (node.data.stepType === 'condition.if') {
      const trueEdge  = edges.find((e) => e.source === node.id && e.sourceHandle === 'true');
      const falseEdge = edges.find((e) => e.source === node.id && e.sourceHandle === 'false');
      return {
        id: node.id,
        type: node.data.stepType,
        name: node.data.name,
        config: node.data.config,
        ...(trueEdge    ? { trueStepId:      trueEdge.target    } : {}),
        ...(falseEdge   ? { falseStepId:     falseEdge.target   } : {}),
        ...(failureEdge ? { onFailureStepId: failureEdge.target } : {}),
        ...retryField,
      };
    }

    // foreach.loop / foreach.row: emit loopStepId / completedStepId based on sourceHandle
    if (isForeachStepType(node.data.stepType)) {
      const loopBodyEdge  = edges.find((e) => e.source === node.id && e.sourceHandle === 'loop-body');
      const completedEdge = edges.find((e) => e.source === node.id && e.sourceHandle === 'completed');
      return {
        id: node.id,
        type: node.data.stepType,
        name: node.data.name,
        config: node.data.config,
        ...(loopBodyEdge  ? { loopStepId:      loopBodyEdge.target  } : {}),
        ...(completedEdge ? { completedStepId: completedEdge.target } : {}),
        ...(failureEdge   ? { onFailureStepId: failureEdge.target   } : {}),
        ...retryField,
      };
    }

    // Regular nodes: success path uses all non-failure outgoing edges.
    // Also include the loop-back edge (source-side — it's just a regular nextStepId pointing to foreach).
    const outgoing = edges.filter((e) =>
      e.source === node.id &&
      e.sourceHandle !== 'failure' &&
      e.targetHandle !== 'loop-back',   // loop-back is captured separately below
    );
    // Loop-back: edge where this node connects to a foreach's loop-back handle
    const loopBackEdge = edges.find((e) =>
      e.source === node.id && e.targetHandle === 'loop-back',
    );

    let nextStepId: string | string[] | undefined;
    if (loopBackEdge) {
      // The last body step — its nextStepId points back to the foreach node
      nextStepId = loopBackEdge.target;
    } else if (outgoing.length === 1) {
      nextStepId = outgoing[0].target;
    } else if (outgoing.length > 1) {
      nextStepId = outgoing.map((e) => e.target);
    }

    return {
      id: node.id,
      type: node.data.stepType,
      name: node.data.name,
      config: node.data.config,
      ...(nextStepId !== undefined ? { nextStepId } : {}),
      ...(failureEdge ? { onFailureStepId: failureEdge.target } : {}),
      ...retryField,
    };
  });

  return {
    name,
    ...(description ? { description } : {}),
    version,
    steps,
  };
}

// ── Import ─────────────────────────────────────────────────────────────────────

export interface ParsedWorkflow {
  nodes: Node<WorkflowNodeData>[];
  edges: Edge[];
  name: string;
  version: string;
  description: string;
}

/**
 * Parses a workflow JSON string and returns React Flow nodes + edges.
 * Supports both `onSuccess` and `dependsOn` step linkage.
 */
export function parseWorkflowDefinition(json: string): ParsedWorkflow {
  let def: WorkflowDefinition;
  try {
    def = JSON.parse(json) as WorkflowDefinition;
  } catch {
    throw new Error('Invalid JSON — could not parse workflow definition.');
  }

  if (!def.steps || !Array.isArray(def.steps) || def.steps.length === 0) {
    throw new Error('Workflow must contain at least one step.');
  }

  const positions = autoLayout(def.steps);

  // Build a lookup for step type by ID (needed to detect loop-back edges)
  const stepTypeById = new Map(def.steps.map((s) => [s.id, s.type]));

  const nodes: Node<WorkflowNodeData>[] = def.steps.map((step) => ({
    id: step.id,
    type: getRFNodeType(step.type),
    position: positions.get(step.id) ?? { x: 100, y: 100 },
    data: {
      name: step.name ?? step.type,
      stepType: step.type,
      config: step.config ?? {},
      ...(step.retry ? { retry: step.retry } : {}),
    },
  }));

  const seenEdges = new Set<string>();
  const edges: Edge[] = [];

  const addE = (
    src: string,
    tgt: string,
    sourceHandle?: string,
    label?: string,
    edgeColor?: string,
    targetHandle?: string,
  ) => {
    const key = `${src}→${tgt}:${sourceHandle ?? ''}:${targetHandle ?? ''}`;
    if (seenEdges.has(key)) return;
    seenEdges.add(key);
    edges.push({
      id: `e-${src}-${tgt}${sourceHandle ? '-' + sourceHandle : ''}`,
      source: src,
      target: tgt,
      sourceHandle,
      targetHandle,
      type: 'smoothstep',
      animated: true,
      markerEnd: { type: MarkerType.ArrowClosed },
      ...(label ? {
        label,
        labelStyle: { fill: edgeColor, fontWeight: 700, fontSize: 10 },
        labelBgStyle: { fill: 'white', fillOpacity: 0.85 },
      } : {}),
      ...(edgeColor ? { style: { stroke: edgeColor } } : {}),
    });
  };

  def.steps.forEach((step) => {
    // condition.if: restore true/false branch edges with handle + color
    if (step.trueStepId)  addE(step.id, step.trueStepId,  'true',  '✓ True',  '#16a34a');
    if (step.falseStepId) addE(step.id, step.falseStepId, 'false', '✗ False', '#dc2626');

    // foreach.loop / foreach.row: restore loop-body and completed edges
    if (isForeachStepType(step.type)) {
      if (step.loopStepId) {
        addE(step.id, step.loopStepId, 'loop-body', '↻ Body', '#0891b2');
      }
      if (step.completedStepId) {
        addE(step.id, step.completedStepId, 'completed', '✓ Done', '#16a34a');
      }
    }

    // Phase 10: failure routing edge — dashed red, labeled "On Error"
    if (step.onFailureStepId) {
      addE(step.id, step.onFailureStepId, 'failure', '⚠ On Error', '#dc2626');
      const last = edges[edges.length - 1];
      if (last) last.style = { stroke: '#dc2626', strokeDasharray: '6,3' };
    }

    // Support all three regular linkage styles
    const forwardRef = step.nextStepId ?? step.onSuccess;
    if (forwardRef) {
      const t = Array.isArray(forwardRef) ? forwardRef : [forwardRef];
      t.forEach((targetId) => {
        // Detect loop-back: a regular nextStepId pointing to a foreach node
        if (isForeachStepType(stepTypeById.get(targetId) ?? '')) {
          addE(step.id, targetId,
            undefined,       // no sourceHandle (regular output)
            '↵ back',
            '#64748b',
            'loop-back',     // targetHandle on the foreach node
          );
          // Make loop-back edges dashed
          const last = edges[edges.length - 1];
          if (last) last.style = { stroke: '#64748b', strokeDasharray: '5,4' };
        } else {
          addE(step.id, targetId);
        }
      });
    }
    if (step.dependsOn) {
      const d = Array.isArray(step.dependsOn) ? step.dependsOn : [step.dependsOn];
      d.forEach((id) => addE(id, step.id));
    }
  });

  return {
    nodes,
    edges,
    name: def.name ?? 'imported-workflow',
    version: def.version ?? '1',
    description: def.description ?? '',
  };
}

// ── React Flow node type mapping ───────────────────────────────────────────────

/** Maps a backend step type to the React Flow custom node type key. */
export function getRFNodeType(stepType: string): string {
  if (stepType === 'condition.if') return 'conditionNode';
  if (isForeachStepType(stepType)) return 'forEachNode';
  return 'workflowNode';
}

// ── Fallback node def ──────────────────────────────────────────────────────────

export function getNodeDef(stepType: string) {
  return NODE_DEFS[stepType] ?? {
    label: stepType,
    emoji: '⚙️',
    color: '#6b7280',
    bgClass: 'bg-gray-500',
    category: 'Folder' as const,
    defaultConfig: {},
  };
}

// ── Variable binding ────────────────────────────────────────────────────────────

/**
 * Returns all output variables / artifact names produced by a single step,
 * derived from its type and current configuration.
 *
 * Mirrors the backend step-handler output contracts:
 *   - mail.read-attachments  → {{outputVariable}}, {{outputVariable_0}}, …_count, …_first
 *   - folder.read-file       → literal artifactName string
 *   - excel.read             → {{outputVariable}}, {{outputVariable_count}}
 *   - pdf.extract-table      → {{outputVariable}}, …_count, …_columns, …_first, …_0…_9
 *   - word.fill-template     → {{outputVariable}}, {{outputName}}, {{placeholdersReplaced}}, {{missingPlaceholders}}
 *   - excel.read-range       → {{outputVariable}}, {{outputVariable_count}}, …_columns, …_first, …_0…_9
 *   - excel.write-datatable  → {{rowsWritten}}, {{columnsWritten}}, {{outputArtifactName}}
 *   - excel.append-datatable → {{rowsAppended}}, {{columnsWritten}}, {{lastRowAfterAppend}}
 *   - datatable.aggregate    → {{outputVariable}}, …_operation, …_column, …_sourceCount
 *   - datatable.join         → {{outputVariable}}, {{outputVariable_count}}, …_columns, …_first, …_0…_9
 *   - http.request           → {{outputVariable}}, …_statusCode, …_isSuccess, …_headers, …_body, …_contentType
 *   - json.parse             → {{outputVariable}}, …_count, …_first, …_0…_9, …_columns
 *   - sql.query              → {{outputVariable}}, …_count, …_columns, …_first, …_0…_9
 *   - excel.transform        → literal "outputName.xlsx" artifact
 */
export function inferOutputVariables(node: Node<WorkflowNodeData>): AvailableVariable[] {
  const { stepType, config, name: sourceNodeName } = node.data;
  const base = { sourceNodeId: node.id, sourceNodeName };

  switch (stepType) {
    case 'mail.read-attachments': {
      // The backend key is outputVariable (also accepts outputVar for legacy)
      const v = String(config.outputVariable ?? config.outputVar ?? 'mailArtifacts');
      return [
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_0}}`, label: `${v}_0`,
          description: 'First attachment artifact name',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_1}}`, label: `${v}_1`,
          description: 'Second attachment artifact name',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_first}}`, label: `${v}_first`,
          description: 'Alias for _0 — first attachment',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Total number of attachments downloaded',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'JSON array of all artifact names',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{selectedMessageId}}', label: 'selectedMessageId',
          description: 'IMAP UID of the processed email',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{selectedMessageFolder}}', label: 'selectedMessageFolder',
          description: 'Folder of the processed email',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{selectedEmailSubject}}', label: 'selectedEmailSubject',
          description: 'Subject of the processed email',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{selectedEmailFrom}}', label: 'selectedEmailFrom',
          description: 'Sender of the processed email',
        },
      ];
    }

    case 'mail.get-body': {
      const v = String(config.outputVariable ?? 'mailBody');
      return [
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'Full email body content',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_length}}`, label: `${v}_length`,
          description: 'Character length of the email body',
        },
      ];
    }

    case 'mail.extract-value': {
      const v = String(config.outputVariable ?? 'extractedValue');
      return [
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'Extracted value from source text',
        },
      ];
    }

    case 'mail.extract-table': {
      const v = String(config.outputVariable ?? 'tableValue');
      const mode = String(config.mode ?? 'cell').toLowerCase();
      const vars = [
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}}}`, label: v,
          description: mode === 'tablejson'
            ? 'Table rows as JSON array'
            : 'Extracted table cell value',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_columns}}`, label: `${v}_columns`,
          description: 'JSON array of detected column headers',
        },
      ];
      if (mode === 'tablejson') {
        vars.push({
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Number of data rows in the table',
        });
      }
      return vars;
    }

    case 'mail.reply':
      return [
        {
          ...base, kind: 'variable',
          insertValue: '{{replyMessageId}}', label: 'replyMessageId',
          description: 'Message-ID of the sent reply',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{replyConversationId}}', label: 'replyConversationId',
          description: 'Conversation/thread id of the reply',
        },
      ];

    case 'mail.forward':
      return [
        {
          ...base, kind: 'variable',
          insertValue: '{{forwardMessageId}}', label: 'forwardMessageId',
          description: 'Message-ID of the forwarded message',
        },
      ];

    case 'mail.move':
      return [
        {
          ...base, kind: 'variable',
          insertValue: '{{movedFolder}}', label: 'movedFolder',
          description: 'Folder the message was moved to',
        },
      ];

    case 'mail.mark':
      return [
        {
          ...base, kind: 'variable',
          insertValue: '{{mailReadState}}', label: 'mailReadState',
          description: 'read or unread',
        },
      ];

    case 'mail.delete':
      return [
        {
          ...base, kind: 'variable',
          insertValue: '{{mailDeleted}}', label: 'mailDeleted',
          description: 'true when the message was deleted',
        },
      ];

    case 'folder.read-file': {
      const name = String(config.artifactName ?? 'file');
      return [
        {
          ...base, kind: 'artifact',
          insertValue: name, label: name,
          description: 'Artifact name of the file read from disk',
        },
      ];
    }

    case 'folder.list-files': {
      const v = String(config.outputVariable ?? 'files');
      const vars: AvailableVariable[] = [
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'JSON array of file objects (name, fullPath, sizeBytes, …)',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Number of matching files found',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_first}}`, label: `${v}_first`,
          description: 'First file object as JSON',
        },
      ];
      for (let i = 0; i < 10; i++) {
        vars.push({
          ...base, kind: 'variable',
          insertValue: `{{${v}_${i}}}`, label: `${v}_${i}`,
          description: i === 0 ? 'Alias for first file — same as _first' : `File object at index ${i}`,
        });
      }
      return vars;
    }

    case 'csv.read': {
      const artifact = String(config.inputArtifactName ?? 'source-file');
      const v = String(config.outputVariable ?? 'csvData');
      const vars: AvailableVariable[] = [
        {
          ...base, kind: 'artifact',
          insertValue: artifact, label: artifact,
          description: 'Input CSV artifact name',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'DataTable rows as JSON array of objects',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Number of data rows in the CSV',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_columns}}`, label: `${v}_columns`,
          description: 'JSON array of column names',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_first}}`, label: `${v}_first`,
          description: 'First data row as JSON object',
        },
      ];
      for (let i = 0; i < 10; i++) {
        vars.push({
          ...base, kind: 'variable',
          insertValue: `{{${v}_${i}}}`, label: `${v}_${i}`,
          description: `Row ${i} as JSON object`,
        });
      }
      return vars;
    }

    case 'csv.write': {
      const artifact = String(config.outputName ?? 'output.csv');
      return [
        {
          ...base, kind: 'artifact',
          insertValue: artifact, label: artifact,
          description: 'Output CSV artifact name',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{outputName}}', label: 'outputName',
          description: 'Name of the CSV artifact produced by this step',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{rowsWritten}}', label: 'rowsWritten',
          description: 'Number of data rows written',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{columnsWritten}}', label: 'columnsWritten',
          description: 'Number of columns written',
        },
      ];
    }

    case 'json.read-file': {
      const artifact = String(config.inputArtifactName ?? 'source-file');
      const v = String(config.outputVariable ?? 'jsonData');
      const vars: AvailableVariable[] = [
        {
          ...base, kind: 'artifact',
          insertValue: artifact, label: artifact,
          description: 'Input JSON artifact name',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'Parsed value, JSON string, or DataTable JSON',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Number of items when result is an array or table',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_first}}`, label: `${v}_first`,
          description: 'First item when result is an array or table',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_columns}}`, label: `${v}_columns`,
          description: 'Column names when output mode is table',
        },
      ];
      for (let i = 0; i < 10; i++) {
        vars.push({
          ...base, kind: 'variable',
          insertValue: `{{${v}_${i}}}`, label: `${v}_${i}`,
          description: `Item ${i} when result is an array or table`,
        });
      }
      return vars;
    }

    case 'json.write-file': {
      const artifact = String(config.outputName ?? 'output.json');
      return [
        {
          ...base, kind: 'artifact',
          insertValue: artifact, label: artifact,
          description: 'Output JSON artifact name',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{outputName}}', label: 'outputName',
          description: 'Name of the JSON artifact produced by this step',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{bytesWritten}}', label: 'bytesWritten',
          description: 'Size of the written JSON file in bytes',
        },
      ];
    }

    case 'zip.extract': {
      const inputArtifact = String(config.inputArtifactName ?? 'reports.zip');
      const prefix = String(config.outputPrefix ?? 'unzipped');
      const v = String(config.outputVariable ?? 'extractedFiles');
      const vars: AvailableVariable[] = [
        {
          ...base, kind: 'artifact',
          insertValue: inputArtifact, label: inputArtifact,
          description: 'Input ZIP artifact name',
        },
        {
          ...base, kind: 'variable',
          insertValue: prefix, label: `${prefix}-*`,
          description: 'Output prefix for extracted artifact names',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'JSON array of extracted artifact names',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Number of extracted files',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_first}}`, label: `${v}_first`,
          description: 'First extracted artifact name',
        },
      ];
      for (let i = 0; i < 10; i++) {
        vars.push({
          ...base, kind: 'variable',
          insertValue: `{{${v}_${i}}}`, label: `${v}_${i}`,
          description: `Extracted artifact name at index ${i}`,
        });
      }
      return vars;
    }

    case 'zip.create': {
      const artifact = String(config.outputName ?? 'reports.zip');
      return [
        {
          ...base, kind: 'artifact',
          insertValue: artifact, label: artifact,
          description: 'Output ZIP artifact name',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{outputName}}', label: 'outputName',
          description: 'Name of the ZIP artifact produced by this step',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{filesZipped}}', label: 'filesZipped',
          description: 'Number of files added to the ZIP',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{zipSizeBytes}}', label: 'zipSizeBytes',
          description: 'Size of the ZIP file in bytes',
        },
      ];
    }

    case 'pdf.read-text': {
      const artifact = String(config.inputArtifactName ?? 'invoice.pdf');
      const v = String(config.outputVariable ?? 'pdfText');
      return [
        {
          ...base, kind: 'artifact',
          insertValue: artifact, label: artifact,
          description: 'Input PDF artifact name',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'Full extracted PDF text',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_length}}`, label: `${v}_length`,
          description: 'Character count of extracted text',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_pageCount}}`, label: `${v}_pageCount`,
          description: 'Number of pages extracted',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_first500}}`, label: `${v}_first500`,
          description: 'First 500 characters of extracted text',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_first1000}}`, label: `${v}_first1000`,
          description: 'First 1000 characters of extracted text',
        },
      ];
    }

    case 'pdf.extract-table': {
      const artifact = String(config.inputArtifactName ?? 'report.pdf');
      const v = String(config.outputVariable ?? 'pdfTable');
      const vars = [
        {
          ...base, kind: 'artifact' as const,
          insertValue: artifact, label: artifact,
          description: 'Input PDF artifact name',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}}}`, label: v,
          description: 'DataTable rows as JSON array of objects',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Number of data rows in the table',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_columns}}`, label: `${v}_columns`,
          description: 'JSON array of column names',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_first}}`, label: `${v}_first`,
          description: 'First data row as JSON object',
        },
      ];
      for (let i = 0; i < 10; i++) {
        vars.push({
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_${i}}}`, label: `${v}_${i}`,
          description: `Row ${i} as JSON object`,
        });
      }
      return vars;
    }

    case 'word.fill-template': {
      const inputArtifact = String(config.inputArtifactName ?? 'template.docx');
      const artifact      = String(config.outputName ?? 'generated.docx');
      const v             = String(config.outputVariable ?? 'generatedDocument');
      return [
        {
          ...base, kind: 'artifact',
          insertValue: inputArtifact, label: inputArtifact,
          description: 'Input Word template artifact name',
        },
        {
          ...base, kind: 'artifact',
          insertValue: artifact, label: artifact,
          description: 'Generated Word document artifact name',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'Generated document artifact name (same as outputName)',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{outputName}}', label: 'outputName',
          description: 'Name of the generated Word document artifact',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{placeholdersReplaced}}', label: 'placeholdersReplaced',
          description: 'Number of placeholders successfully replaced',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{missingPlaceholders}}', label: 'missingPlaceholders',
          description: 'Comma-separated list of placeholders with missing variables',
        },
      ];
    }

    case 'excel.read': {
      const v = String(config.outputVariable ?? config.outputVar ?? 'excelData');
      return [
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'Excel rows serialised as a JSON array',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Number of data rows (excluding header)',
        },
      ];
    }

    case 'excel.read-range': {
      const v = String(config.outputVariable ?? 'dataTable');
      const vars = [
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}}}`, label: v,
          description: 'DataTable rows as JSON array of objects',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Number of data rows in the table',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_columns}}`, label: `${v}_columns`,
          description: 'JSON array of column names',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_first}}`, label: `${v}_first`,
          description: 'First data row as JSON object',
        },
      ];
      for (let i = 0; i < 10; i++) {
        vars.push({
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_${i}}}`, label: `${v}_${i}`,
          description: `Row ${i} as JSON object`,
        });
      }
      return vars;
    }

    case 'excel.write-datatable': {
      const artifact = String(config.inputArtifactName ?? 'report.xlsx');
      return [
        {
          ...base, kind: 'artifact',
          insertValue: artifact, label: artifact,
          description: 'Updated Excel workbook artifact',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{outputArtifactName}}', label: 'outputArtifactName',
          description: 'Name of the updated workbook artifact',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{rowsWritten}}', label: 'rowsWritten',
          description: 'Number of data rows written',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{columnsWritten}}', label: 'columnsWritten',
          description: 'Number of columns written',
        },
      ];
    }

    case 'excel.append-datatable': {
      const artifact = String(config.inputArtifactName ?? 'history.xlsx');
      return [
        {
          ...base, kind: 'artifact',
          insertValue: artifact, label: artifact,
          description: 'Updated Excel workbook artifact',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{rowsAppended}}', label: 'rowsAppended',
          description: 'Number of rows appended',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{columnsWritten}}', label: 'columnsWritten',
          description: 'Number of source columns written',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{lastRowAfterAppend}}', label: 'lastRowAfterAppend',
          description: 'Last row number after append',
        },
      ];
    }

    case 'excel.transform': {
      const name = String(config.outputName ?? 'transformed');
      return [
        {
          ...base, kind: 'artifact',
          insertValue: `${name}.xlsx`, label: `${name}.xlsx`,
          description: 'Artifact name of the transformed Excel file',
        },
      ];
    }

    case 'excel.merge': {
      const name = String(config.outputName ?? 'merged-excel');
      return [
        {
          ...base, kind: 'artifact',
          insertValue: `${name}.xlsx`, label: `${name}.xlsx`,
          description: 'Artifact name of the merged Excel file',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{mergedArtifactName}}', label: 'mergedArtifactName',
          description: 'Artifact name of the merged Excel file',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{mergedArtifact_sourceCount}}', label: 'mergedArtifact_sourceCount',
          description: 'Number of input files merged',
        },
      ];
    }

    case 'excel.split': {
      const v = 'splitArtifacts';
      return [
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_0}}`, label: `${v}_0`,
          description: 'First split output artifact name',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_1}}`, label: `${v}_1`,
          description: 'Second split output artifact name',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_first}}`, label: `${v}_first`,
          description: 'Alias for _0 — first split output',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Total number of split output files',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'JSON array of all split artifact names',
        },
      ];
    }

    case 'foreach.loop': {
      const itemVar  = String(config.itemVariable  ?? 'currentItem');
      const indexVar = String(config.indexVariable ?? 'currentIndex');
      return [
        {
          ...base, kind: 'variable',
          insertValue: `{{${itemVar}}}`, label: itemVar,
          description: 'Current item in the loop iteration',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${indexVar}}}`, label: indexVar,
          description: 'Zero-based index of the current item',
        },
      ];
    }

    case 'foreach.row': {
      const rowVar   = String(config.rowVariable   ?? 'currentRow');
      const indexVar = String(config.indexVariable ?? 'currentIndex');
      const vars: AvailableVariable[] = [
        {
          ...base, kind: 'variable',
          insertValue: `{{${rowVar}}}`, label: rowVar,
          description: 'Current row as JSON object',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${indexVar}}}`, label: indexVar,
          description: 'Zero-based index of the current row',
        },
      ];
      for (const col of ['CustomerNo', 'Amount', 'Region']) {
        vars.push({
          ...base, kind: 'variable',
          insertValue: `{{${rowVar}.${col}}}`, label: `${rowVar}.${col}`,
          description: `Row field ${col}`,
        });
      }
      return vars;
    }

    case 'foreach.file': {
      const fileVar  = String(config.fileVariable  ?? 'currentFile');
      const indexVar = String(config.indexVariable ?? 'currentIndex');
      const vars: AvailableVariable[] = [
        {
          ...base, kind: 'variable',
          insertValue: `{{${fileVar}}}`, label: fileVar,
          description: 'Current file object as JSON',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${indexVar}}}`, label: indexVar,
          description: 'Zero-based index of the current file',
        },
      ];
      for (const field of ['name', 'fullPath', 'directory', 'extension', 'sizeBytes', 'lastModified']) {
        vars.push({
          ...base, kind: 'variable',
          insertValue: `{{${fileVar}.${field}}}`, label: `${fileVar}.${field}`,
          description: `File field ${field}`,
        });
      }
      return vars;
    }

    case 'set.variable': {
      const v = String(config.variableName ?? '').trim().replace(/^\{\{|\}\}$/g, '');
      if (!v) return [];
      return [
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'Variable value set by this step',
        },
      ];
    }

    case 'datatable.aggregate': {
      const v = String(config.outputVariable ?? 'totalAmount');
      return [
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'Aggregate result value',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_operation}}`, label: `${v}_operation`,
          description: 'Operation used for the aggregate',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_column}}`, label: `${v}_column`,
          description: 'Column aggregated (if applicable)',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_sourceCount}}`, label: `${v}_sourceCount`,
          description: 'Number of rows in the source DataTable',
        },
      ];
    }

    case 'datatable.join': {
      const v = String(config.outputVariable ?? 'joinedTable');
      const vars = [
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}}}`, label: v,
          description: 'Joined DataTable rows as JSON array of objects',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Number of rows in the joined table',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_columns}}`, label: `${v}_columns`,
          description: 'JSON array of final column names',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_first}}`, label: `${v}_first`,
          description: 'First joined row as JSON object',
        },
      ];
      for (let i = 0; i < 10; i++) {
        vars.push({
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_${i}}}`, label: `${v}_${i}`,
          description: `Joined row ${i} as JSON object`,
        });
      }
      return vars;
    }

    case 'http.request': {
      const v = String(config.outputVariable ?? 'apiResult');
      return [
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'Raw HTTP response body',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_statusCode}}`, label: `${v}_statusCode`,
          description: 'HTTP status code',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_isSuccess}}`, label: `${v}_isSuccess`,
          description: 'True when status code is 2xx',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_headers}}`, label: `${v}_headers`,
          description: 'Response headers as JSON object',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_body}}`, label: `${v}_body`,
          description: 'Raw HTTP response body',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_contentType}}`, label: `${v}_contentType`,
          description: 'Response content type',
        },
      ];
    }

    case 'json.parse': {
      const v = String(config.outputVariable ?? 'parsed');
      const vars = [
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}}}`, label: v,
          description: 'Parsed value, JSON string, or DataTable JSON',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Number of items when result is an array or table',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_first}}`, label: `${v}_first`,
          description: 'First item when result is an array or table',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_columns}}`, label: `${v}_columns`,
          description: 'Column names when output mode is table',
        },
      ];
      for (let i = 0; i < 10; i++) {
        vars.push({
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_${i}}}`, label: `${v}_${i}`,
          description: `Item ${i} when result is an array or table`,
        });
      }
      return vars;
    }

    case 'sql.query': {
      const v = String(config.outputVariable ?? 'sqlResult');
      const vars = [
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}}}`, label: v,
          description: 'SQL result rows as JSON array of objects',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Number of rows returned',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_columns}}`, label: `${v}_columns`,
          description: 'JSON array of column names',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_first}}`, label: `${v}_first`,
          description: 'First row as JSON object',
        },
      ];
      for (let i = 0; i < 10; i++) {
        vars.push({
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_${i}}}`, label: `${v}_${i}`,
          description: `Row ${i} as JSON object`,
        });
      }
      return vars;
    }

    case 'sql.execute': {
      return [
        {
          ...base, kind: 'variable' as const,
          insertValue: '{{rowsAffected}}', label: 'rowsAffected',
          description: 'Number of rows affected by the statement',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: '{{executionSucceeded}}', label: 'executionSucceeded',
          description: 'true when execution completed successfully',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: '{{executionDurationMs}}', label: 'executionDurationMs',
          description: 'Execution duration in milliseconds',
        },
      ];
    }

    case 'sql.stored-procedure': {
      const v = String(config.outputVariable ?? 'procResult');
      const vars: AvailableVariable[] = [
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}}}`, label: v,
          description: 'First result set rows as JSON array',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_returnValue}}`, label: `${v}_returnValue`,
          description: 'Stored procedure return value',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_rowsAffected}}`, label: `${v}_rowsAffected`,
          description: 'Total rows affected by the procedure',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_resultCount}}`, label: `${v}_resultCount`,
          description: 'Number of rows in the first result set',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Number of rows in the first result set',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_columns}}`, label: `${v}_columns`,
          description: 'JSON array of column names from first result set',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_first}}`, label: `${v}_first`,
          description: 'First row from the first result set',
        },
      ];
      if (Array.isArray(config.parameters)) {
        for (const item of config.parameters) {
          const row = item as Record<string, unknown>;
          const direction = String(row.direction ?? 'Input');
          const name = String(row.name ?? '').trim();
          if (name && (direction.toLowerCase() === 'output' || direction.toLowerCase() === 'inputoutput')) {
            vars.push({
              ...base, kind: 'variable' as const,
              insertValue: `{{${v}_${name}}}`, label: `${v}_${name}`,
              description: `Output parameter ${name}`,
            });
          }
        }
      }
      for (let i = 0; i < 10; i++) {
        vars.push({
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_${i}}}`, label: `${v}_${i}`,
          description: `Row ${i} from first result set`,
        });
      }
      return vars;
    }

    case 'browser.get-text': {
      const v = String(config.outputVariable ?? 'browserText');
      return [
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'Text content read from the page element',
        },
      ];
    }

    case 'browser.get-attribute': {
      const v = String(config.outputVariable ?? 'browserAttribute');
      return [
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'Attribute value read from the page element',
        },
      ];
    }

    case 'browser.screenshot':
    case 'browser.download':
    case 'browser.wait-for-download':
    case 'browser.wait-download': {
      let name = String(config.artifactName ?? 'browser-artifact');
      if (stepType === 'browser.screenshot' && !name.toLowerCase().endsWith('.png')) {
        name += '.png';
      }
      const vars: AvailableVariable[] = [
        {
          ...base, kind: 'artifact',
          insertValue: name, label: name,
          description: stepType === 'browser.screenshot'
            ? 'Screenshot PNG artifact'
            : 'Downloaded file artifact',
        },
      ];
      if (stepType === 'browser.wait-download') {
        vars.push(
          {
            ...base, kind: 'variable',
            insertValue: '{{artifactName}}', label: 'artifactName',
            description: 'Name of the downloaded artifact',
          },
          {
            ...base, kind: 'variable',
            insertValue: '{{downloadedFileName}}', label: 'downloadedFileName',
            description: 'Downloaded file name (may include preserved extension)',
          },
          {
            ...base, kind: 'variable',
            insertValue: '{{downloadedFileSizeBytes}}', label: 'downloadedFileSizeBytes',
            description: 'Downloaded file size in bytes',
          },
        );
      }
      return vars;
    }

    case 'browser.element-exists': {
      const v = String(config.outputVariable ?? 'elementExists');
      const selector = String(config.selector ?? '');
      return [
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'true if element was found within timeout, else false',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}_selector}}`, label: `${v}_selector`,
          description: 'Selector that was checked',
        },
        ...(selector ? [{
          ...base, kind: 'variable' as const,
          insertValue: selector, label: selector,
          description: 'CSS selector checked for existence',
        }] : []),
      ];
    }

    case 'browser.wait-url':
    case 'browser.wait-text':
    case 'browser.wait-network-idle':
      return [];

    case 'browser.upload-file': {
      const selector = String(config.selector ?? "input[type='file']");
      const artifact = String(config.artifactName ?? '');
      const filePath = String(config.filePath ?? '');
      const vars: AvailableVariable[] = [
        {
          ...base, kind: 'variable',
          insertValue: selector, label: selector,
          description: 'CSS selector for the file input',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{uploadedFileName}}', label: 'uploadedFileName',
          description: 'Name of the uploaded file',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{uploadedSourceType}}', label: 'uploadedSourceType',
          description: 'Upload source: artifact or filePath',
        },
      ];
      if (artifact) {
        vars.push({
          ...base, kind: 'artifact',
          insertValue: artifact, label: artifact,
          description: 'Workflow artifact to upload',
        });
      }
      if (filePath) {
        vars.push({
          ...base, kind: 'variable',
          insertValue: filePath, label: filePath,
          description: 'Local file path to upload',
        });
      }
      return vars;
    }

    case 'browser.extract-table': {
      const selector = String(config.selector ?? 'table');
      const rowSelector = String(config.rowSelector ?? '');
      const cellSelector = String(config.cellSelector ?? '');
      const v = String(config.outputVariable ?? 'webTable');
      const vars: AvailableVariable[] = [
        {
          ...base, kind: 'variable',
          insertValue: selector, label: selector,
          description: 'CSS selector for table or scope',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}}}`, label: v,
          description: 'DataTable rows as JSON array of objects',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_count}}`, label: `${v}_count`,
          description: 'Number of data rows in the table',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_columns}}`, label: `${v}_columns`,
          description: 'JSON array of column names',
        },
        {
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_first}}`, label: `${v}_first`,
          description: 'First data row as JSON object',
        },
      ];
      if (rowSelector) {
        vars.push({
          ...base, kind: 'variable',
          insertValue: rowSelector, label: rowSelector,
          description: 'CSS selector for grid rows',
        });
      }
      if (cellSelector) {
        vars.push({
          ...base, kind: 'variable',
          insertValue: cellSelector, label: cellSelector,
          description: 'CSS selector for grid cells',
        });
      }
      for (let i = 0; i < 10; i++) {
        vars.push({
          ...base, kind: 'variable' as const,
          insertValue: `{{${v}_${i}}}`, label: `${v}_${i}`,
          description: `Row ${i} as JSON object`,
        });
      }
      return vars;
    }

    case 'browser.switch-tab':
    case 'browser.close-tab':
    case 'browser.wait-popup': {
      const urlVar = stepType === 'browser.wait-popup' ? 'popupUrl' : 'activePageUrl';
      const titleVar = stepType === 'browser.wait-popup' ? 'popupTitle' : 'activePageTitle';
      const vars: AvailableVariable[] = [
        {
          ...base, kind: 'variable',
          insertValue: `{{${urlVar}}}`, label: urlVar,
          description: 'URL of the active page or popup',
        },
        {
          ...base, kind: 'variable',
          insertValue: `{{${titleVar}}}`, label: titleVar,
          description: 'Title of the active page or popup',
        },
      ];
      if (stepType !== 'browser.wait-popup') {
        vars.push(
          {
            ...base, kind: 'variable',
            insertValue: '{{activePageIndex}}', label: 'activePageIndex',
            description: 'Zero-based index of the active tab',
          },
        );
      }
      if (stepType === 'browser.close-tab') {
        vars.push({
          ...base, kind: 'variable',
          insertValue: '{{tabClosed}}', label: 'tabClosed',
          description: 'true when a tab was closed',
        });
      }
      if (stepType === 'browser.switch-tab') {
        const urlContains = String(config.urlContains ?? '');
        const titleContains = String(config.titleContains ?? '');
        if (urlContains) {
          vars.push({
            ...base, kind: 'variable',
            insertValue: urlContains, label: urlContains,
            description: 'URL pattern used for byUrl mode',
          });
        }
        if (titleContains) {
          vars.push({
            ...base, kind: 'variable',
            insertValue: titleContains, label: titleContains,
            description: 'Title pattern used for byTitle mode',
          });
        }
      }
      if (stepType === 'browser.wait-popup') {
        const clickSelector = String(config.clickSelector ?? '');
        if (clickSelector) {
          vars.push({
            ...base, kind: 'variable',
            insertValue: clickSelector, label: clickSelector,
            description: 'Selector clicked to open the popup',
          });
        }
        vars.push({
          ...base, kind: 'variable',
          insertValue: '{{activePageUrl}}', label: 'activePageUrl',
          description: 'Active page URL after popup switch',
        });
      }
      return vars;
    }

    case 'browser.handle-alert':
    case 'browser.click-and-handle-alert': {
      const promptText = String(config.promptText ?? '');
      const clickSelector = String(config.clickSelector ?? '');
      const vars: AvailableVariable[] = [
        {
          ...base, kind: 'variable',
          insertValue: '{{dialogType}}', label: 'dialogType',
          description: 'Dialog type: alert, confirm, or prompt',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{dialogMessage}}', label: 'dialogMessage',
          description: 'Message text from the dialog',
        },
        {
          ...base, kind: 'variable',
          insertValue: '{{dialogHandled}}', label: 'dialogHandled',
          description: 'true when the dialog was handled',
        },
      ];
      if (promptText) {
        vars.push({
          ...base, kind: 'variable',
          insertValue: promptText, label: promptText,
          description: 'Prompt text sent to prompt dialogs',
        });
      }
      if (clickSelector) {
        vars.push({
          ...base, kind: 'variable',
          insertValue: clickSelector, label: clickSelector,
          description: 'Selector that triggered the dialog',
        });
      }
      return vars;
    }

    case 'browser.evaluate': {
      const v = String(config.outputVariable ?? 'browserEvalResult');
      return [
        {
          ...base, kind: 'variable',
          insertValue: `{{${v}}}`, label: v,
          description: 'JavaScript evaluation result from the page',
        },
      ];
    }

    default:
      return [];
  }
}

/**
 * Returns all nodes that are *upstream* of `nodeId` (direct + transitive ancestors)
 * by following incoming edges backwards through the graph.
 */
export function getUpstreamNodes(
  nodeId: string,
  nodes: Node<WorkflowNodeData>[],
  edges: Edge[],
): Node<WorkflowNodeData>[] {
  const nodeMap = new Map(nodes.map((n) => [n.id, n]));
  const visited = new Set<string>();
  const result: Node<WorkflowNodeData>[] = [];
  const queue = [nodeId];

  while (queue.length > 0) {
    const current = queue.shift()!;
    if (visited.has(current)) continue;
    visited.add(current);

    edges
      .filter((e) => e.target === current)
      .forEach((e) => {
        const src = nodeMap.get(e.source);
        if (src && !visited.has(src.id)) {
          result.push(src);
          queue.push(src.id);
        }
      });
  }

  return result;
}

/**
 * Collects all output variables from all upstream steps of `nodeId`.
 * Used to populate the variable picker in the properties panel.
 */
export function getAvailableVariables(
  nodeId: string,
  nodes: Node<WorkflowNodeData>[],
  edges: Edge[],
): AvailableVariable[] {
  return getUpstreamNodes(nodeId, nodes, edges).flatMap((n) =>
    inferOutputVariables(n),
  );
}
