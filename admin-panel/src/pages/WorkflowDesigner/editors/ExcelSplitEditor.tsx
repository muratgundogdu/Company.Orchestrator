import type { EditorProps } from './types';
import EditableNumberInput from './EditableNumberInput';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function ExcelSplitEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const inputArtifactName = String(config.inputArtifactName ?? '');
  const sourceSheetName   = String(config.sourceSheetName   ?? 'Data');
  const splitColumn       = String(config.splitColumn       ?? 'Region');
  const outputNamePattern = String(config.outputNamePattern ?? 'split-{value}.xlsx');
  const includeHeader     = config.includeHeader !== false && config.includeHeader !== 'false';

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Input Artifact Name *</label>
        <input
          value={inputArtifactName}
          onChange={(e) => onChange({ ...config, inputArtifactName: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'inputArtifactName')}
          className={`input font-mono text-xs ${fieldErrors?.inputArtifactName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="source-file"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="inputArtifactName" />
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
          <label className="label">Split Column *</label>
          <input
            value={splitColumn}
            onChange={(e) => onChange({ ...config, splitColumn: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'splitColumn')}
            className={`input font-mono text-xs ${fieldErrors?.splitColumn ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="Region"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="splitColumn" />
        </div>
      </div>

      <div>
        <label className="label">Output Name Pattern *</label>
        <input
          value={outputNamePattern}
          onChange={(e) => onChange({ ...config, outputNamePattern: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputNamePattern')}
          className={`input font-mono text-xs ${fieldErrors?.outputNamePattern ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="split-{value}.xlsx"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputNamePattern" />
        <p className="text-xs text-gray-400 mt-1">
          Use <code className="bg-gray-100 px-0.5 rounded">{'{value}'}</code> for the group value and{' '}
          <code className="bg-gray-100 px-0.5 rounded">{'{index}'}</code> for a zero-based index.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Start Row</label>
          <EditableNumberInput
            min={1}
            fallback={2}
            value={config.startRow}
            onValueChange={(startRow) => onChange({ ...config, startRow })}
            className={`input text-xs ${fieldErrors?.startRow ? 'border-red-400 focus:ring-red-400' : ''}`}
          />
          <FieldMsg errors={fieldErrors} field="startRow" />
        </div>
        <div>
          <label className="label">Include Header</label>
          <label className="flex items-center gap-2 text-xs text-gray-700 cursor-pointer mt-1">
            <input
              type="checkbox"
              checked={includeHeader}
              onChange={(e) => onChange({ ...config, includeHeader: e.target.checked })}
              className="rounded"
            />
            Copy header row into each output
          </label>
        </div>
      </div>
    </div>
  );
}
