import 'reactflow/dist/style.css';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import ReactFlow, {
  addEdge,
  Background,
  BackgroundVariant,
  Connection,
  Controls,
  Edge,
  MarkerType,
  MiniMap,
  Node,
  NodeTypes,
  OnSelectionChangeParams,
  ReactFlowInstance,
  useEdgesState,
  useNodesState,
} from 'reactflow';
import {
  Download,
  Upload,
  RotateCcw,
  Copy,
  Check,
  GitBranch,
  Maximize2,
  FileJson,
  FolderOpen,
  Save,
  Send,
  Play,
  AlertCircle,
  Calendar,
} from 'lucide-react';

import WorkflowNode from './WorkflowNode';
import ConditionNode from './ConditionNode';
import ForEachNode from './ForEachNode';
import NodeToolbox from './NodeToolbox';
import PropertiesPanel from './PropertiesPanel';
import SaveModal, { type SaveResult } from './SaveModal';
import OpenModal, { type OpenResult } from './OpenModal';
import RunModal from './RunModal';
import ScheduleModal from './ScheduleModal';
import ValidationModal from './ValidationModal';
import { validateWorkflow, type ValidationResult } from './validation';
import { ValidationContext } from './ValidationContext';
import { useAuth } from '../../auth/AuthContext';
import { Permissions } from '../../auth/permissions';
import { NODE_DEFS } from './nodeDefinitions';
import {
  buildWorkflowDefinition,
  generateNodeId,
  getAvailableVariables,
  getNodeDef,
  getRFNodeType,
  parseWorkflowDefinition,
} from './utils';
import type { WorkflowNodeData } from './types';
import { VersionStatus } from '../../api/types';
import type { TriggerDto } from '../../api/types';
import { VersionStatusBadge } from '../../components/StatusBadge';
import { processDefinitionApi } from '../../api/endpoints';
import type { AvailableVariable } from './types';

// ── System / error-handler variables always available in context at runtime ───
// These are injected by WorkflowEngine when a step fails and an onFailureStepId
// is set. They appear in the variable picker so users can reference them without
// having to remember the exact names.
const SYSTEM_ERROR_VARIABLES: AvailableVariable[] = [
  { sourceNodeId: '__system__', sourceNodeName: 'System — Error Context', insertValue: '{{errorMessage}}',              label: 'errorMessage',              description: 'Error message from the failed step',                             kind: 'variable' },
  { sourceNodeId: '__system__', sourceNodeName: 'System — Error Context', insertValue: '{{failedStepId}}',              label: 'failedStepId',              description: 'ID of the step that failed',                                     kind: 'variable' },
  { sourceNodeId: '__system__', sourceNodeName: 'System — Error Context', insertValue: '{{failedStepName}}',            label: 'failedStepName',            description: 'Name of the step that failed',                                   kind: 'variable' },
  { sourceNodeId: '__system__', sourceNodeName: 'System — Error Context', insertValue: '{{failedStepType}}',            label: 'failedStepType',            description: 'Type of the step that failed',                                   kind: 'variable' },
  { sourceNodeId: '__system__', sourceNodeName: 'System — Error Context', insertValue: '{{failureReportArtifactName}}', label: 'failureReportArtifactName', description: 'Name of the auto-generated failure report artifact (text/plain)', kind: 'artifact' },
];

/** Generic loop-context variables shown when a foreach.loop is upstream. */
const SYSTEM_LOOP_VARIABLES: AvailableVariable[] = [
  { sourceNodeId: '__loop__', sourceNodeName: 'Loop Context', insertValue: '{{currentItem}}',  label: 'currentItem',  description: 'Current item value in the For Each loop',        kind: 'variable' },
  { sourceNodeId: '__loop__', sourceNodeName: 'Loop Context', insertValue: '{{currentIndex}}', label: 'currentIndex', description: 'Zero-based index of the current loop iteration',  kind: 'variable' },
];

/** Injected when a workflow is started by a FolderWatcher trigger. */
const FOLDER_WATCHER_TRIGGER_VARIABLES: AvailableVariable[] = [
  { sourceNodeId: '__trigger__', sourceNodeName: 'FolderWatcher Trigger', insertValue: '{{triggerFilePath}}',         label: 'triggerFilePath',         description: 'Full path to the file (processing folder when move-to-processing is enabled)', kind: 'variable' },
  { sourceNodeId: '__trigger__', sourceNodeName: 'FolderWatcher Trigger', insertValue: '{{triggerProcessingPath}}',   label: 'triggerProcessingPath',   description: 'Same as triggerFilePath — path after move to processing folder',             kind: 'variable' },
  { sourceNodeId: '__trigger__', sourceNodeName: 'FolderWatcher Trigger', insertValue: '{{triggerOriginalFilePath}}', label: 'triggerOriginalFilePath', description: 'Original full path before move to processing folder',                          kind: 'variable' },
  { sourceNodeId: '__trigger__', sourceNodeName: 'FolderWatcher Trigger', insertValue: '{{triggerFileName}}',         label: 'triggerFileName',         description: 'File name only (e.g. data.xlsx)',                                              kind: 'variable' },
  { sourceNodeId: '__trigger__', sourceNodeName: 'FolderWatcher Trigger', insertValue: '{{triggerDirectory}}',        label: 'triggerDirectory',        description: 'Original directory containing the detected file',                                kind: 'variable' },
];

const nodeTypes: NodeTypes = {
  workflowNode:  WorkflowNode,
  conditionNode: ConditionNode,
  forEachNode:   ForEachNode,
};

// ── Sample starter workflow ────────────────────────────────────────────────────
const STARTER_JSON = JSON.stringify(
  {
    name: 'my-workflow',
    version: '1',
    steps: [
      {
        id: 'read-folder',
        type: 'folder.read-file',
        name: 'Read Source File',
        config: {
          sourcePath: '{{triggerFilePath}}',
          artifactName: 'source-file',
        },
        nextStepId: 'transform',
      },
      {
        id: 'transform',
        type: 'excel.transform',
        name: 'Transform Excel',
        config: {
          inputArtifactName: 'source-file',
          outputName: 'transformed-excel',
          operations: [{ type: 'addSheet', sheetName: 'Result' }],
        },
        nextStepId: 'write-output',
      },
      {
        id: 'write-output',
        type: 'folder.write-file',
        name: 'Write Output',
        config: {
          artifactName: 'transformed-excel.xlsx',
          destinationPath: 'C:\\Temp\\AlterOneOutput\\transformed-excel.xlsx',
          overwrite: true,
        },
      },
    ],
  },
  null,
  2,
);

export default function WorkflowDesigner() {
  const { hasPermission } = useAuth();
  const canEdit = hasPermission(Permissions.WorkflowEdit);
  const canExecute = hasPermission(Permissions.WorkflowExecute);

  // ── React Flow state ─────────────────────────────────────────────────────────
  const [nodes, setNodes, onNodesChange] = useNodesState<WorkflowNodeData>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState([]);
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const [rfInstance, setRfInstance] = useState<ReactFlowInstance | null>(null);

  // ── Workflow metadata ────────────────────────────────────────────────────────
  const [workflowName, setWorkflowName]       = useState('my-workflow');
  const [workflowVersion, setWorkflowVersion] = useState('1');
  const [workflowDesc, setWorkflowDesc]       = useState('');

  // ── Backend state ────────────────────────────────────────────────────────────
  const [currentDefinitionId, setCurrentDefinitionId] = useState<string | null>(null);
  const [currentVersionId, setCurrentVersionId]       = useState<string | null>(null);
  const [currentVersionNumber, setCurrentVersionNumber] = useState<number | null>(null);
  const [versionStatus, setVersionStatus]             = useState<VersionStatus | null>(null);
  const [isDirty, setIsDirty]                         = useState(false);
  const [publishingVersion, setPublishingVersion]     = useState(false);
  const [publishError, setPublishError]               = useState<string | null>(null);

  // ── Selection + variable binding ─────────────────────────────────────────────
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const selectedNode = useMemo(
    () => nodes.find((n) => n.id === selectedNodeId) ?? null,
    [nodes, selectedNodeId],
  );
  const availableVariables = useMemo(() => {
    const upstream = selectedNodeId ? getAvailableVariables(selectedNodeId, nodes, edges) : [];
    const hasUpstreamForEach = upstream.some(
      (v) => {
        const stepType = nodes.find((n) => n.id === v.sourceNodeId)?.data.stepType;
        return stepType === 'foreach.loop' || stepType === 'foreach.row' || stepType === 'foreach.file';
      },
    );
    return [
      ...upstream,
      ...FOLDER_WATCHER_TRIGGER_VARIABLES,
      ...SYSTEM_ERROR_VARIABLES,
      ...(hasUpstreamForEach ? SYSTEM_LOOP_VARIABLES : []),
    ];
  }, [selectedNodeId, nodes, edges]);

  // ── Trigger state ─────────────────────────────────────────────────────────────
  const [currentTriggerId, setCurrentTriggerId]     = useState<string | null>(null);
  const [currentTriggerName, setCurrentTriggerName] = useState('');
  const [currentTriggerCron, setCurrentTriggerCron] = useState('');

  // ── Validation state ──────────────────────────────────────────────────────────
  const EMPTY_RESULT: ValidationResult = { errors: [], warnings: [] };
  const [validationResult, setValidationResult] = useState<ValidationResult>(EMPTY_RESULT);
  const [showValidationModal, setShowValidationModal] = useState(false);
  const [validationAction, setValidationAction]       = useState<'run' | 'publish'>('run');

  // Reactive (debounced) validation — updates node highlights as user edits
  useEffect(() => {
    if (nodes.length === 0) { setValidationResult(EMPTY_RESULT); return; }
    const t = setTimeout(() => setValidationResult(validateWorkflow(nodes, edges)), 800);
    return () => clearTimeout(t);
  }, [nodes, edges]); // eslint-disable-line react-hooks/exhaustive-deps

  const errorNodeIds = useMemo(
    () => new Set(validationResult.errors.filter(e => e.nodeId).map(e => e.nodeId)),
    [validationResult],
  );
  const warningNodeIds = useMemo(
    () => new Set(validationResult.warnings.filter(w => w.nodeId).map(w => w.nodeId)),
    [validationResult],
  );

  // Per-selected-node errors for PropertiesPanel
  const selectedNodeValidation = useMemo(() => ({
    errors:   validationResult.errors.filter(e => e.nodeId === selectedNodeId),
    warnings: validationResult.warnings.filter(w => w.nodeId === selectedNodeId),
  }), [validationResult, selectedNodeId]);

  // ── Modal state ───────────────────────────────────────────────────────────────
  const [showSave, setShowSave]         = useState(false);
  const [showOpen, setShowOpen]         = useState(false);
  const [showRun, setShowRun]           = useState(false);
  const [showSchedule, setShowSchedule] = useState(false);
  const [showExport, setShowExport] = useState(false);
  const [exportText, setExportText] = useState('');
  const [copied, setCopied]         = useState(false);
  const [showImport, setShowImport] = useState(false);
  const [importText, setImportText] = useState('');
  const [importError, setImportError] = useState<string | null>(null);

  // ── Auto-load definition from ?definitionId URL param ────────────────────────
  useEffect(() => {
    const defId = new URLSearchParams(window.location.search).get('definitionId');
    if (!defId) return;
    // Remove the param so refreshing doesn't re-load
    window.history.replaceState({}, '', window.location.pathname);

    (async () => {
      try {
        const [defRes, versionsRes] = await Promise.all([
          processDefinitionApi.getById(defId),
          processDefinitionApi.getVersions(defId),
        ]);
        const versions = [...versionsRes.data].sort((a, b) => b.versionNumber - a.versionNumber);
        if (versions.length === 0) return;
        const v = versions[0];

        try {
          const parsed = parseWorkflowDefinition(v.jsonDefinition);
          setNodes(parsed.nodes);
          setEdges(parsed.edges);
          setWorkflowName(defRes.data.name);
          setWorkflowVersion(String(v.versionNumber));
          setWorkflowDesc(defRes.data.description ?? '');
        } catch {
          setNodes([]);
          setEdges([]);
          setWorkflowName(defRes.data.name);
          setWorkflowDesc(defRes.data.description ?? '');
        }
        setSelectedNodeId(null);
        setCurrentDefinitionId(defId);
        setCurrentVersionId(v.id);
        setCurrentVersionNumber(v.versionNumber);
        setVersionStatus(v.status);
        setIsDirty(false);
        setTimeout(() => rfInstance?.fitView({ padding: 0.15 }), 200);
      } catch {
        // Silent — user can open manually via the Open button
      }
    })();
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // ── Helpers: build the current workflow JSON ──────────────────────────────────
  function buildCurrentJson(): string {
    return JSON.stringify(
      buildWorkflowDefinition(workflowName, workflowVersion, workflowDesc, nodes, edges),
      null,
      2,
    );
  }

  // ── After save callback ───────────────────────────────────────────────────────
  function handleAfterSave(result: SaveResult) {
    setCurrentDefinitionId(result.definitionId);
    setCurrentVersionId(result.versionId);
    setCurrentVersionNumber(result.versionNumber);
    setVersionStatus(result.versionStatus);
    setWorkflowName(result.definitionName);
    setIsDirty(false);
    setShowSave(false);
  }

  // ── After open callback ───────────────────────────────────────────────────────
  function handleAfterOpen(result: OpenResult) {
    try {
      const parsed = parseWorkflowDefinition(result.versionJson);
      setNodes(parsed.nodes);
      setEdges(parsed.edges);
      setWorkflowName(result.definitionName);
      setWorkflowVersion(String(result.versionNumber));
      setWorkflowDesc(result.definitionDesc);
      setSelectedNodeId(null);
    } catch {
      // If the stored JSON doesn't parse as a workflow, load a blank canvas
      setNodes([]);
      setEdges([]);
      setWorkflowName(result.definitionName);
      setWorkflowDesc(result.definitionDesc);
      setSelectedNodeId(null);
    }
    setCurrentDefinitionId(result.definitionId);
    setCurrentVersionId(result.versionId);
    setCurrentVersionNumber(result.versionNumber);
    setVersionStatus(result.versionStatus);
    setIsDirty(false);
    setShowOpen(false);
    setTimeout(() => rfInstance?.fitView({ padding: 0.15 }), 60);
  }

  // ── After schedule created callback ──────────────────────────────────────────
  function handleAfterScheduled(trigger: TriggerDto) {
    setCurrentTriggerId(trigger.id);
    setCurrentTriggerName(trigger.name);
    setCurrentTriggerCron(trigger.cronExpression ?? '');
    setShowSchedule(false);
  }

  // ── Publish ───────────────────────────────────────────────────────────────────
  async function handlePublish() {
    if (!currentDefinitionId || !currentVersionId) return;

    // Validate before publishing
    const result = validateWorkflow(nodes, edges);
    setValidationResult(result);
    if (result.errors.length > 0) {
      setValidationAction('publish');
      setShowValidationModal(true);
      return;
    }

    setPublishingVersion(true);
    setPublishError(null);
    try {
      const res = await processDefinitionApi.publishVersion(currentDefinitionId, currentVersionId);
      setVersionStatus(res.data.status);
    } catch (e: unknown) {
      setPublishError(e instanceof Error ? e.message : 'Publish failed');
    } finally {
      setPublishingVersion(false);
    }
  }

  // ── Run (with validation gate) ────────────────────────────────────────────────
  function handleRunClick() {
    const result = validateWorkflow(nodes, edges);
    setValidationResult(result);
    if (result.errors.length > 0) {
      setValidationAction('run');
      setShowValidationModal(true);
      return;
    }
    setShowRun(true);
  }

  // ── Mark dirty when nodes/edges change after a save ──────────────────────────
  function markDirty() {
    if (currentDefinitionId) setIsDirty(true);
  }

  // ── Connection handler ────────────────────────────────────────────────────────
  const onConnect = useCallback(
    (params: Connection) => {
      const isTrue      = params.sourceHandle === 'true';
      const isFalse     = params.sourceHandle === 'false';
      const isFailure   = params.sourceHandle === 'failure';
      const isLoopBody  = params.sourceHandle === 'loop-body';
      const isCompleted = params.sourceHandle === 'completed';
      const isLoopBack  = params.targetHandle === 'loop-back';

      setEdges((eds) =>
        addEdge(
          {
            ...params,
            type: 'smoothstep',
            animated: !isFailure && !isLoopBack,
            markerEnd: { type: MarkerType.ArrowClosed },
            ...(isTrue      ? { label: '✓ True',    style: { stroke: '#16a34a' },                  labelStyle: { fill: '#16a34a', fontWeight: 700, fontSize: 10 }, labelBgStyle: { fill: 'white', fillOpacity: 0.85 } } : {}),
            ...(isFalse     ? { label: '✗ False',   style: { stroke: '#dc2626' },                  labelStyle: { fill: '#dc2626', fontWeight: 700, fontSize: 10 }, labelBgStyle: { fill: 'white', fillOpacity: 0.85 } } : {}),
            ...(isFailure   ? { label: '⚠ On Error', style: { stroke: '#dc2626', strokeDasharray: '6,3' }, labelStyle: { fill: '#dc2626', fontWeight: 700, fontSize: 10 }, labelBgStyle: { fill: 'white', fillOpacity: 0.85 } } : {}),
            ...(isLoopBody  ? { label: '↻ Body',    style: { stroke: '#0891b2' },                  labelStyle: { fill: '#0891b2', fontWeight: 700, fontSize: 10 }, labelBgStyle: { fill: 'white', fillOpacity: 0.85 } } : {}),
            ...(isCompleted ? { label: '✓ Done',    style: { stroke: '#16a34a' },                  labelStyle: { fill: '#16a34a', fontWeight: 700, fontSize: 10 }, labelBgStyle: { fill: 'white', fillOpacity: 0.85 } } : {}),
            ...(isLoopBack  ? { label: '↵ back',    style: { stroke: '#64748b', strokeDasharray: '5,4' }, labelStyle: { fill: '#64748b', fontWeight: 700, fontSize: 10 }, labelBgStyle: { fill: 'white', fillOpacity: 0.85 } } : {}),
          },
          eds,
        ),
      );
      markDirty();
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [setEdges, currentDefinitionId],
  );

  // ── Selection change ─────────────────────────────────────────────────────────
  const onSelectionChange = useCallback(({ nodes: sel }: OnSelectionChangeParams) => {
    setSelectedNodeId(sel.length === 1 ? sel[0].id : null);
  }, []);

  // ── Drop from toolbox ─────────────────────────────────────────────────────────
  const onDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
  }, []);

  const onDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      if (!reactFlowWrapper.current || !rfInstance) return;

      const stepType = e.dataTransfer.getData('application/reactflow');
      if (!stepType || !NODE_DEFS[stepType]) return;

      const bounds   = reactFlowWrapper.current.getBoundingClientRect();
      const position = rfInstance.project({ x: e.clientX - bounds.left, y: e.clientY - bounds.top });
      const id  = generateNodeId(stepType);
      const def = getNodeDef(stepType);

      const newNode: Node<WorkflowNodeData> = {
        id,
        type: getRFNodeType(stepType),
        position,
        data: { name: def.label, stepType, config: { ...def.defaultConfig } },
      };

      setNodes((nds) => nds.concat(newNode));
      setSelectedNodeId(id);
      markDirty();
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [rfInstance, setNodes, currentDefinitionId],
  );

  // ── Update selected node data ─────────────────────────────────────────────────
  const updateSelectedNode = useCallback(
    (data: WorkflowNodeData) => {
      setNodes((nds) => nds.map((n) => (n.id === selectedNodeId ? { ...n, data } : n)));
      markDirty();
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [selectedNodeId, setNodes, currentDefinitionId],
  );

  // ── Delete selected node ──────────────────────────────────────────────────────
  const deleteSelectedNode = useCallback(() => {
    if (!selectedNodeId) return;
    setNodes((nds) => nds.filter((n) => n.id !== selectedNodeId));
    setEdges((eds) => eds.filter((e) => e.source !== selectedNodeId && e.target !== selectedNodeId));
    setSelectedNodeId(null);
    markDirty();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedNodeId, setNodes, setEdges, currentDefinitionId]);

  // ── Clear canvas ──────────────────────────────────────────────────────────────
  function clearCanvas() {
    if (!confirm('Clear the entire canvas? This cannot be undone.')) return;
    setNodes([]);
    setEdges([]);
    setSelectedNodeId(null);
    setCurrentDefinitionId(null);
    setCurrentVersionId(null);
    setCurrentVersionNumber(null);
    setVersionStatus(null);
    setIsDirty(false);
    setWorkflowName('my-workflow');
    setWorkflowVersion('1');
    setWorkflowDesc('');
  }

  // ── Export ────────────────────────────────────────────────────────────────────
  function openExport() {
    setExportText(buildCurrentJson());
    setShowExport(true);
    setCopied(false);
  }

  function downloadJson() {
    const blob = new Blob([exportText], { type: 'application/json' });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href     = url;
    a.download = `${workflowName.replace(/\s+/g, '-')}.json`;
    a.click();
    URL.revokeObjectURL(url);
  }

  async function copyToClipboard() {
    await navigator.clipboard.writeText(exportText);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  // ── Import ────────────────────────────────────────────────────────────────────
  function openImport() {
    setImportText(STARTER_JSON);
    setImportError(null);
    setShowImport(true);
  }

  function handleImport() {
    try {
      const result = parseWorkflowDefinition(importText);
      setNodes(result.nodes);
      setEdges(result.edges);
      setWorkflowName(result.name);
      setWorkflowVersion(result.version);
      setWorkflowDesc(result.description);
      setSelectedNodeId(null);
      setCurrentDefinitionId(null);
      setCurrentVersionId(null);
      setCurrentVersionNumber(null);
      setVersionStatus(null);
      setIsDirty(false);
      setShowImport(false);
      setImportText('');
      setTimeout(() => rfInstance?.fitView({ padding: 0.15 }), 50);
    } catch (err: unknown) {
      setImportError(err instanceof Error ? err.message : 'Import failed');
    }
  }

  // ── Fit view ──────────────────────────────────────────────────────────────────
  function fitView() {
    rfInstance?.fitView({ padding: 0.15, duration: 400 });
  }

  // ── Status indicator helpers ──────────────────────────────────────────────────
  const statusLabel = currentDefinitionId
    ? isDirty
      ? '● Unsaved changes'
      : `Saved · v${currentVersionNumber}`
    : 'Not saved';

  const statusClass = !currentDefinitionId
    ? 'text-gray-400'
    : isDirty
    ? 'text-amber-600'
    : 'text-emerald-600';

  const canPublish =
    currentDefinitionId &&
    currentVersionId &&
    versionStatus === VersionStatus.Draft &&
    !isDirty;

  const canRun      = !!currentDefinitionId;
  const canSchedule = !!currentDefinitionId;

  // ── Render ────────────────────────────────────────────────────────────────────
  return (
    <div className="-m-6 flex flex-col" style={{ height: 'calc(100vh - 52px)' }}>

      {/* ── Top bar ───────────────────────────────────────────────────────────── */}
      <header className="flex items-center gap-2 bg-white border-b border-gray-200 px-4 py-2 shrink-0 flex-wrap">
        <GitBranch size={18} className="text-brand-primary shrink-0" />

        {/* Workflow name */}
        <input
          value={workflowName}
          onChange={(e) => { setWorkflowName(e.target.value); markDirty(); }}
          className="font-semibold text-sm text-gray-900 bg-transparent border-none outline-none w-44 hover:bg-gray-50 focus:bg-gray-50 rounded px-1.5 py-0.5 transition-colors"
          placeholder="workflow-name"
        />

        {/* Version */}
        <div className="flex items-center gap-1 shrink-0">
          <span className="text-xs text-gray-400">v</span>
          <input
            value={workflowVersion}
            onChange={(e) => setWorkflowVersion(e.target.value)}
            className="text-xs text-gray-600 bg-transparent border-none outline-none w-8 hover:bg-gray-50 focus:bg-gray-50 rounded px-1 transition-colors text-center"
          />
        </div>

        {/* Description */}
        <input
          value={workflowDesc}
          onChange={(e) => { setWorkflowDesc(e.target.value); markDirty(); }}
          className="text-xs text-gray-400 bg-transparent border-none outline-none flex-1 min-w-0 hover:bg-gray-50 focus:bg-gray-50 rounded px-1.5 py-0.5 transition-colors"
          placeholder="Optional description…"
        />

        {/* Step count */}
        <span className="text-xs text-gray-400 shrink-0 hidden xl:inline">
          {nodes.length} step{nodes.length !== 1 ? 's' : ''}
          {edges.length > 0 && ` · ${edges.length} link${edges.length !== 1 ? 's' : ''}`}
        </span>

        <div className="h-4 w-px bg-gray-200 shrink-0" />

        {/* ── Save status + version badge ── */}
        <div className="flex items-center gap-2 shrink-0 flex-wrap">
          <span className={`text-xs font-medium ${statusClass}`}>{statusLabel}</span>
          {versionStatus != null && !isDirty && (
            <VersionStatusBadge status={versionStatus} />
          )}
          {currentTriggerId && (
            <span
              className="inline-flex items-center gap-1 rounded-md bg-indigo-50 border border-indigo-100 px-2 py-0.5 text-xs text-indigo-700"
              title={`Scheduled trigger: ${currentTriggerName}`}
            >
              <Calendar size={10} className="text-indigo-500" />
              <span className="font-medium">{currentTriggerName}</span>
              <code className="font-mono text-indigo-400">{currentTriggerCron}</code>
            </span>
          )}
        </div>

        {publishError && (
          <span className="text-xs text-red-500 flex items-center gap-1 shrink-0">
            <AlertCircle size={12} /> {publishError}
          </span>
        )}

        <div className="h-4 w-px bg-gray-200 shrink-0" />

        {/* ── Backend action buttons ── */}
        <button onClick={() => setShowOpen(true)} className="btn btn-secondary btn-sm" title="Open existing workflow">
          <FolderOpen size={13} /> Open
        </button>

        {canEdit && (
          <>
            <button
              onClick={() => setShowSave(true)}
              className="btn btn-secondary btn-sm"
              title={currentDefinitionId ? 'Save as new version' : 'Save to backend'}
              disabled={nodes.length === 0}
            >
              <Save size={13} />
              {currentDefinitionId ? 'Save Version' : 'Save'}
            </button>

            <button
              onClick={handlePublish}
              disabled={!canPublish || publishingVersion}
              className="btn btn-secondary btn-sm"
              title={
                !currentDefinitionId ? 'Save first, then publish'
                : isDirty ? 'Save changes before publishing'
                : versionStatus === VersionStatus.Published ? 'Already published'
                : 'Publish this version'
              }
            >
              <Send size={13} />
              {publishingVersion ? 'Publishing…' : 'Publish'}
            </button>
          </>
        )}

        {canExecute && (
          <button
            onClick={handleRunClick}
            disabled={!canRun}
            className="btn btn-success btn-sm"
            title={!canRun ? 'Save the workflow first' : 'Start a process instance'}
          >
            <Play size={13} /> Run
          </button>
        )}

        {/* Validation error count badge */}
        {validationResult.errors.length > 0 && nodes.length > 0 && (
          <button
            onClick={() => { setValidationAction('run'); setShowValidationModal(true); }}
            className="flex items-center gap-1 rounded-md bg-red-100 border border-red-200 px-2 py-1 text-xs font-semibold text-red-700 hover:bg-red-200 transition-colors shrink-0"
            title="Show validation errors"
          >
            <AlertCircle size={11} />
            {validationResult.errors.length} error{validationResult.errors.length !== 1 ? 's' : ''}
          </button>
        )}

        {canEdit && (
          <button
            onClick={() => setShowSchedule(true)}
            disabled={!canSchedule}
            className="btn btn-secondary btn-sm"
            title={!canSchedule ? 'Save the workflow first' : 'Create a scheduled trigger'}
          >
            <Calendar size={13} />
            {currentTriggerId ? 'Reschedule' : 'Schedule'}
          </button>
        )}

        <div className="h-4 w-px bg-gray-200 shrink-0" />

        {/* ── Canvas utilities ── */}
        <button onClick={fitView} className="btn btn-secondary btn-sm" title="Fit view">
          <Maximize2 size={13} />
        </button>
        {canEdit && (
          <button onClick={clearCanvas} className="btn btn-secondary btn-sm">
            <RotateCcw size={13} /> Clear
          </button>
        )}
        {canEdit && (
          <button onClick={openImport} className="btn btn-secondary btn-sm">
            <Upload size={13} /> Import
          </button>
        )}
        <button onClick={openExport} className="btn btn-primary btn-sm" disabled={nodes.length === 0}>
          <Download size={13} /> Export JSON
        </button>
      </header>

      {/* ── Body ──────────────────────────────────────────────────────────────── */}
      <div className="flex flex-1 overflow-hidden">
        {canEdit && <NodeToolbox />}

        {/* Canvas — wrapped in ValidationContext so WorkflowNode/ConditionNode can read it */}
        <ValidationContext.Provider value={{ errorNodeIds, warningNodeIds }}>
        <div ref={reactFlowWrapper} className="flex-1 h-full">
          <ReactFlow
            nodes={nodes}
            edges={edges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={canEdit ? onConnect : undefined}
            onInit={setRfInstance}
            onDrop={canEdit ? onDrop : undefined}
            onDragOver={canEdit ? onDragOver : undefined}
            onSelectionChange={onSelectionChange}
            nodeTypes={nodeTypes}
            deleteKeyCode={canEdit ? 'Delete' : null}
            nodesDraggable={canEdit}
            nodesConnectable={canEdit}
            elementsSelectable
            fitView
            fitViewOptions={{ padding: 0.2 }}
            defaultEdgeOptions={{
              type: 'smoothstep',
              animated: true,
              markerEnd: { type: MarkerType.ArrowClosed },
            }}
          >
            <Background variant={BackgroundVariant.Dots} gap={20} size={1} color="#e5e7eb" />
            <Controls />
            <MiniMap
              nodeColor={(n: Node<WorkflowNodeData>) => getNodeDef(n.data?.stepType ?? '').color ?? '#6b7280'}
              pannable
              zoomable
            />

            {nodes.length === 0 && (
              <div
                style={{
                  position: 'absolute', top: '50%', left: '50%',
                  transform: 'translate(-50%, -50%)',
                  pointerEvents: 'none', textAlign: 'center', color: '#9ca3af',
                }}
              >
                <FileJson size={40} style={{ margin: '0 auto 12px', opacity: 0.4 }} />
                <p style={{ fontSize: 14, fontWeight: 600 }}>Canvas is empty</p>
                <p style={{ fontSize: 12, marginTop: 4 }}>
                  Drag a step from the left panel, or click <strong>Open</strong> / <strong>Import</strong>.
                </p>
              </div>
            )}
          </ReactFlow>
        </div>
        </ValidationContext.Provider>

        {/* Right properties panel */}
        {selectedNode && (
          <PropertiesPanel
            node={selectedNode}
            availableVariables={availableVariables}
            onUpdate={updateSelectedNode}
            onDelete={deleteSelectedNode}
            validationErrors={selectedNodeValidation.errors}
            validationWarnings={selectedNodeValidation.warnings}
          />
        )}
      </div>

      {/* ─────────────────────────────── Modals ──────────────────────────────── */}

      {showValidationModal && (
        <ValidationModal
          result={validationResult}
          action={validationAction}
          onClose={() => setShowValidationModal(false)}
        />
      )}

      {showSave && (
        <SaveModal
          workflowName={workflowName}
          workflowDesc={workflowDesc}
          workflowJson={buildCurrentJson()}
          currentDefinitionId={currentDefinitionId}
          onClose={() => setShowSave(false)}
          onSaved={handleAfterSave}
        />
      )}

      {showOpen && (
        <OpenModal
          currentDefinitionId={currentDefinitionId}
          onClose={() => setShowOpen(false)}
          onOpen={handleAfterOpen}
        />
      )}

      {showRun && currentDefinitionId && (
        <RunModal
          definitionId={currentDefinitionId}
          definitionName={workflowName}
          versionNumber={currentVersionNumber}
          versionStatus={versionStatus}
          onClose={() => setShowRun(false)}
        />
      )}

      {showSchedule && currentDefinitionId && (
        <ScheduleModal
          definitionId={currentDefinitionId}
          definitionName={workflowName}
          onClose={() => setShowSchedule(false)}
          onScheduled={handleAfterScheduled}
        />
      )}

      {/* ── Export Modal ──────────────────────────────────────────────────────── */}
      {showExport && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-xl shadow-2xl w-[640px] max-h-[80vh] flex flex-col overflow-hidden">
            <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
              <h2 className="font-semibold text-gray-900">Export Workflow JSON</h2>
              <button onClick={() => setShowExport(false)} className="text-gray-400 hover:text-gray-600 text-xl leading-none">×</button>
            </div>
            <div className="flex-1 overflow-auto p-4">
              <textarea
                readOnly
                value={exportText}
                rows={24}
                className="w-full input font-mono text-xs leading-relaxed bg-gray-50 resize-none"
              />
            </div>
            <div className="flex gap-2 justify-end px-5 py-3 border-t border-gray-200 bg-gray-50">
              <button onClick={() => setShowExport(false)} className="btn btn-secondary">Close</button>
              <button onClick={copyToClipboard} className="btn btn-secondary">
                {copied ? <><Check size={13} /> Copied!</> : <><Copy size={13} /> Copy</>}
              </button>
              <button onClick={downloadJson} className="btn btn-primary">
                <Download size={13} /> Download .json
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Import Modal ──────────────────────────────────────────────────────── */}
      {showImport && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-xl shadow-2xl w-[640px] max-h-[80vh] flex flex-col overflow-hidden">
            <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
              <h2 className="font-semibold text-gray-900">Import Workflow JSON</h2>
              <button onClick={() => setShowImport(false)} className="text-gray-400 hover:text-gray-600 text-xl leading-none">×</button>
            </div>
            <div className="flex-1 overflow-auto p-4 space-y-3">
              <p className="text-sm text-gray-500">
                Paste a workflow JSON definition. Supports both{' '}
                <code className="bg-gray-100 px-1 rounded text-xs">onSuccess</code> and{' '}
                <code className="bg-gray-100 px-1 rounded text-xs">dependsOn</code> step linkage.
              </p>
              {importError && (
                <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-3 py-2">
                  {importError}
                </div>
              )}
              <textarea
                value={importText}
                onChange={(e) => { setImportText(e.target.value); setImportError(null); }}
                rows={22}
                spellCheck={false}
                className="w-full input font-mono text-xs leading-relaxed resize-none"
                placeholder="Paste workflow JSON here…"
              />
            </div>
            <div className="flex gap-2 justify-end px-5 py-3 border-t border-gray-200 bg-gray-50">
              <button onClick={() => setShowImport(false)} className="btn btn-secondary">Cancel</button>
              <button onClick={handleImport} className="btn btn-primary" disabled={!importText.trim()}>
                <Upload size={13} /> Import &amp; Render
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
