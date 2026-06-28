import type { EditorProps } from './types';
import EditableNumberInput from './EditableNumberInput';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

function displayArtifactNames(value: unknown): string {
  if (Array.isArray(value)) return value.map(String).join(', ');
  return String(value ?? '');
}

export default function ExcelMergeEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const inputArtifactNames = displayArtifactNames(config.inputArtifactNames);
  const outputName           = String(config.outputName           ?? 'merged-excel');
  const sourceSheetName      = String(config.sourceSheetName      ?? 'Data');
  const targetSheetName      = String(config.targetSheetName      ?? 'Merged');
  const includeHeaderOnce    = config.includeHeaderOnce !== false && config.includeHeaderOnce !== 'false';

  function setArtifactNames(raw: string) {
    const trimmed = raw.trim();
    if (!trimmed) {
      onChange({ ...config, inputArtifactNames: '' });
      return;
    }

    if (trimmed.includes('{{')) {
      onChange({ ...config, inputArtifactNames: trimmed });
      return;
    }

    const names = trimmed.split(',').map((s) => s.trim()).filter(Boolean);
    onChange({ ...config, inputArtifactNames: names.length === 1 ? names[0] : names });
  }

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Input Artifact Names *</label>
        <input
          value={inputArtifactNames}
          onChange={(e) => setArtifactNames(e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'inputArtifactNames')}
          className={`input font-mono text-xs ${fieldErrors?.inputArtifactNames ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="file1.xlsx, file2.xlsx  or  {{mailArtifacts}}"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="inputArtifactNames" />
        {!fieldErrors?.inputArtifactNames && (
          <p className="text-xs text-gray-400 mt-1">
            Comma-separated artifact names or a variable (e.g.{' '}
            <code className="bg-gray-100 px-0.5 rounded">{'{{mailArtifacts}}'}</code>).
          </p>
        )}
      </div>

      <div>
        <label className="label">Output Name *</label>
        <input
          value={outputName}
          onChange={(e) => onChange({ ...config, outputName: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputName')}
          className={`input font-mono text-xs ${fieldErrors?.outputName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="merged-excel"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputName" />
        {!fieldErrors?.outputName && (
          <p className="text-xs text-gray-400 mt-1">
            Output artifact:{' '}
            <code className="bg-gray-100 px-0.5 rounded">{outputName || '…'}.xlsx</code>
          </p>
        )}
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Source Sheet Name *</label>
          <input
            value={sourceSheetName}
            onChange={(e) => onChange({ ...config, sourceSheetName: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'sourceSheetName')}
            className={`input text-xs ${fieldErrors?.sourceSheetName ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="Data"
          />
          <FieldMsg errors={fieldErrors} field="sourceSheetName" />
        </div>
        <div>
          <label className="label">Target Sheet Name *</label>
          <input
            value={targetSheetName}
            onChange={(e) => onChange({ ...config, targetSheetName: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'targetSheetName')}
            className={`input text-xs ${fieldErrors?.targetSheetName ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="Merged"
          />
          <FieldMsg errors={fieldErrors} field="targetSheetName" />
        </div>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Start Row</label>
          <EditableNumberInput
            min={1}
            fallback={1}
            value={config.startRow}
            onValueChange={(startRow) => onChange({ ...config, startRow })}
            className={`input text-xs ${fieldErrors?.startRow ? 'border-red-400 focus:ring-red-400' : ''}`}
          />
          <FieldMsg errors={fieldErrors} field="startRow" />
        </div>
        <div>
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer mt-6">
            <input
              type="checkbox"
              checked={includeHeaderOnce}
              onChange={(e) => onChange({ ...config, includeHeaderOnce: e.target.checked })}
              className="rounded"
            />
            Include Header Once
          </label>
        </div>
      </div>
      <p className="text-xs text-gray-400 -mt-1">
        When enabled, the header row is copied only from the first file; subsequent files skip row 1.
      </p>
    </div>
  );
}
