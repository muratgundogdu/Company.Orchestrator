import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function WordFillTemplateEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const inputArtifactName = String(config.inputArtifactName ?? 'template.docx');
  const outputName        = String(config.outputName ?? 'generated.docx');
  const outputVariable    = String(config.outputVariable ?? 'generatedDocument');
  const strictMode        = config.strictMode === true || config.strictMode === 'true';

  function set(key: string, value: unknown) {
    onChange({ ...config, [key]: value });
  }

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Input Artifact Name *</label>
        <input
          value={inputArtifactName}
          onChange={(e) => set('inputArtifactName', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'inputArtifactName')}
          className={`input font-mono text-xs ${fieldErrors?.inputArtifactName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="template.docx"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="inputArtifactName" />
      </div>

      <div>
        <label className="label">Output Name *</label>
        <input
          value={outputName}
          onChange={(e) => set('outputName', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputName')}
          className={`input font-mono text-xs ${fieldErrors?.outputName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="generated.docx"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputName" />
      </div>

      <div>
        <label className="label">Output Variable *</label>
        <input
          value={outputVariable}
          onChange={(e) => set('outputVariable', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
          className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="generatedDocument"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputVariable" />
        {!fieldErrors?.outputVariable && outputVariable && (
          <p className="text-xs text-gray-400 mt-1">
            Exposes{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{outputName}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{placeholdersReplaced}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{missingPlaceholders}}`}</code>
          </p>
        )}
      </div>

      <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
        <input
          type="checkbox"
          checked={strictMode}
          onChange={(e) => set('strictMode', e.target.checked)}
          className="rounded border-gray-300"
        />
        Strict mode (fail when a placeholder variable is missing)
      </label>

      <div className="rounded-lg border border-blue-200 bg-blue-50/60 px-2.5 py-2 text-xs text-blue-900 space-y-1">
        <p>Use <code className="bg-blue-100 px-0.5 rounded">{`{{VariableName}}`}</code> placeholders in the template.</p>
        <p>Supports nested paths like <code className="bg-blue-100 px-0.5 rounded">{`{{currentRow.Amount}}`}</code>.</p>
        <p>Attach the output with Mail Send using the output artifact name.</p>
      </div>
    </div>
  );
}
