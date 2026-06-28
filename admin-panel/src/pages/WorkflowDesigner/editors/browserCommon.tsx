import { useState } from 'react';
import { Crosshair } from 'lucide-react';
import type { EditorProps } from './types';
import PickSelectorModal from '../PickSelectorModal';

export function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export function SessionNameField({ config, onChange, onFocusField }: EditorProps) {
  const sessionName = String(config.sessionName ?? 'default');
  return (
    <div>
      <label className="label">Session Name</label>
      <input
        value={sessionName}
        onChange={(e) => onChange({ ...config, sessionName: e.target.value })}
        onFocus={(e) => onFocusField(e.currentTarget, 'sessionName')}
        className="input font-mono text-xs"
        placeholder="default"
        spellCheck={false}
      />
      <p className="text-xs text-gray-400 mt-1">Logical browser session label (shared across steps in a workflow).</p>
    </div>
  );
}

export function SelectorField({
  config,
  onChange,
  onFocusField,
  fieldErrors,
  field = 'selector',
  label = 'Selector',
  hint = 'CSS selector for the target element.',
  pickUrl,
}: EditorProps & { field?: string; label?: string; hint?: string; pickUrl?: string }) {
  const value = String(config[field] ?? '');
  const [pickerOpen, setPickerOpen] = useState(false);
  const defaultPickUrl = pickUrl ?? String(config.url ?? 'https://www.google.com');

  return (
    <div>
      <label className="label">{label}</label>
      <div className="flex gap-2">
        <input
          value={value}
          onChange={(e) => onChange({ ...config, [field]: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, field)}
          className={`input font-mono text-xs flex-1 min-w-0 ${fieldErrors?.[field] ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="#submit-btn"
          spellCheck={false}
        />
        <button
          type="button"
          title="Pick selector from live page"
          onClick={() => setPickerOpen(true)}
          className="btn btn-secondary btn-sm shrink-0 px-2.5"
        >
          <Crosshair size={14} />
          Pick
        </button>
      </div>
      <FieldMsg errors={fieldErrors} field={field} />
      {!fieldErrors?.[field] && <p className="text-xs text-gray-400 mt-1">{hint}</p>}
      {pickerOpen && (
        <PickSelectorModal
          initialUrl={defaultPickUrl}
          onUse={(selector) => onChange({ ...config, [field]: selector })}
          onClose={() => setPickerOpen(false)}
        />
      )}
    </div>
  );
}

export function OutputVariableField({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const outputVariable = String(config.outputVariable ?? '');
  return (
    <div>
      <label className="label">Output Variable *</label>
      <input
        value={outputVariable}
        onChange={(e) => onChange({ ...config, outputVariable: e.target.value })}
        onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
        className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
        placeholder="browserText"
        spellCheck={false}
      />
      <FieldMsg errors={fieldErrors} field="outputVariable" />
      {!fieldErrors?.outputVariable && (
        <p className="text-xs text-gray-400 mt-1">
          Referenced downstream as{' '}
          <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable || 'var'}}}`}</code>
        </p>
      )}
    </div>
  );
}

export function ArtifactNameField({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const artifactName = String(config.artifactName ?? '');
  return (
    <div>
      <label className="label">Artifact Name *</label>
      <input
        value={artifactName}
        onChange={(e) => onChange({ ...config, artifactName: e.target.value })}
        onFocus={(e) => onFocusField(e.currentTarget, 'artifactName')}
        className={`input font-mono text-xs ${fieldErrors?.artifactName ? 'border-red-400 focus:ring-red-400' : ''}`}
        placeholder="browser-screenshot"
        spellCheck={false}
      />
      <FieldMsg errors={fieldErrors} field="artifactName" />
    </div>
  );
}
