import { useEffect, useMemo, useRef, useState } from 'react';
import { Node } from 'reactflow';
import { Trash2, ChevronDown, ChevronRight, Zap, Code2, LayoutList, RefreshCw } from 'lucide-react';
import type { RetryPolicy } from './types';
import { getNodeDef } from './utils';
import type { AvailableVariable, WorkflowNodeData } from './types';
import type { ValidationError, ValidationWarning } from './validation';
import FolderReadEditor from './editors/FolderReadEditor';
import FolderWriteEditor from './editors/FolderWriteEditor';
import FolderListFilesEditor from './editors/FolderListFilesEditor';
import CsvReadEditor from './editors/CsvReadEditor';
import CsvWriteEditor from './editors/CsvWriteEditor';
import JsonReadFileEditor from './editors/JsonReadFileEditor';
import JsonWriteFileEditor from './editors/JsonWriteFileEditor';
import ZipExtractEditor from './editors/ZipExtractEditor';
import ZipCreateEditor from './editors/ZipCreateEditor';
import PdfReadTextEditor from './editors/PdfReadTextEditor';
import PdfExtractTableEditor from './editors/PdfExtractTableEditor';
import WordFillTemplateEditor from './editors/WordFillTemplateEditor';
import MailReadEditor from './editors/MailReadEditor';
import MailGetBodyEditor from './editors/MailGetBodyEditor';
import MailExtractValueEditor from './editors/MailExtractValueEditor';
import MailExtractTableEditor from './editors/MailExtractTableEditor';
import ExcelReadRangeEditor from './editors/ExcelReadRangeEditor';
import ExcelWriteDataTableEditor from './editors/ExcelWriteDataTableEditor';
import ExcelAppendDataTableEditor from './editors/ExcelAppendDataTableEditor';
import ExcelTransformEditor from './editors/ExcelTransformEditor';
import ExcelMergeEditor from './editors/ExcelMergeEditor';
import ExcelSplitEditor from './editors/ExcelSplitEditor';
import MailSendEditor from './editors/MailSendEditor';
import MailReplyEditor from './editors/MailReplyEditor';
import MailForwardEditor from './editors/MailForwardEditor';
import MailMoveEditor from './editors/MailMoveEditor';
import MailMarkEditor from './editors/MailMarkEditor';
import MailDeleteEditor from './editors/MailDeleteEditor';
import ConditionEditor from './editors/ConditionEditor';
import SetVariableEditor from './editors/SetVariableEditor';
import ForEachEditor from './editors/ForEachEditor';
import ForEachRowEditor from './editors/ForEachRowEditor';
import ForEachFileEditor from './editors/ForEachFileEditor';
import DataTableAggregateEditor from './editors/DataTableAggregateEditor';
import DataTableJoinEditor from './editors/DataTableJoinEditor';
import HttpRequestEditor from './editors/HttpRequestEditor';
import JsonParseEditor from './editors/JsonParseEditor';
import SqlQueryEditor from './editors/SqlQueryEditor';
import SqlExecuteEditor from './editors/SqlExecuteEditor';
import SqlStoredProcedureEditor from './editors/SqlStoredProcedureEditor';
import {
  BrowserOpenEditor,
  BrowserNavigateEditor,
  BrowserClickEditor,
  BrowserTypeEditor,
  BrowserWaitEditor,
  BrowserGetTextEditor,
  BrowserGetAttributeEditor,
  BrowserScreenshotEditor,
  BrowserDownloadEditor,
  BrowserCloseEditor,
  BrowserPressKeyEditor,
  BrowserClearEditor,
  BrowserScrollEditor,
  BrowserSelectEditor,
  BrowserHoverEditor,
  BrowserWaitUrlEditor,
  BrowserWaitTextEditor,
  BrowserWaitDownloadEditor,
  BrowserWaitNetworkIdleEditor,
  BrowserElementExistsEditor,
  BrowserUploadFileEditor,
  BrowserSwitchTabEditor,
  BrowserCloseTabEditor,
  BrowserHandleAlertEditor,
  BrowserWaitPopupEditor,
  BrowserClickAndHandleAlertEditor,
  BrowserExtractTableEditor,
  BrowserWaitForTextEditor,
  BrowserWaitForUrlEditor,
  BrowserSelectOptionEditor,
  BrowserEvaluateEditor,
  BrowserWaitForDownloadEditor,
} from './editors/BrowserEditors';
import EditableNumberInput from './editors/EditableNumberInput';
import type { EditorProps } from './editors/types';

// ── Step types that have a structured form editor ──────────────────────────────
const FORM_EDITORS: Record<string, React.ComponentType<EditorProps>> = {
  'folder.read-file':      FolderReadEditor,
  'folder.write-file':     FolderWriteEditor,
  'folder.list-files':     FolderListFilesEditor,
  'csv.read':              CsvReadEditor,
  'csv.write':             CsvWriteEditor,
  'json.read-file':        JsonReadFileEditor,
  'json.write-file':       JsonWriteFileEditor,
  'zip.extract':           ZipExtractEditor,
  'zip.create':            ZipCreateEditor,
  'pdf.read-text':         PdfReadTextEditor,
  'pdf.extract-table':     PdfExtractTableEditor,
  'word.fill-template':    WordFillTemplateEditor,
  'mail.read-attachments': MailReadEditor,
  'mail.get-body':         MailGetBodyEditor,
  'mail.extract-value':    MailExtractValueEditor,
  'mail.extract-table':    MailExtractTableEditor,
  'mail.send':             MailSendEditor,
  'mail.reply':            MailReplyEditor,
  'mail.forward':          MailForwardEditor,
  'mail.move':             MailMoveEditor,
  'mail.mark':             MailMarkEditor,
  'mail.delete':           MailDeleteEditor,
  'excel.read-range':      ExcelReadRangeEditor,
  'excel.write-datatable': ExcelWriteDataTableEditor,
  'excel.append-datatable': ExcelAppendDataTableEditor,
  'excel.transform':       ExcelTransformEditor,
  'excel.merge':           ExcelMergeEditor,
  'excel.split':           ExcelSplitEditor,
  'condition.if':          ConditionEditor,
  'set.variable':          SetVariableEditor,
  'foreach.loop':          ForEachEditor,
  'foreach.row':           ForEachRowEditor,
  'foreach.file':          ForEachFileEditor,
  'datatable.aggregate':   DataTableAggregateEditor,
  'datatable.join':        DataTableJoinEditor,
  'http.request':          HttpRequestEditor,
  'json.parse':            JsonParseEditor,
  'sql.query':             SqlQueryEditor,
  'sql.execute':           SqlExecuteEditor,
  'sql.stored-procedure':  SqlStoredProcedureEditor,
  'browser.open':            BrowserOpenEditor,
  'browser.navigate':        BrowserNavigateEditor,
  'browser.click':           BrowserClickEditor,
  'browser.type':            BrowserTypeEditor,
  'browser.wait-for-selector': BrowserWaitEditor,
  'browser.get-text':        BrowserGetTextEditor,
  'browser.get-attribute':   BrowserGetAttributeEditor,
  'browser.screenshot':      BrowserScreenshotEditor,
  'browser.download':        BrowserDownloadEditor,
  'browser.close':           BrowserCloseEditor,
  'browser.press-key':       BrowserPressKeyEditor,
  'browser.clear':           BrowserClearEditor,
  'browser.scroll':          BrowserScrollEditor,
  'browser.select':          BrowserSelectEditor,
  'browser.hover':           BrowserHoverEditor,
  'browser.wait-url':        BrowserWaitUrlEditor,
  'browser.wait-text':       BrowserWaitTextEditor,
  'browser.wait-download':   BrowserWaitDownloadEditor,
  'browser.wait-network-idle': BrowserWaitNetworkIdleEditor,
  'browser.element-exists':  BrowserElementExistsEditor,
  'browser.upload-file':     BrowserUploadFileEditor,
  'browser.switch-tab':      BrowserSwitchTabEditor,
  'browser.close-tab':       BrowserCloseTabEditor,
  'browser.handle-alert':    BrowserHandleAlertEditor,
  'browser.wait-popup':      BrowserWaitPopupEditor,
  'browser.click-and-handle-alert': BrowserClickAndHandleAlertEditor,
  'browser.extract-table':       BrowserExtractTableEditor,
  'browser.wait-for-text':   BrowserWaitForTextEditor,
  'browser.wait-for-url':    BrowserWaitForUrlEditor,
  'browser.select-option':   BrowserSelectOptionEditor,
  'browser.evaluate':        BrowserEvaluateEditor,
  'browser.wait-for-download': BrowserWaitForDownloadEditor,
};

// ── Deep-set helper for nested config paths (e.g. "condition.left") ───────────
function deepSet(
  obj: Record<string, unknown>,
  dotPath: string,
  value: unknown,
): Record<string, unknown> {
  const dot = dotPath.indexOf('.');
  if (dot === -1) return { ...obj, [dotPath]: value };
  const head = dotPath.slice(0, dot);
  const tail  = dotPath.slice(dot + 1);
  return {
    ...obj,
    [head]: deepSet((obj[head] as Record<string, unknown>) ?? {}, tail, value),
  };
}

// ── Props ─────────────────────────────────────────────────────────────────────

interface PropertiesPanelProps {
  node: Node<WorkflowNodeData>;
  availableVariables: AvailableVariable[];
  onUpdate: (data: WorkflowNodeData) => void;
  onDelete: () => void;
  validationErrors?:   ValidationError[];
  validationWarnings?: ValidationWarning[];
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function PropertiesPanel({
  node,
  availableVariables,
  onUpdate,
  onDelete,
  validationErrors   = [],
  validationWarnings = [],
}: PropertiesPanelProps) {
  const def          = getNodeDef(node.data.stepType);
  const FormEditor   = FORM_EDITORS[node.data.stepType] ?? null;
  const hasFormEditor = FormEditor !== null;

  // ── Field-level error/warning maps for editors ────────────────────────────
  const fieldErrors = useMemo(() => {
    const m: Record<string, string> = {};
    for (const e of validationErrors) if (e.field) m[e.field] = e.message;
    return m;
  }, [validationErrors]);

  const fieldWarnings = useMemo(() => {
    const m: Record<string, string> = {};
    for (const w of validationWarnings) if (w.field) m[w.field] = w.message;
    return m;
  }, [validationWarnings]);

  // Non-field errors (graph-level or general)
  const generalErrors   = validationErrors.filter(e => !e.field);
  const generalWarnings = validationWarnings.filter(w => !w.field);

  // ── Local state ──────────────────────────────────────────────────────────────
  const [name, setName]             = useState(node.data.name);
  const [configText, setConfigText] = useState(JSON.stringify(node.data.config, null, 2));
  const [configError, setConfigError] = useState<string | null>(null);
  // Advanced JSON mode: default false when a form editor exists, true otherwise
  const [advancedMode, setAdvancedMode] = useState(!hasFormEditor);
  const [varsOpen, setVarsOpen]         = useState(true);
  const [retryOpen, setRetryOpen]       = useState(false);
  const [retryEnabled, setRetryEnabled] = useState(!!node.data.retry);
  const [retryMax, setRetryMax]         = useState(node.data.retry?.maxAttempts ?? 3);
  const [retryDelay, setRetryDelay]     = useState(node.data.retry?.delaySeconds ?? 10);

  // Reset local state whenever the selected node changes
  useEffect(() => {
    setName(node.data.name);
    setConfigText(JSON.stringify(node.data.config, null, 2));
    setConfigError(null);
    setAdvancedMode(!FORM_EDITORS[node.data.stepType]);
    setVarsOpen(true);
    setRetryOpen(false);
    setRetryEnabled(!!node.data.retry);
    setRetryMax(node.data.retry?.maxAttempts ?? 3);
    setRetryDelay(node.data.retry?.delaySeconds ?? 10);
    focusedRef.current = null;
    lastFocusedRef.current = null;
  }, [node.id]); // eslint-disable-line react-hooks/exhaustive-deps

  // Keep JSON textarea aligned with node config while in form mode
  useEffect(() => {
    if (!advancedMode) {
      setConfigText(JSON.stringify(node.data.config, null, 2));
      setConfigError(null);
    }
  }, [node.data.config, advancedMode]);

  // ── Focused-input tracking for variable insertion ─────────────────────────────
  /**
   * Tracks the currently focused text/number input together with its config key.
   * key = null  → the JSON textarea
   * key = "xxx" → a specific field inside the form editor
   */
  const focusedRef = useRef<{
    el: HTMLInputElement | HTMLTextAreaElement;
    key: string | null;
  } | null>(null);

  /** Last focused field — survives blur when clicking variable chips. */
  const lastFocusedRef = useRef<{
    el: HTMLInputElement | HTMLTextAreaElement;
    key: string | null;
  } | null>(null);

  const nodeRef = useRef(node);
  nodeRef.current = node;

  const configTextareaRef = useRef<HTMLTextAreaElement>(null);

  function onFocusTextarea(e: React.SyntheticEvent<HTMLTextAreaElement>) {
    const target = { el: e.currentTarget, key: null as string | null };
    focusedRef.current = target;
    lastFocusedRef.current = target;
  }

  /** Called by editor child components when one of their inputs gains focus. */
  function onFocusField(el: HTMLInputElement | HTMLTextAreaElement, key: string) {
    const target = { el, key };
    focusedRef.current = target;
    lastFocusedRef.current = target;
  }

  // ── Config update helpers ─────────────────────────────────────────────────────

  /** Single source of truth: update both React Flow state + JSON textarea. */
  function applyConfig(newConfig: Record<string, unknown>) {
    onUpdate({ ...nodeRef.current.data, config: newConfig });
    setConfigText(JSON.stringify(newConfig, null, 2));
    setConfigError(null);
  }

  function applyRetry(enabled: boolean, max: number, delay: number) {
    const retryPolicy: RetryPolicy | undefined = enabled ? { maxAttempts: max, delaySeconds: delay } : undefined;
    onUpdate({ ...node.data, retry: retryPolicy });
  }

  // Called by form editors
  function handleFormChange(newConfig: Record<string, unknown>) {
    applyConfig(newConfig);
  }

  // Called by the JSON textarea
  function handleJsonChange(e: React.ChangeEvent<HTMLTextAreaElement>) {
    const text = e.target.value;
    setConfigText(text);
    try {
      const parsed = JSON.parse(text) as Record<string, unknown>;
      onUpdate({ ...nodeRef.current.data, config: parsed });
      setConfigError(null);
    } catch {
      setConfigError('Invalid JSON');
    }
  }

  function handleJsonBlur() {
    if (!configError) {
      try {
        setConfigText(JSON.stringify(JSON.parse(configText), null, 2));
      } catch { /* leave as-is */ }
    }
  }

  // When switching from Advanced JSON → Form: node config is source of truth
  function switchToForm() {
    setConfigText(JSON.stringify(nodeRef.current.data.config, null, 2));
    setConfigError(null);
    focusedRef.current = null;
    setAdvancedMode(false);
  }

  // When switching from Form → Advanced JSON: stringify current config
  function switchToJson() {
    setConfigText(JSON.stringify(nodeRef.current.data.config, null, 2));
    setConfigError(null);
    setAdvancedMode(true);
  }

  // ── Name change ───────────────────────────────────────────────────────────────
  function handleNameChange(e: React.ChangeEvent<HTMLInputElement>) {
    setName(e.target.value);
    onUpdate({ ...node.data, name: e.target.value });
  }

  // ── Variable insertion ────────────────────────────────────────────────────────
  function insertVariable(insertValue: string) {
    let focused = focusedRef.current ?? lastFocusedRef.current;

    // Form mode: never append raw text to JSON — use last focused field or bail
    if (!focused && !advancedMode) return;

    if (!focused && advancedMode) {
      const newText = configText + insertValue;
      setConfigText(newText);
      try {
        const parsed = JSON.parse(newText) as Record<string, unknown>;
        onUpdate({ ...nodeRef.current.data, config: parsed });
        setConfigError(null);
      } catch {
        setConfigError('Invalid JSON');
      }
      return;
    }

    if (!focused) return;

    const { el, key } = focused;
    const start = el.selectionStart ?? el.value.length;
    const end   = el.selectionEnd   ?? el.value.length;
    const newValue = el.value.slice(0, start) + insertValue + el.value.slice(end);
    const cursorPos = start + insertValue.length;

    if (key === null) {
      // ── JSON textarea ─────────────────────────────────────────────────────
      setConfigText(newValue);
      try {
        const parsed = JSON.parse(newValue) as Record<string, unknown>;
        onUpdate({ ...nodeRef.current.data, config: parsed });
        setConfigError(null);
      } catch {
        setConfigError('Invalid JSON — fix manually or undo');
      }
      requestAnimationFrame(() => {
        const ta = configTextareaRef.current;
        if (ta) { ta.focus(); ta.setSelectionRange(cursorPos, cursorPos); }
        const target = { el: el as HTMLTextAreaElement, key: null as string | null };
        focusedRef.current = target;
        lastFocusedRef.current = target;
      });
    } else {
      // ── Form field (supports dot-notation paths like "condition.left") ───
      const newConfig = deepSet(nodeRef.current.data.config, key, newValue);
      applyConfig(newConfig);
      requestAnimationFrame(() => {
        el.focus();
        el.setSelectionRange(cursorPos, cursorPos);
        const target = { el, key };
        focusedRef.current = target;
        lastFocusedRef.current = target;
      });
    }
  }

  // ── Variable grouping ─────────────────────────────────────────────────────────
  const grouped = availableVariables.reduce<Map<string, { name: string; vars: AvailableVariable[] }>>(
    (acc, v) => {
      if (!acc.has(v.sourceNodeId)) acc.set(v.sourceNodeId, { name: v.sourceNodeName, vars: [] });
      acc.get(v.sourceNodeId)!.vars.push(v);
      return acc;
    },
    new Map(),
  );

  // ── Render ────────────────────────────────────────────────────────────────────
  return (
    <aside className="w-72 shrink-0 bg-white border-l border-gray-200 flex flex-col overflow-hidden">

      {/* ── Header ── */}
      <div
        className="flex items-center justify-between px-4 py-3 shrink-0"
        style={{ borderBottom: `3px solid ${def.color}` }}
      >
        <div className="flex items-center gap-2">
          <span
            className="flex h-7 w-7 items-center justify-center rounded-md text-sm shrink-0"
            style={{ background: def.color }}
          >
            {def.emoji}
          </span>
          <div className="min-w-0">
            <p className="text-xs font-bold text-gray-900 truncate">{def.label}</p>
            <p className="text-xs text-gray-400 font-mono leading-tight truncate">{def.category}</p>
          </div>
        </div>
        <button
          onClick={() => { if (confirm('Delete this step?')) onDelete(); }}
          className="btn btn-danger btn-sm shrink-0"
        >
          <Trash2 size={12} /> Delete
        </button>
      </div>

      {/* ── Scrollable body ── */}
      <div className="flex-1 overflow-y-auto px-4 py-4 space-y-4">

        {/* Step ID (read-only) */}
        <div>
          <label className="label">Step ID</label>
          <input
            readOnly
            value={node.id}
            className="input bg-gray-50 text-gray-500 font-mono text-xs cursor-default"
          />
        </div>

        {/* Step Name */}
        <div>
          <label className="label">Step Name</label>
          <input
            value={name}
            onChange={handleNameChange}
            className="input"
            placeholder="Descriptive step name…"
          />
        </div>

        {/* ── Configuration area ── */}
        <div>
          {/* Section header + toggle */}
          <div className="flex items-center justify-between mb-2">
            <label className="label mb-0">Configuration</label>
            <div className="flex items-center gap-1.5">
              {configError && (
                <span className="text-xs text-red-500 font-medium">⚠ JSON invalid</span>
              )}
              {!configError && advancedMode && (
                <span className="text-xs text-green-600">✓ valid</span>
              )}
              {hasFormEditor && (
                <button
                  onClick={advancedMode ? switchToForm : switchToJson}
                  className="btn btn-secondary btn-sm gap-1"
                  title={advancedMode ? 'Switch to form editor' : 'Switch to raw JSON'}
                >
                  {advancedMode
                    ? <><LayoutList size={11} /> Form</>
                    : <><Code2 size={11} /> JSON</>
                  }
                </button>
              )}
            </div>
          </div>

          {/* ── Validation summary (non-field errors / all errors) ── */}
          {(validationErrors.length > 0 || generalWarnings.length > 0) && (
            <div className="mb-3 space-y-1.5">
              {validationErrors.length > 0 && (
                <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2">
                  {generalErrors.length > 0
                    ? generalErrors.map((e, i) => (
                        <p key={i} className="text-xs text-red-700">• {e.message}</p>
                      ))
                    : <p className="text-xs font-medium text-red-700">
                        {validationErrors.length} field error{validationErrors.length !== 1 ? 's' : ''} — fix fields below
                      </p>
                  }
                </div>
              )}
              {generalWarnings.map((w, i) => (
                <div key={i} className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-1.5">
                  <p className="text-xs text-amber-700">• {w.message}</p>
                </div>
              ))}
            </div>
          )}

          {/* ── Form editor (structured) ── */}
          {!advancedMode && FormEditor && (
            <FormEditor
              config={node.data.config}
              onChange={handleFormChange}
              onFocusField={onFocusField}
              fieldErrors={{ ...fieldErrors, ...fieldWarnings }}
            />
          )}

          {/* ── Advanced JSON textarea ── */}
          {advancedMode && (
            <>
              {availableVariables.length > 0 && (
                <p className="text-xs text-gray-400 mb-1.5">
                  Position cursor inside a value, then click a variable below.
                </p>
              )}
              <textarea
                ref={configTextareaRef}
                value={configText}
                onChange={handleJsonChange}
                onBlur={handleJsonBlur}
                onFocus={onFocusTextarea}
                onClick={onFocusTextarea}
                onKeyUp={onFocusTextarea}
                rows={18}
                spellCheck={false}
                className={`input font-mono text-xs leading-relaxed resize-none ${
                  configError ? 'border-red-400 focus:ring-red-400' : ''
                }`}
              />
            </>
          )}

          {/* Form mode: hint about JSON textarea availability */}
          {!advancedMode && hasFormEditor && (
            <p className="text-xs text-gray-400 mt-2">
              Switch to <strong>JSON</strong> mode to edit all config fields directly.
            </p>
          )}
        </div>

        {/* ── Retry Policy ── */}
        <div className="border border-gray-200 rounded-lg overflow-hidden">
          <button
            onClick={() => setRetryOpen(!retryOpen)}
            className="flex items-center justify-between w-full px-3 py-2 bg-gray-50 hover:bg-gray-100 transition-colors text-left"
          >
            <div className="flex items-center gap-1.5">
              <RefreshCw size={12} className={retryEnabled ? 'text-indigo-500' : 'text-gray-400'} />
              <span className="text-xs font-semibold text-gray-700">Retry Policy</span>
              {retryEnabled && (
                <span className="rounded-full bg-indigo-100 text-indigo-700 text-xs px-1.5 font-medium">
                  {retryMax}× / {retryDelay}s
                </span>
              )}
            </div>
            {retryOpen
              ? <ChevronDown size={13} className="text-gray-400" />
              : <ChevronRight size={13} className="text-gray-400" />}
          </button>

          {retryOpen && (
            <div className="px-3 py-3 space-y-3">
              {/* Enable toggle */}
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={retryEnabled}
                  onChange={(e) => {
                    setRetryEnabled(e.target.checked);
                    applyRetry(e.target.checked, retryMax, retryDelay);
                  }}
                  className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                />
                <span className="text-xs font-medium text-gray-700">Enable retry on failure</span>
              </label>

              {retryEnabled && (
                <>
                  {/* Max Attempts */}
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">
                      Max Attempts <span className="text-gray-400 font-normal">(total, including first)</span>
                    </label>
                    <EditableNumberInput
                      min={1}
                      max={10}
                      fallback={3}
                      value={retryMax}
                      onValueChange={(v) => {
                        if (v === '') return;
                        setRetryMax(v);
                        applyRetry(true, v, retryDelay);
                      }}
                      className={`w-full text-xs border rounded px-2 py-1.5 focus:outline-none focus:ring-1 ${
                        retryMax < 1
                          ? 'border-red-400 focus:ring-red-400'
                          : 'border-gray-300 focus:ring-indigo-400'
                      }`}
                    />
                    {retryMax < 1 && (
                      <p className="text-xs text-red-500 mt-0.5">Must be at least 1</p>
                    )}
                  </div>

                  {/* Delay Seconds */}
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">
                      Delay Between Attempts <span className="text-gray-400 font-normal">(seconds)</span>
                    </label>
                    <EditableNumberInput
                      min={0}
                      max={3600}
                      fallback={10}
                      value={retryDelay}
                      onValueChange={(v) => {
                        if (v === '') return;
                        setRetryDelay(v);
                        applyRetry(true, retryMax, v);
                      }}
                      className={`w-full text-xs border rounded px-2 py-1.5 focus:outline-none focus:ring-1 ${
                        retryDelay < 0
                          ? 'border-red-400 focus:ring-red-400'
                          : 'border-gray-300 focus:ring-indigo-400'
                      }`}
                    />
                    {retryDelay < 0 && (
                      <p className="text-xs text-red-500 mt-0.5">Must be 0 or greater</p>
                    )}
                  </div>

                  <p className="text-xs text-gray-400">
                    Will retry up to <strong className="text-gray-600">{retryMax - 1}</strong> time(s),
                    waiting <strong className="text-gray-600">{retryDelay}s</strong> between each attempt.
                    If all {retryMax} attempt(s) fail, the step is marked failed.
                  </p>
                </>
              )}

              {!retryEnabled && (
                <p className="text-xs text-gray-400">
                  No retries — any failure immediately marks this step failed.
                </p>
              )}
            </div>
          )}
        </div>

        {/* ── Variable Picker ── */}
        {availableVariables.length > 0 ? (
          <div className="border border-gray-200 rounded-lg overflow-hidden">
            <button
              onClick={() => setVarsOpen(!varsOpen)}
              className="flex items-center justify-between w-full px-3 py-2 bg-gray-50 hover:bg-gray-100 transition-colors text-left"
            >
              <div className="flex items-center gap-1.5">
                <Zap size={12} className="text-blue-500" />
                <span className="text-xs font-semibold text-gray-700">Available Variables</span>
                <span className="rounded-full bg-blue-100 text-blue-600 text-xs px-1.5 font-medium">
                  {availableVariables.length}
                </span>
              </div>
              {varsOpen
                ? <ChevronDown size={13} className="text-gray-400" />
                : <ChevronRight size={13} className="text-gray-400" />}
            </button>

            {varsOpen && (
              <div className="px-3 py-2 space-y-3">
                <p className="text-xs text-gray-400">
                  {advancedMode
                    ? 'Click a chip to insert at cursor in the JSON above.'
                    : 'Focus a text field above, then click a chip to insert.'}
                </p>
                {[...grouped.entries()].map(([srcId, { name: srcName, vars }]) => {
                  const dotColor = vars[0]?.kind === 'variable' ? '#3b82f6' : '#6b7280';
                  return (
                    <div key={srcId}>
                      <p className="text-xs text-gray-500 font-medium mb-1.5 flex items-center gap-1">
                        <span
                          className="w-1.5 h-1.5 rounded-full inline-block shrink-0"
                          style={{ background: dotColor }}
                        />
                        {srcName}
                      </p>
                      <div className="flex flex-wrap gap-1.5">
                        {vars.map((v) => (
                          <VariableChip
                            key={v.insertValue}
                            variable={v}
                            onClick={() => insertVariable(v.insertValue)}
                          />
                        ))}
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        ) : (
          <div className="border border-dashed border-gray-200 rounded-lg px-3 py-4 text-center">
            <Zap size={16} className="text-gray-300 mx-auto mb-1.5" />
            <p className="text-xs text-gray-400 font-medium">No upstream variables</p>
            <p className="text-xs text-gray-400 mt-0.5">
              Connect a source step to see available variables.
            </p>
          </div>
        )}
      </div>
    </aside>
  );
}

// ── Variable chip ──────────────────────────────────────────────────────────────

interface ChipProps { variable: AvailableVariable; onClick: () => void; }

function VariableChip({ variable: v, onClick }: ChipProps) {
  const isVar = v.kind === 'variable';
  return (
    <button
      type="button"
      onMouseDown={(e) => e.preventDefault()}
      onClick={onClick}
      title={v.description}
      className={`inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded text-xs font-mono font-medium transition-colors leading-tight ${
        isVar
          ? 'bg-blue-50 text-blue-700 ring-1 ring-blue-200 hover:bg-blue-100 hover:ring-blue-300'
          : 'bg-slate-100 text-slate-600 ring-1 ring-slate-200 hover:bg-slate-200 hover:ring-slate-300'
      }`}
    >
      {isVar  && <span className="opacity-50 text-xs">{'{{'}</span>}
      {!isVar && <span className="opacity-50 mr-0.5 text-xs">📄</span>}
      <span>{v.label}</span>
      {isVar  && <span className="opacity-50 text-xs">{'}}'}</span>}
    </button>
  );
}
