import type { Edge, Node } from 'reactflow';
import { NODE_DEFS } from './nodeDefinitions';
import { getAvailableVariables } from './utils';
import type { WorkflowNodeData } from './types';

/** Config field that receives an upstream artifact reference for each step type. */
export const ARTIFACT_INPUT_FIELD_BY_STEP: Record<string, string> = {
  'excel.read':              'artifactName',
  'excel.read-range':        'inputArtifactName',
  'excel.write-datatable':   'inputArtifactName',
  'excel.append-datatable':  'inputArtifactName',
  'excel.transform':         'inputArtifactName',
  'excel.split':             'inputArtifactName',
  'csv.read':                'inputArtifactName',
  'json.read-file':          'inputArtifactName',
  'zip.extract':             'inputArtifactName',
  'pdf.read-text':           'inputArtifactName',
  'pdf.extract-table':       'inputArtifactName',
  'word.fill-template':      'inputArtifactName',
  'folder.write-file':       'artifactName',
  'browser.upload-file':     'artifactName',
};

export function getArtifactInputField(stepType: string): string | null {
  return ARTIFACT_INPUT_FIELD_BY_STEP[stepType] ?? null;
}

export function expectsArtifactInput(stepType: string): boolean {
  return getArtifactInputField(stepType) !== null;
}

export function getDefaultArtifactFieldValue(stepType: string): string {
  const field = getArtifactInputField(stepType);
  if (!field) return '';
  const defaultConfig = NODE_DEFS[stepType]?.defaultConfig;
  if (!defaultConfig) return '';
  return String(defaultConfig[field] ?? '');
}

export function isAutoBindableArtifactValue(
  stepType: string,
  currentValue: string,
): boolean {
  const trimmed = currentValue.trim();
  if (!trimmed) return true;
  return trimmed === getDefaultArtifactFieldValue(stepType);
}

export function collectUpstreamArtifactBindings(
  nodeId: string,
  nodes: Node<WorkflowNodeData>[],
  edges: Edge[],
): string[] {
  const vars = getAvailableVariables(nodeId, nodes, edges);
  const bindings: string[] = [];
  const seen = new Set<string>();

  for (const v of vars) {
    if (v.kind !== 'artifact') continue;
    if (seen.has(v.insertValue)) continue;
    seen.add(v.insertValue);
    bindings.push(v.insertValue);
  }

  for (const v of vars) {
    if (v.sourceStepType !== 'mail.read-attachments') continue;
    if (!v.label.endsWith('_0')) continue;
    if (seen.has(v.insertValue)) continue;
    seen.add(v.insertValue);
    bindings.push(v.insertValue);
  }

  return bindings;
}

export function resolveSingleUpstreamArtifact(
  nodeId: string,
  nodes: Node<WorkflowNodeData>[],
  edges: Edge[],
): string | null {
  const bindings = collectUpstreamArtifactBindings(nodeId, nodes, edges);
  return bindings.length === 1 ? bindings[0] : null;
}

export type AutoBindResult = 'applied' | 'pending' | 'skipped';

export function tryAutoBindArtifactForNode(
  node: Node<WorkflowNodeData>,
  nodes: Node<WorkflowNodeData>[],
  edges: Edge[],
  manualFields?: ReadonlySet<string>,
): { result: AutoBindResult; config?: Record<string, unknown> } {
  const field = getArtifactInputField(node.data.stepType);
  if (!field) return { result: 'skipped' };

  if (manualFields?.has(field)) return { result: 'skipped' };

  const current = String(node.data.config[field] ?? '');
  if (!isAutoBindableArtifactValue(node.data.stepType, current)) {
    return { result: 'skipped' };
  }

  const bindings = collectUpstreamArtifactBindings(node.id, nodes, edges);
  if (bindings.length === 0) return { result: 'pending' };
  if (bindings.length > 1) return { result: 'skipped' };

  return {
    result: 'applied',
    config: { ...node.data.config, [field]: bindings[0] },
  };
}

export function applyAutoBindToNodes(
  nodes: Node<WorkflowNodeData>[],
  edges: Edge[],
  targetNodeIds: Iterable<string>,
  manualFieldsByNode: ReadonlyMap<string, ReadonlySet<string>>,
): { nodes: Node<WorkflowNodeData>[]; awaiting: string[] } {
  const targetSet = new Set(targetNodeIds);
  const awaiting: string[] = [];

  const nextNodes = nodes.map((node) => {
    if (!targetSet.has(node.id)) return node;

    const attempt = tryAutoBindArtifactForNode(
      node,
      nodes,
      edges,
      manualFieldsByNode.get(node.id),
    );

    if (attempt.result === 'applied' && attempt.config) {
      return { ...node, data: { ...node.data, config: attempt.config } };
    }

    if (attempt.result === 'pending') awaiting.push(node.id);
    return node;
  });

  return { nodes: nextNodes, awaiting };
}

export function applyArtifactBindingToConfig(
  stepType: string,
  config: Record<string, unknown>,
  artifactValue: string,
): Record<string, unknown> | null {
  const field = getArtifactInputField(stepType);
  if (!field) return null;
  return { ...config, [field]: artifactValue };
}
