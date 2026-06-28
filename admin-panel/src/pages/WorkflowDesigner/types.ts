// ── Workflow definition (backend-compatible JSON schema) ──────────────────────

export interface RetryPolicy {
  maxAttempts: number;
  delaySeconds: number;
}

export interface WorkflowStep {
  id: string;
  type: string;
  name: string;
  config: Record<string, unknown>;
  /** Canonical backend field — ID of the next step to execute on success. */
  nextStepId?: string | string[];
  /** For condition.if: step to execute when the condition evaluates to true. */
  trueStepId?: string;
  /** For condition.if: step to execute when the condition evaluates to false. */
  falseStepId?: string;
  /** Step to route to when this step fails (Phase 10 error handling). */
  onFailureStepId?: string;
  /** Legacy export field — kept for import round-trip compatibility. */
  onSuccess?: string | string[];
  /** Alternative dependency declaration (older schema style). */
  dependsOn?: string | string[];
  /** Per-step retry policy. */
  retry?: RetryPolicy;
  // ── foreach.loop / foreach.row routing ──────────────────────────────────────
  /** First step of the loop body, executed once per item/row. */
  loopStepId?: string;
  /** Step to execute after all items/rows are processed. */
  completedStepId?: string;
}

export interface WorkflowDefinition {
  name: string;
  description?: string;
  version: string;
  steps: WorkflowStep[];
}

// ── React Flow node data ───────────────────────────────────────────────────────

export interface WorkflowNodeData {
  /** Step display name (editable). */
  name: string;
  /** Backend step type identifier, e.g. "mail.read-attachments". */
  stepType: string;
  /** Step configuration object. */
  config: Record<string, unknown>;
  /** Optional per-step retry policy. Absent means no retries. */
  retry?: RetryPolicy;
}

// ── Variable binding ───────────────────────────────────────────────────────────

/**
 * An output value produced by an upstream step, shown in the variable picker.
 * - `variable`: referenced as `{{name}}` in config fields
 * - `artifact`:  referenced as a literal artifact name string
 */
export interface AvailableVariable {
  /** Exact string to insert into a JSON config field. */
  insertValue: string;
  /** Short display label (variable name without braces, or artifact filename). */
  label: string;
  /** Human-readable tooltip description. */
  description: string;
  sourceNodeId: string;
  sourceNodeName: string;
  kind: 'variable' | 'artifact';
}
