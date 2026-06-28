import type { EditorProps } from './types';
import EditableNumberInput from './EditableNumberInput';
import { ArtifactNameField, FieldMsg, OutputVariableField, SelectorField, SessionNameField } from './browserCommon';

export function BrowserOpenEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const headless = config.headless !== false && config.headless !== 'false';

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <label className="flex items-center gap-2 cursor-pointer select-none">
        <input
          type="checkbox"
          checked={headless}
          onChange={(e) => onChange({ ...config, headless: e.target.checked })}
          className="h-4 w-4 rounded border-gray-300 text-teal-600 focus:ring-teal-500"
        />
        <span className="text-sm font-medium text-gray-700">Headless</span>
      </label>
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Viewport Width</label>
          <EditableNumberInput
            min={320}
            fallback={1366}
            value={config.viewportWidth}
            onValueChange={(viewportWidth) => onChange({ ...config, viewportWidth })}
            className="input w-full"
          />
        </div>
        <div>
          <label className="label">Viewport Height</label>
          <EditableNumberInput
            min={240}
            fallback={768}
            value={config.viewportHeight}
            onValueChange={(viewportHeight) => onChange({ ...config, viewportHeight })}
            className="input w-full"
          />
        </div>
      </div>
      <FieldMsg errors={fieldErrors} field="viewportWidth" />
    </div>
  );
}

export function BrowserNavigateEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const url = String(config.url ?? '');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <div>
        <label className="label">URL *</label>
        <input
          value={url}
          onChange={(e) => onChange({ ...config, url: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'url')}
          className={`input font-mono text-xs ${fieldErrors?.url ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="https://www.google.com"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="url" />
      </div>
    </div>
  );
}

export function BrowserClickEditor(props: EditorProps) {
  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField {...props} />
    </div>
  );
}

export function BrowserTypeEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const text = String(config.text ?? '');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField {...props} />
      <div>
        <label className="label">Text *</label>
        <input
          value={text}
          onChange={(e) => onChange({ ...config, text: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'text')}
          className={`input ${fieldErrors?.text ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="Text to type"
        />
        <FieldMsg errors={fieldErrors} field="text" />
      </div>
    </div>
  );
}

export function BrowserWaitEditor(props: EditorProps) {
  const { config, onChange } = props;

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField {...props} />
      <div>
        <label className="label">Timeout (ms)</label>
        <EditableNumberInput
          min={0}
          fallback={30000}
          value={config.timeoutMs}
          onValueChange={(timeoutMs) => onChange({ ...config, timeoutMs })}
          className="input w-32"
        />
        <p className="text-xs text-gray-400 mt-1">Maximum time to wait for the element to appear.</p>
      </div>
    </div>
  );
}

export function BrowserGetTextEditor(props: EditorProps) {
  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField {...props} />
      <OutputVariableField {...props} />
    </div>
  );
}

export function BrowserGetAttributeEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const attribute = String(config.attribute ?? 'href');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField {...props} />
      <div>
        <label className="label">Attribute *</label>
        <input
          value={attribute}
          onChange={(e) => onChange({ ...config, attribute: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'attribute')}
          className={`input font-mono text-xs ${fieldErrors?.attribute ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="href"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="attribute" />
      </div>
      <OutputVariableField {...props} />
    </div>
  );
}

export function BrowserScreenshotEditor(props: EditorProps) {
  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <ArtifactNameField {...props} />
    </div>
  );
}

export function BrowserDownloadEditor(props: EditorProps) {
  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField
        {...props}
        field="clickSelector"
        label="Click Selector *"
        hint="CSS selector for the element that triggers the file download."
      />
      <ArtifactNameField {...props} />
    </div>
  );
}

export function BrowserCloseEditor(props: EditorProps) {
  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <p className="text-xs text-gray-400">Closes the browser session opened by Browser Open.</p>
    </div>
  );
}

const KEY_OPTIONS = [
  'Enter',
  'Tab',
  'Escape',
  'ArrowUp',
  'ArrowDown',
  'ArrowLeft',
  'ArrowRight',
  'PageUp',
  'PageDown',
  'Home',
  'End',
  'Delete',
  'Backspace',
  'Space',
] as const;

export function BrowserPressKeyEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const key = String(config.key ?? 'Enter');
  const isPreset = KEY_OPTIONS.includes(key as typeof KEY_OPTIONS[number]);

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField
        {...props}
        label="Selector (optional)"
        hint="When set, focuses the element before pressing the key. Leave empty for page-level key press."
      />
      <div>
        <label className="label">Key *</label>
        <select
          value={isPreset ? key : '__custom__'}
          onChange={(e) => {
            if (e.target.value !== '__custom__') onChange({ ...config, key: e.target.value });
          }}
          className="input text-xs mb-2"
        >
          {KEY_OPTIONS.map((k) => (
            <option key={k} value={k}>{k}</option>
          ))}
          <option value="__custom__">Custom…</option>
        </select>
        {!isPreset && (
          <input
            value={key}
            onChange={(e) => onChange({ ...config, key: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'key')}
            className={`input font-mono text-xs ${fieldErrors?.key ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="Enter"
            spellCheck={false}
          />
        )}
        <FieldMsg errors={fieldErrors} field="key" />
      </div>
    </div>
  );
}

export function BrowserClearEditor(props: EditorProps) {
  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField {...props} hint="CSS selector for the input or textarea to clear." />
    </div>
  );
}

export function BrowserWaitForTextEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const text = String(config.text ?? '');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <div>
        <label className="label">Text *</label>
        <input
          value={text}
          onChange={(e) => onChange({ ...config, text: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'text')}
          className={`input ${fieldErrors?.text ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="Discover"
        />
        <FieldMsg errors={fieldErrors} field="text" />
        <p className="text-xs text-gray-400 mt-1">Waits until this text appears anywhere on the page.</p>
      </div>
      <div>
        <label className="label">Timeout (ms)</label>
        <EditableNumberInput
          min={0}
          fallback={30000}
          value={config.timeoutMs}
          onValueChange={(timeoutMs) => onChange({ ...config, timeoutMs })}
          className="input w-32"
        />
      </div>
    </div>
  );
}

export function BrowserWaitForUrlEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const urlContains = String(config.urlContains ?? '');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <div>
        <label className="label">URL Contains *</label>
        <input
          value={urlContains}
          onChange={(e) => onChange({ ...config, urlContains: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'urlContains')}
          className={`input font-mono text-xs ${fieldErrors?.urlContains ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="/discover"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="urlContains" />
        <p className="text-xs text-gray-400 mt-1">Waits until the current URL contains this text.</p>
      </div>
      <div>
        <label className="label">Timeout (ms)</label>
        <EditableNumberInput
          min={0}
          fallback={30000}
          value={config.timeoutMs}
          onValueChange={(timeoutMs) => onChange({ ...config, timeoutMs })}
          className="input w-32"
        />
      </div>
    </div>
  );
}

export function BrowserSelectOptionEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const value = String(config.value ?? '');
  const label = String(config.label ?? '');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField {...props} hint="CSS selector for the &lt;select&gt; element." />
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Value</label>
          <input
            value={value}
            onChange={(e) => onChange({ ...config, value: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'value')}
            className={`input font-mono text-xs ${fieldErrors?.value ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="option-value"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="value" />
        </div>
        <div>
          <label className="label">Label</label>
          <input
            value={label}
            onChange={(e) => onChange({ ...config, label: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'label')}
            className={`input text-xs ${fieldErrors?.label ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="Visible label"
          />
          <FieldMsg errors={fieldErrors} field="label" />
        </div>
      </div>
      <p className="text-xs text-gray-400">Provide either Value or Label (or both — Label takes precedence).</p>
    </div>
  );
}

export function BrowserEvaluateEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const script = String(config.script ?? '');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <div>
        <label className="label">Script *</label>
        <textarea
          value={script}
          onChange={(e) => onChange({ ...config, script: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'script')}
          className={`input font-mono text-xs min-h-[88px] ${fieldErrors?.script ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="document.title"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="script" />
        <p className="text-xs text-amber-700 mt-1">Trusted internal workflows only — runs JavaScript in the page context.</p>
      </div>
      <OutputVariableField {...props} />
    </div>
  );
}

export function BrowserWaitForDownloadEditor(props: EditorProps) {
  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField
        {...props}
        field="clickSelector"
        label="Click Selector *"
        hint="CSS selector for the element that triggers the file download."
      />
      <ArtifactNameField {...props} />
      <p className="text-xs text-gray-400">Clicks the element and waits for the browser download to complete.</p>
    </div>
  );
}

const SCROLL_DIRECTIONS = ['up', 'down', 'left', 'right'] as const;

export function BrowserScrollEditor(props: EditorProps) {
  const { config, onChange, fieldErrors } = props;
  const direction = String(config.direction ?? 'down');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField
        {...props}
        label="Selector (optional)"
        hint="Leave empty to scroll the page. Provide a selector to scroll within an element."
      />
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Direction</label>
          <select
            value={direction}
            onChange={(e) => onChange({ ...config, direction: e.target.value })}
            className={`input text-xs ${fieldErrors?.direction ? 'border-red-400 focus:ring-red-400' : ''}`}
          >
            {SCROLL_DIRECTIONS.map((d) => (
              <option key={d} value={d}>{d}</option>
            ))}
          </select>
          <FieldMsg errors={fieldErrors} field="direction" />
        </div>
        <div>
          <label className="label">Amount (px)</label>
          <EditableNumberInput
            min={1}
            fallback={1000}
            value={config.amount}
            onValueChange={(amount) => onChange({ ...config, amount })}
            className="input w-full"
          />
          <FieldMsg errors={fieldErrors} field="amount" />
        </div>
      </div>
    </div>
  );
}

export function BrowserSelectEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const mode = String(config.mode ?? 'value');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField {...props} hint="CSS selector for the &lt;select&gt; element." />
      <div>
        <label className="label">Mode</label>
        <select
          value={mode}
          onChange={(e) => onChange({ ...config, mode: e.target.value })}
          className="input text-xs"
        >
          <option value="value">Value</option>
          <option value="label">Label</option>
          <option value="index">Index</option>
        </select>
      </div>
      {mode === 'value' && (
        <div>
          <label className="label">Value *</label>
          <input
            value={String(config.value ?? '')}
            onChange={(e) => onChange({ ...config, value: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'value')}
            className={`input font-mono text-xs ${fieldErrors?.value ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="TR"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="value" />
        </div>
      )}
      {mode === 'label' && (
        <div>
          <label className="label">Label *</label>
          <input
            value={String(config.label ?? '')}
            onChange={(e) => onChange({ ...config, label: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'label')}
            className={`input text-xs ${fieldErrors?.label ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="Turkey"
          />
          <FieldMsg errors={fieldErrors} field="label" />
        </div>
      )}
      {mode === 'index' && (
        <div>
          <label className="label">Index *</label>
          <EditableNumberInput
            min={0}
            fallback={0}
            value={config.index}
            onValueChange={(index) => onChange({ ...config, index })}
            className="input w-32"
          />
          <FieldMsg errors={fieldErrors} field="index" />
        </div>
      )}
    </div>
  );
}

export function BrowserHoverEditor(props: EditorProps) {
  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField {...props} hint="CSS selector for the element to hover over (e.g. dropdown menu trigger)." />
    </div>
  );
}

const URL_MATCH_MODES = ['contains', 'equals', 'startsWith', 'regex'] as const;

export function BrowserWaitUrlEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const urlContains = String(config.urlContains ?? config.pattern ?? '');
  const matchMode = String(config.matchMode ?? 'contains');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <div>
        <label className="label">URL Contains / Pattern *</label>
        <input
          value={urlContains}
          onChange={(e) => onChange({ ...config, urlContains: e.target.value, pattern: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'urlContains')}
          className={`input font-mono text-xs ${fieldErrors?.urlContains ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="/discover"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="urlContains" />
      </div>
      <div>
        <label className="label">Match Mode</label>
        <select
          value={matchMode}
          onChange={(e) => onChange({ ...config, matchMode: e.target.value })}
          className="input text-xs"
        >
          {URL_MATCH_MODES.map((mode) => (
            <option key={mode} value={mode}>{mode}</option>
          ))}
        </select>
      </div>
      <div>
        <label className="label">Timeout (ms)</label>
        <EditableNumberInput
          min={1}
          fallback={30000}
          value={config.timeoutMs}
          onValueChange={(timeoutMs) => onChange({ ...config, timeoutMs })}
          className="input w-32"
        />
        <FieldMsg errors={fieldErrors} field="timeoutMs" />
      </div>
    </div>
  );
}

export function BrowserWaitTextEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const text = String(config.text ?? '');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <div>
        <label className="label">Text *</label>
        <input
          value={text}
          onChange={(e) => onChange({ ...config, text: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'text')}
          className={`input ${fieldErrors?.text ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="Export completed"
        />
        <FieldMsg errors={fieldErrors} field="text" />
      </div>
      <SelectorField
        {...props}
        label="Selector (optional)"
        hint="When set, waits for text inside this element only (e.g. .toast)."
      />
      <div>
        <label className="label">Timeout (ms)</label>
        <EditableNumberInput
          min={1}
          fallback={30000}
          value={config.timeoutMs}
          onValueChange={(timeoutMs) => onChange({ ...config, timeoutMs })}
          className="input w-32"
        />
        <FieldMsg errors={fieldErrors} field="timeoutMs" />
      </div>
    </div>
  );
}

export function BrowserWaitDownloadEditor(props: EditorProps) {
  const { config, onChange, fieldErrors } = props;

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField
        {...props}
        field="clickSelector"
        label="Click Selector *"
        hint="CSS selector for the element that triggers the file download."
      />
      <ArtifactNameField {...props} />
      <div>
        <label className="label">Timeout (ms)</label>
        <EditableNumberInput
          min={1}
          fallback={60000}
          value={config.timeoutMs}
          onValueChange={(timeoutMs) => onChange({ ...config, timeoutMs })}
          className="input w-32"
        />
        <FieldMsg errors={fieldErrors} field="timeoutMs" />
      </div>
      <p className="text-xs text-gray-400">
        Clicks the element, waits for download, and saves the file as an artifact. Extension is preserved when artifact name has none.
      </p>
    </div>
  );
}

export function BrowserWaitNetworkIdleEditor(props: EditorProps) {
  const { config, onChange, fieldErrors } = props;

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <div>
        <label className="label">Timeout (ms)</label>
        <EditableNumberInput
          min={1}
          fallback={30000}
          value={config.timeoutMs}
          onValueChange={(timeoutMs) => onChange({ ...config, timeoutMs })}
          className="input w-32"
        />
        <FieldMsg errors={fieldErrors} field="timeoutMs" />
        <p className="text-xs text-gray-400 mt-1">Waits until the page reaches network idle (no in-flight requests).</p>
      </div>
    </div>
  );
}

export function BrowserElementExistsEditor(props: EditorProps) {
  const { config, onChange, fieldErrors } = props;
  const visibleOnly = config.visibleOnly !== false && config.visibleOnly !== 'false';

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField {...props} hint="CSS selector to check for within the timeout window." />
      <div>
        <label className="label">Timeout (ms)</label>
        <EditableNumberInput
          min={1}
          fallback={5000}
          value={config.timeoutMs}
          onValueChange={(timeoutMs) => onChange({ ...config, timeoutMs })}
          className="input w-32"
        />
        <FieldMsg errors={fieldErrors} field="timeoutMs" />
      </div>
      <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
        <input
          type="checkbox"
          checked={visibleOnly}
          onChange={(e) => onChange({ ...config, visibleOnly: e.target.checked })}
          className="rounded border-gray-300"
        />
        Visible only (element must be visible, not just in DOM)
      </label>
      <OutputVariableField {...props} />
      <p className="text-xs text-gray-400">Does not fail when the element is missing — writes true/false to the output variable.</p>
    </div>
  );
}

export function BrowserUploadFileEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const artifactName = String(config.artifactName ?? '');
  const filePath     = String(config.filePath ?? '');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField
        {...props}
        label="Selector *"
        hint="CSS selector for the file input, e.g. input[type='file']"
      />
      <div>
        <label className="label">Artifact Name</label>
        <input
          value={artifactName}
          onChange={(e) => onChange({ ...config, artifactName: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'artifactName')}
          className={`input font-mono text-xs ${fieldErrors?.artifactName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="report.xlsx"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="artifactName" />
        <p className="text-xs text-gray-400 mt-1">
          Use when uploading a file generated or downloaded by the workflow (takes priority over File Path).
        </p>
      </div>
      <div>
        <label className="label">File Path</label>
        <input
          value={filePath}
          onChange={(e) => onChange({ ...config, filePath: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'filePath')}
          className={`input font-mono text-xs ${fieldErrors?.filePath ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="C:\Data\template.xlsx"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="filePath" />
        <p className="text-xs text-gray-400 mt-1">
          Fixed local path on the Worker machine. Use only when not using a workflow artifact.
        </p>
      </div>
      <div>
        <label className="label">Timeout (ms)</label>
        <EditableNumberInput
          min={1}
          fallback={30000}
          value={config.timeoutMs}
          onValueChange={(timeoutMs) => onChange({ ...config, timeoutMs })}
          className="input w-32"
        />
        <FieldMsg errors={fieldErrors} field="timeoutMs" />
      </div>
      <div className="rounded-lg border border-teal-200 bg-teal-50/60 px-2.5 py-2 text-xs text-teal-900">
        Provide Artifact Name or File Path (artifact takes priority). Exposes{' '}
        <code className="bg-teal-100 px-0.5 rounded">{`{{uploadedFileName}}`}</code> and{' '}
        <code className="bg-teal-100 px-0.5 rounded">{`{{uploadedSourceType}}`}</code>.
      </div>
    </div>
  );
}

const SWITCH_TAB_MODES = ['last', 'first', 'byUrl', 'byTitle'] as const;
const CLOSE_TAB_MODES = ['current', 'last', 'first'] as const;

export function BrowserSwitchTabEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const mode = String(config.mode ?? 'last');
  const urlContains = String(config.urlContains ?? '');
  const titleContains = String(config.titleContains ?? '');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <div>
        <label className="label">Mode *</label>
        <select
          value={mode}
          onChange={(e) => onChange({ ...config, mode: e.target.value })}
          className="input text-xs"
        >
          {SWITCH_TAB_MODES.map((m) => (
            <option key={m} value={m}>{m}</option>
          ))}
        </select>
      </div>
      {mode === 'byUrl' && (
        <div>
          <label className="label">URL Contains *</label>
          <input
            value={urlContains}
            onChange={(e) => onChange({ ...config, urlContains: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'urlContains')}
            className={`input font-mono text-xs ${fieldErrors?.urlContains ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="/discover"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="urlContains" />
        </div>
      )}
      {mode === 'byTitle' && (
        <div>
          <label className="label">Title Contains *</label>
          <input
            value={titleContains}
            onChange={(e) => onChange({ ...config, titleContains: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'titleContains')}
            className={`input text-xs ${fieldErrors?.titleContains ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="Report"
          />
          <FieldMsg errors={fieldErrors} field="titleContains" />
        </div>
      )}
      <div>
        <label className="label">Timeout (ms)</label>
        <EditableNumberInput
          min={1}
          fallback={30000}
          value={config.timeoutMs}
          onValueChange={(timeoutMs) => onChange({ ...config, timeoutMs })}
          className="input w-32"
        />
        <FieldMsg errors={fieldErrors} field="timeoutMs" />
      </div>
    </div>
  );
}

export function BrowserCloseTabEditor(props: EditorProps) {
  const { config, onChange } = props;
  const mode = String(config.mode ?? 'current');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <div>
        <label className="label">Mode</label>
        <select
          value={mode}
          onChange={(e) => onChange({ ...config, mode: e.target.value })}
          className="input text-xs"
        >
          {CLOSE_TAB_MODES.map((m) => (
            <option key={m} value={m}>{m}</option>
          ))}
        </select>
        <p className="text-xs text-gray-400 mt-1">Fails safely if closing would leave no active page.</p>
      </div>
    </div>
  );
}

export function BrowserHandleAlertEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const action = String(config.action ?? 'accept');
  const promptText = String(config.promptText ?? '');
  const clickSelector = String(config.clickSelector ?? '');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <div>
        <label className="label">Action *</label>
        <select
          value={action}
          onChange={(e) => onChange({ ...config, action: e.target.value })}
          className="input text-xs"
        >
          <option value="accept">Accept</option>
          <option value="dismiss">Dismiss</option>
        </select>
      </div>
      <div>
        <label className="label">Prompt Text</label>
        <input
          value={promptText}
          onChange={(e) => onChange({ ...config, promptText: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'promptText')}
          className="input font-mono text-xs"
          placeholder="Value for prompt dialogs"
          spellCheck={false}
        />
      </div>
      <SelectorField
        {...props}
        field="clickSelector"
        label="Trigger Click Selector (optional)"
        hint="When set, clicks and handles the dialog in one step."
      />
      {!clickSelector && (
        <p className="text-xs text-amber-700">
          For click-triggered alerts, set Trigger Click Selector or use Click And Handle Alert.
        </p>
      )}
      <div>
        <label className="label">Timeout (ms)</label>
        <EditableNumberInput
          min={1}
          fallback={30000}
          value={config.timeoutMs}
          onValueChange={(timeoutMs) => onChange({ ...config, timeoutMs })}
          className="input w-32"
        />
        <FieldMsg errors={fieldErrors} field="timeoutMs" />
      </div>
    </div>
  );
}

export function BrowserWaitPopupEditor(props: EditorProps) {
  const { config, onChange, fieldErrors } = props;
  const switchToPopup = config.switchToPopup !== false && config.switchToPopup !== 'false';

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField
        {...props}
        field="clickSelector"
        label="Click Selector *"
        hint="Element that opens a popup or new window."
      />
      <div>
        <label className="label">Timeout (ms)</label>
        <EditableNumberInput
          min={1}
          fallback={30000}
          value={config.timeoutMs}
          onValueChange={(timeoutMs) => onChange({ ...config, timeoutMs })}
          className="input w-32"
        />
        <FieldMsg errors={fieldErrors} field="timeoutMs" />
      </div>
      <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
        <input
          type="checkbox"
          checked={switchToPopup}
          onChange={(e) => onChange({ ...config, switchToPopup: e.target.checked })}
          className="rounded border-gray-300"
        />
        Switch to popup (make popup the active page)
      </label>
    </div>
  );
}

export function BrowserClickAndHandleAlertEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const action = String(config.action ?? 'accept');
  const promptText = String(config.promptText ?? '');

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />
      <SelectorField
        {...props}
        field="clickSelector"
        label="Click Selector *"
        hint="Button or link that triggers the JavaScript dialog."
      />
      <div>
        <label className="label">Action *</label>
        <select
          value={action}
          onChange={(e) => onChange({ ...config, action: e.target.value })}
          className="input text-xs"
        >
          <option value="accept">Accept</option>
          <option value="dismiss">Dismiss</option>
        </select>
      </div>
      <div>
        <label className="label">Prompt Text</label>
        <input
          value={promptText}
          onChange={(e) => onChange({ ...config, promptText: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'promptText')}
          className="input font-mono text-xs"
          placeholder="Value for prompt dialogs"
          spellCheck={false}
        />
      </div>
      <div>
        <label className="label">Timeout (ms)</label>
        <EditableNumberInput
          min={1}
          fallback={30000}
          value={config.timeoutMs}
          onValueChange={(timeoutMs) => onChange({ ...config, timeoutMs })}
          className="input w-32"
        />
        <FieldMsg errors={fieldErrors} field="timeoutMs" />
      </div>
    </div>
  );
}

const EXTRACT_MODES = [
  { value: 'htmlTable', label: 'HTML Table' },
  { value: 'cssGrid', label: 'CSS Grid' },
] as const;

export function BrowserExtractTableEditor(props: EditorProps) {
  const { config, onChange, onFocusField, fieldErrors } = props;
  const mode              = String(config.mode ?? 'htmlTable');
  const tableIndex        = Number(config.tableIndex ?? 0);
  const outputVariable    = String(config.outputVariable ?? 'webTable');
  const includeHeaders    = config.includeHeaders !== false && config.includeHeaders !== 'false';
  const skipEmptyRows     = config.skipEmptyRows !== false && config.skipEmptyRows !== 'false';
  const normalizeWhitespace = config.normalizeWhitespace !== false && config.normalizeWhitespace !== 'false';
  const isHtmlTable       = mode === 'htmlTable';

  return (
    <div className="space-y-3">
      <SessionNameField {...props} />

      <div>
        <label className="label">Mode *</label>
        <select
          value={mode}
          onChange={(e) => onChange({ ...config, mode: e.target.value })}
          className={`input text-xs ${fieldErrors?.mode ? 'border-red-400 focus:ring-red-400' : ''}`}
        >
          {EXTRACT_MODES.map((m) => (
            <option key={m.value} value={m.value}>{m.label}</option>
          ))}
        </select>
        <FieldMsg errors={fieldErrors} field="mode" />
      </div>

      {isHtmlTable ? (
        <>
          <SelectorField
            {...props}
            field="selector"
            label="Selector *"
            hint="CSS selector for table elements, e.g. table or #results table"
          />
          <div>
            <label className="label">Table Index</label>
            <input
              type="number"
              min={0}
              value={tableIndex}
              onChange={(e) => onChange({ ...config, tableIndex: parseInt(e.target.value, 10) || 0 })}
              onFocus={(e) => onFocusField(e.currentTarget, 'tableIndex')}
              className={`input font-mono text-xs w-24 ${fieldErrors?.tableIndex ? 'border-red-400 focus:ring-red-400' : ''}`}
            />
            <FieldMsg errors={fieldErrors} field="tableIndex" />
            <p className="text-xs text-gray-400 mt-1">0 = first matching table on the page.</p>
          </div>
        </>
      ) : (
        <>
          <SelectorField
            {...props}
            field="selector"
            label="Scope Selector"
            hint="Optional container to limit row search (leave empty for whole page)."
          />
          <SelectorField
            {...props}
            field="rowSelector"
            label="Row Selector *"
            hint="CSS selector for each grid row, e.g. .euiTableRow"
          />
          <SelectorField
            {...props}
            field="cellSelector"
            label="Cell Selector *"
            hint="CSS selector for cells within each row, e.g. .euiTableRowCell"
          />
        </>
      )}

      <div>
        <label className="label">Output Variable *</label>
        <input
          value={outputVariable}
          onChange={(e) => onChange({ ...config, outputVariable: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
          className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="webTable"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputVariable" />
        {!fieldErrors?.outputVariable && outputVariable && (
          <p className="text-xs text-gray-400 mt-1">
            Exposes{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}_count}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}_columns}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}_first}}`}</code>
          </p>
        )}
      </div>

      <div className="space-y-2">
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={includeHeaders}
            onChange={(e) => onChange({ ...config, includeHeaders: e.target.checked })}
            className="rounded border-gray-300"
          />
          Include headers (first row or thead)
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={skipEmptyRows}
            onChange={(e) => onChange({ ...config, skipEmptyRows: e.target.checked })}
            className="rounded border-gray-300"
          />
          Skip empty rows
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={normalizeWhitespace}
            onChange={(e) => onChange({ ...config, normalizeWhitespace: e.target.checked })}
            className="rounded border-gray-300"
          />
          Normalize whitespace in cell text
        </label>
      </div>

      <div>
        <label className="label">Timeout (ms)</label>
        <EditableNumberInput
          min={1}
          fallback={30000}
          value={config.timeoutMs}
          onValueChange={(timeoutMs) => onChange({ ...config, timeoutMs })}
          className="input w-32"
        />
        <FieldMsg errors={fieldErrors} field="timeoutMs" />
      </div>
    </div>
  );
}
