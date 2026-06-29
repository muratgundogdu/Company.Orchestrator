import type { Edge, Node } from 'reactflow';
import { describe, expect, it } from 'vitest';
import {
  applyArtifactBindingToConfig,
  applyAutoBindToNodes,
  collectUpstreamArtifactBindings,
  isAutoBindableArtifactValue,
  resolveSingleUpstreamArtifact,
  tryAutoBindArtifactForNode,
} from './artifactBinding';
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

describe('applyArtifactBindingToConfig', () => {
  it('binds picker artifact to inputArtifactName for excel.transform', () => {
    const config = applyArtifactBindingToConfig(
      'excel.transform',
      { inputArtifactName: '', outputName: 'out' },
      'tsess.xlsx',
    );

    expect(config?.inputArtifactName).toBe('tsess.xlsx');
  });

  it('binds picker artifact to artifactName for folder.write-file', () => {
    const config = applyArtifactBindingToConfig(
      'folder.write-file',
      { artifactName: '', destinationPath: 'C:\\Output' },
      'tsess.xlsx',
    );

    expect(config?.artifactName).toBe('tsess.xlsx');
  });
});

describe('tryAutoBindArtifactForNode', () => {
  it('auto-binds excel.transform to excel.transform when single upstream artifact exists', () => {
    const first = node('t1', 'excel.transform', 'First', {
      inputArtifactName: 'source-file',
      outputName: 'tsess',
      operations: [],
    });
    const second = node('t2', 'excel.transform', 'Second', {
      inputArtifactName: 'source-file',
      outputName: 'next',
      operations: [],
    });
    const nodes = [first, second];
    const edges = [{ id: 'e1', source: 't1', target: 't2' }];

    const attempt = tryAutoBindArtifactForNode(second, nodes, edges);
    expect(attempt.result).toBe('applied');
    expect(attempt.config?.inputArtifactName).toBe('tsess.xlsx');
  });

  it('auto-binds excel.transform output to folder.write-file artifactName', () => {
    const transform = node('t1', 'excel.transform', 'Transform', {
      outputName: 'tsess',
      operations: [],
    });
    const write = node('w1', 'folder.write-file', 'Write', {
      artifactName: 'transformed-excel.xlsx',
      destinationPath: 'C:\\Output',
    });
    const nodes = [transform, write];
    const edges = [{ id: 'e1', source: 't1', target: 'w1' }];

    const attempt = tryAutoBindArtifactForNode(write, nodes, edges);
    expect(attempt.result).toBe('applied');
    expect(attempt.config?.artifactName).toBe('tsess.xlsx');
  });

  it('auto-binds mail.read-attachments first output to excel.read artifactName', () => {
    const mail = node('m1', 'mail.read-attachments', 'Mail', {
      outputVariable: 'mailList',
    });
    const excelRead = node('e1', 'excel.read', 'Excel Read', {
      artifactName: '',
      sheetName: 'Sheet1',
    });
    const nodes = [mail, excelRead];
    const edges = [{ id: 'e1', source: 'm1', target: 'e1' }];

    const bindings = collectUpstreamArtifactBindings('e1', nodes, edges);
    expect(bindings).toEqual(['{{mailList_0}}']);

    const attempt = tryAutoBindArtifactForNode(excelRead, nodes, edges);
    expect(attempt.result).toBe('applied');
    expect(attempt.config?.artifactName).toBe('{{mailList_0}}');
  });

  it('does not auto-bind when multiple upstream artifacts exist', () => {
    const first = node('t1', 'excel.transform', 'First', {
      outputName: 'one',
      operations: [],
    });
    const second = node('t2', 'excel.transform', 'Second', {
      outputName: 'two',
      operations: [],
    });
    const target = node('t3', 'excel.transform', 'Third', {
      inputArtifactName: 'source-file',
      outputName: 'three',
      operations: [],
    });
    const nodes = [first, second, target];
    const edges = [
      { id: 'e1', source: 't1', target: 't3' },
      { id: 'e2', source: 't2', target: 't3' },
    ];

    expect(resolveSingleUpstreamArtifact('t3', nodes, edges)).toBeNull();
    const attempt = tryAutoBindArtifactForNode(target, nodes, edges);
    expect(attempt.result).toBe('skipped');
    expect(attempt.config).toBeUndefined();
  });

  it('skips auto-bind after manual artifact field edit', () => {
    const first = node('t1', 'excel.transform', 'First', {
      outputName: 'tsess',
      operations: [],
    });
    const second = node('t2', 'excel.transform', 'Second', {
      inputArtifactName: 'manual.xlsx',
      outputName: 'next',
      operations: [],
    });
    const nodes = [first, second];
    const edges = [{ id: 'e1', source: 't1', target: 't2' }];

    const attempt = tryAutoBindArtifactForNode(second, nodes, edges, new Set(['inputArtifactName']));
    expect(attempt.result).toBe('skipped');
  });
});

describe('applyAutoBindToNodes', () => {
  it('leaves node awaiting bind when upstream edge is not connected yet', () => {
    const created = node('t2', 'excel.transform', 'Second', {
      inputArtifactName: 'source-file',
      outputName: 'next',
      operations: [],
    });
    const { nodes, awaiting } = applyAutoBindToNodes([created], [], ['t2'], new Map());
    expect(awaiting).toEqual(['t2']);
    expect(nodes[0].data.config.inputArtifactName).toBe('source-file');
  });

  it('treats template default values as auto-bindable', () => {
    expect(isAutoBindableArtifactValue('excel.transform', 'source-file')).toBe(true);
    expect(isAutoBindableArtifactValue('excel.transform', 'manual.xlsx')).toBe(false);
  });
});
