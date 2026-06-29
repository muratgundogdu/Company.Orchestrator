import { describe, expect, it } from 'vitest';
import type { Node } from 'reactflow';
import {
  categorizePickerVariable,
  groupPickerVariables,
  normalizeArtifactOutputName,
  resolveRegisteredNodeOutputs,
} from './nodeOutputDiscovery';
import { getAvailableVariables, inferOutputVariables } from './utils';
import type { WorkflowNodeData } from './types';

function node(
  id: string,
  stepType: string,
  name: string,
  config: Record<string, unknown>,
): Node<WorkflowNodeData> {
  return {
    id,
    type: 'workflowNode',
    position: { x: 0, y: 0 },
    data: { name, stepType, config },
  };
}

describe('normalizeArtifactOutputName', () => {
  it('appends extension when missing', () => {
    expect(normalizeArtifactOutputName('transformed-excel', '.xlsx')).toBe('transformed-excel.xlsx');
  });

  it('does not double-append extension', () => {
    expect(normalizeArtifactOutputName('transformed-excel.xlsx', '.xlsx')).toBe('transformed-excel.xlsx');
  });
});

describe('resolveRegisteredNodeOutputs', () => {
  it('exposes excel.transform artifact with normalized extension', () => {
    const outputs = resolveRegisteredNodeOutputs(
      'excel.transform',
      { outputName: 'transformed-excel.xlsx' },
      { sourceNodeId: 't1', sourceNodeName: 'Transform', sourceStepType: 'excel.transform' },
    );

    const artifacts = outputs?.filter((o) => o.kind === 'artifact') ?? [];
    expect(artifacts).toHaveLength(1);
    expect(artifacts[0].insertValue).toBe('transformed-excel.xlsx');
    expect(artifacts[0].label).toBe('transformed-excel.xlsx');
  });

  it('keeps mail.read-attachments indexed outputs', () => {
    const outputs = resolveRegisteredNodeOutputs(
      'mail.read-attachments',
      { outputVariable: 'mailList' },
      { sourceNodeId: 'm1', sourceNodeName: 'Mail', sourceStepType: 'mail.read-attachments' },
    ) ?? [];

    expect(outputs.some((o) => o.label === 'mailList_0')).toBe(true);
    expect(outputs.some((o) => o.label === 'mailList_1')).toBe(true);
    expect(outputs.some((o) => o.label === 'mailList_count')).toBe(true);
  });
});

describe('getAvailableVariables', () => {
  it('shows excel.transform artifact on downstream node picker', () => {
    const transform = node('transform-1', 'excel.transform', 'Transform Excel', {
      inputArtifactName: 'source.xlsx',
      outputName: 'transformed-excel.xlsx',
      operations: [],
    });
    const downstream = node('write-1', 'folder.write-file', 'Write Output', {
      artifactName: '',
      destinationPath: 'C:\\Output',
    });

    const vars = getAvailableVariables('write-1', [transform, downstream], [
      { id: 'e1', source: 'transform-1', target: 'write-1' },
    ]);

    const artifacts = vars.filter((v) => v.kind === 'artifact');
    expect(artifacts.some((v) => v.insertValue === 'transformed-excel.xlsx')).toBe(true);
  });

  it('chains two excel.transform outputs for second transform input picker', () => {
    const first = node('transform-1', 'excel.transform', 'First Transform', {
      outputName: 'step-one',
      operations: [],
    });
    const second = node('transform-2', 'excel.transform', 'Second Transform', {
      inputArtifactName: '',
      outputName: 'step-two',
      operations: [],
    });

    const vars = getAvailableVariables('transform-2', [first, second], [
      { id: 'e1', source: 'transform-1', target: 'transform-2' },
    ]);

    const artifacts = vars
      .filter((v) => v.kind === 'artifact' && v.sourceNodeId === 'transform-1')
      .map((v) => v.insertValue);

    expect(artifacts).toContain('step-one.xlsx');
    expect(artifacts).not.toContain('step-one.xlsx.xlsx');
  });
});

describe('groupPickerVariables', () => {
  it('groups artifacts separately from step outputs', () => {
    const transformVars = inferOutputVariables(
      node('t1', 'excel.transform', 'Transform', { outputName: 'out' }),
    );
    const grouped = groupPickerVariables(transformVars);

    expect(grouped.artifacts.length).toBeGreaterThan(0);
    expect(grouped['step-outputs'].some((v) => v.label === 'transformedArtifactName')).toBe(true);
    expect(categorizePickerVariable(grouped.artifacts[0])).toBe('artifacts');
  });
});
