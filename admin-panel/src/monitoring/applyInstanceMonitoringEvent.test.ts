import { describe, expect, it } from 'vitest';
import { ProcessStatus, StepStatus } from '../api/types';
import type { ProcessInstanceDto } from '../api/types';
import {
  applyInstanceCompleted,
  applyStepCompleted,
  applyStepFailed,
  applyStepStarted,
} from './applyInstanceMonitoringEvent';

const baseInstance: ProcessInstanceDto = {
  id: 'inst-1',
  processDefinitionId: 'def-1',
  processDefinitionName: 'Test',
  processVersionId: 'ver-1',
  versionNumber: 1,
  status: ProcessStatus.Running,
  correlationId: null,
  inputData: null,
  outputData: null,
  errorMessage: null,
  startedAt: '2026-06-29T09:00:00Z',
  completedAt: null,
  triggeredBy: null,
  createdAt: '2026-06-29T09:00:00Z',
  steps: [
    {
      id: 'step-inst-1',
      stepId: 'read',
      stepName: 'Read',
      stepType: 'excel.read',
      status: StepStatus.Pending,
      errorMessage: null,
      startedAt: null,
      completedAt: null,
      durationMs: null,
      attemptNumber: 1,
    },
  ],
};

describe('applyInstanceMonitoringEvent', () => {
  it('applyStepStarted marks step running', () => {
    const updated = applyStepStarted(baseInstance, {
      processInstanceId: 'inst-1',
      stepInstanceId: 'step-inst-1',
      stepKey: 'read',
      stepName: 'Read',
      status: 'Running',
      startedAt: '2026-06-29T09:01:00Z',
    });

    expect(updated.steps[0].status).toBe(StepStatus.Running);
    expect(updated.steps[0].startedAt).toBe('2026-06-29T09:01:00Z');
  });

  it('applyStepCompleted marks step success', () => {
    const updated = applyStepCompleted(baseInstance, {
      processInstanceId: 'inst-1',
      stepInstanceId: 'step-inst-1',
      stepKey: 'read',
      stepName: 'Read',
      status: 'Completed',
      completedAt: '2026-06-29T09:02:00Z',
      durationMs: 1200,
    });

    expect(updated.steps[0].status).toBe(StepStatus.Success);
    expect(updated.steps[0].durationMs).toBe(1200);
  });

  it('applyStepFailed sets error message', () => {
    const updated = applyStepFailed(baseInstance, {
      processInstanceId: 'inst-1',
      stepInstanceId: 'step-inst-1',
      stepKey: 'read',
      stepName: 'Read',
      status: 'Failed',
      completedAt: '2026-06-29T09:02:00Z',
      errorMessage: 'boom',
    });

    expect(updated.steps[0].status).toBe(StepStatus.Failed);
    expect(updated.steps[0].errorMessage).toBe('boom');
  });

  it('applyInstanceCompleted updates instance status', () => {
    const updated = applyInstanceCompleted(baseInstance, {
      processInstanceId: 'inst-1',
      status: 'Success',
      completedAt: '2026-06-29T09:10:00Z',
      durationMs: 600000,
    });

    expect(updated.status).toBe(ProcessStatus.Success);
    expect(updated.completedAt).toBe('2026-06-29T09:10:00Z');
  });
});
