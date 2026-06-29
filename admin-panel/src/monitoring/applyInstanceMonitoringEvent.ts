import type { ProcessInstanceDto } from '../api/types';
import { ProcessStatus, StepStatus } from '../api/types';

export interface StepStartedEvent {
  processInstanceId: string;
  stepInstanceId: string;
  stepKey: string;
  stepName: string;
  status: string;
  startedAt: string;
}

export interface StepCompletedEvent {
  processInstanceId: string;
  stepInstanceId: string;
  stepKey: string;
  stepName: string;
  status: string;
  completedAt: string;
  durationMs: number | null;
}

export interface StepFailedEvent {
  processInstanceId: string;
  stepInstanceId: string;
  stepKey: string;
  stepName: string;
  status: string;
  completedAt: string;
  errorMessage: string | null;
}

export interface InstanceCompletedEvent {
  processInstanceId: string;
  status: string;
  completedAt: string;
  durationMs: number | null;
}

function mapInstanceStatus(status: string): ProcessStatus {
  switch (status) {
    case 'Success':
      return ProcessStatus.Success;
    case 'Failed':
      return ProcessStatus.Failed;
    case 'Cancelled':
      return ProcessStatus.Cancelled;
    case 'Running':
      return ProcessStatus.Running;
    default:
      return ProcessStatus.Pending;
  }
}

function findStepIndex(inst: ProcessInstanceDto, stepInstanceId: string, stepKey: string): number {
  const byId = inst.steps.findIndex((s) => s.id === stepInstanceId);
  if (byId >= 0) return byId;
  return inst.steps.findIndex((s) => s.stepId === stepKey);
}

export function applyStepStarted(inst: ProcessInstanceDto, event: StepStartedEvent): ProcessInstanceDto {
  const idx = findStepIndex(inst, event.stepInstanceId, event.stepKey);
  if (idx < 0) return inst;

  const steps = [...inst.steps];
  steps[idx] = {
    ...steps[idx],
    status: StepStatus.Running,
    startedAt: event.startedAt,
    errorMessage: null,
  };

  return {
    ...inst,
    status: ProcessStatus.Running,
    steps,
  };
}

export function applyStepCompleted(inst: ProcessInstanceDto, event: StepCompletedEvent): ProcessInstanceDto {
  const idx = findStepIndex(inst, event.stepInstanceId, event.stepKey);
  if (idx < 0) return inst;

  const steps = [...inst.steps];
  steps[idx] = {
    ...steps[idx],
    status: StepStatus.Success,
    completedAt: event.completedAt,
    durationMs: event.durationMs,
    errorMessage: null,
  };

  return { ...inst, steps };
}

export function applyStepFailed(inst: ProcessInstanceDto, event: StepFailedEvent): ProcessInstanceDto {
  const idx = findStepIndex(inst, event.stepInstanceId, event.stepKey);
  if (idx < 0) return inst;

  const steps = [...inst.steps];
  steps[idx] = {
    ...steps[idx],
    status: StepStatus.Failed,
    completedAt: event.completedAt,
    errorMessage: event.errorMessage,
  };

  return { ...inst, steps };
}

export function applyInstanceCompleted(
  inst: ProcessInstanceDto,
  event: InstanceCompletedEvent,
): ProcessInstanceDto {
  return {
    ...inst,
    status: mapInstanceStatus(event.status),
    completedAt: event.completedAt,
  };
}
