import { useEffect, useRef, useState } from 'react';
import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from '@microsoft/signalr';
import { getActiveToken } from '../auth/storage';
import type {
  InstanceCompletedEvent,
  StepCompletedEvent,
  StepFailedEvent,
  StepStartedEvent,
} from '../monitoring/applyInstanceMonitoringEvent';

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000';
const HUB_URL = `${API_BASE.replace(/\/$/, '')}/hubs/instances`;

export type LiveConnectionStatus = 'idle' | 'connecting' | 'connected' | 'disconnected';

export interface UseInstanceMonitoringOptions {
  processInstanceId: string | undefined;
  enabled: boolean;
  onStepStarted: (event: StepStartedEvent) => void;
  onStepCompleted: (event: StepCompletedEvent) => void;
  onStepFailed: (event: StepFailedEvent) => void;
  onInstanceCompleted: (event: InstanceCompletedEvent) => void;
  /** Called after JoinInstance succeeds (initial connect and reconnect). */
  onJoined: () => void;
}

export function useInstanceMonitoring({
  processInstanceId,
  enabled,
  onStepStarted,
  onStepCompleted,
  onStepFailed,
  onInstanceCompleted,
  onJoined,
}: UseInstanceMonitoringOptions): { status: LiveConnectionStatus } {
  const [status, setStatus] = useState<LiveConnectionStatus>('idle');
  const callbacksRef = useRef({
    onStepStarted,
    onStepCompleted,
    onStepFailed,
    onInstanceCompleted,
    onJoined,
  });

  callbacksRef.current = {
    onStepStarted,
    onStepCompleted,
    onStepFailed,
    onInstanceCompleted,
    onJoined,
  };

  useEffect(() => {
    if (!enabled || !processInstanceId || !getActiveToken()) {
      setStatus('idle');
      return;
    }

    let disposed = false;
    let connection: HubConnection | null = null;

    const setStatusSafe = (next: LiveConnectionStatus) => {
      if (!disposed) setStatus(next);
    };

    const joinInstance = async (): Promise<boolean> => {
      if (!connection || disposed) return false;
      try {
        await connection.invoke('JoinInstance', processInstanceId);
        if (disposed) return false;
        setStatusSafe('connected');
        callbacksRef.current.onJoined();
        return true;
      } catch {
        setStatusSafe('disconnected');
        return false;
      }
    };

    const buildConnection = () =>
      new HubConnectionBuilder()
        .withUrl(HUB_URL, {
          accessTokenFactory: () => getActiveToken() ?? '',
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(LogLevel.Warning)
        .build();

    const start = async () => {
      setStatusSafe('connecting');
      connection = buildConnection();

      connection.on('instance.step.started', (payload: StepStartedEvent) => {
        callbacksRef.current.onStepStarted(payload);
      });
      connection.on('instance.step.completed', (payload: StepCompletedEvent) => {
        callbacksRef.current.onStepCompleted(payload);
      });
      connection.on('instance.step.failed', (payload: StepFailedEvent) => {
        callbacksRef.current.onStepFailed(payload);
      });
      connection.on('instance.completed', (payload: InstanceCompletedEvent) => {
        callbacksRef.current.onInstanceCompleted(payload);
      });

      connection.onreconnecting(() => {
        setStatusSafe('connecting');
      });

      connection.onreconnected(async () => {
        if (disposed || !connection) return;
        setStatusSafe('connecting');
        await joinInstance();
      });

      connection.onclose(() => {
        setStatusSafe('disconnected');
      });

      try {
        await connection.start();
        if (disposed) {
          await connection.stop();
          return;
        }
        await joinInstance();
      } catch {
        setStatusSafe('disconnected');
      }
    };

    void start();

    return () => {
      disposed = true;
      setStatus('idle');
      if (connection) {
        void connection.invoke('LeaveInstance', processInstanceId).catch(() => undefined);
        void connection.stop();
      }
    };
  }, [enabled, processInstanceId]);

  return { status };
}
