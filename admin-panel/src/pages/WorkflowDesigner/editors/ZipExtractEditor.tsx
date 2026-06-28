import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function ZipExtractEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const inputArtifactName = String(config.inputArtifactName ?? 'reports.zip');
  const outputPrefix      = String(config.outputPrefix ?? 'unzipped');
  const filterPattern     = String(config.filterPattern ?? '*.xlsx');
  const outputVariable    = String(config.outputVariable ?? 'extractedFiles');
  const overwrite         = config.overwrite !== false && config.overwrite !== 'false';
  const failIfNoMatches   = config.failIfNoMatches === true || config.failIfNoMatches === 'true';

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
          placeholder="reports.zip"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="inputArtifactName" />
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Output Prefix *</label>
          <input
            value={outputPrefix}
            onChange={(e) => set('outputPrefix', e.target.value)}
            onFocus={(e) => onFocusField(e.currentTarget, 'outputPrefix')}
            className={`input font-mono text-xs ${fieldErrors?.outputPrefix ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="unzipped"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="outputPrefix" />
          <p className="text-xs text-gray-400 mt-1">Example: unzipped-report1.xlsx</p>
        </div>
        <div>
          <label className="label">Filter Pattern</label>
          <input
            value={filterPattern}
            onChange={(e) => set('filterPattern', e.target.value)}
            onFocus={(e) => onFocusField(e.currentTarget, 'filterPattern')}
            className="input font-mono text-xs"
            placeholder="*.xlsx"
            spellCheck={false}
          />
          <p className="text-xs text-gray-400 mt-1">Examples: *.*, *.csv, report_*.json</p>
        </div>
      </div>

      <div>
        <label className="label">Output Variable *</label>
        <input
          value={outputVariable}
          onChange={(e) => set('outputVariable', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
          className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="extractedFiles"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputVariable" />
      </div>

      <div className="space-y-2">
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input type="checkbox" checked={overwrite} onChange={(e) => set('overwrite', e.target.checked)} className="rounded border-gray-300" />
          Overwrite (reserved — duplicate names get -2, -3 suffixes)
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input type="checkbox" checked={failIfNoMatches} onChange={(e) => set('failIfNoMatches', e.target.checked)} className="rounded border-gray-300" />
          Fail if no files match the filter
        </label>
      </div>
    </div>
  );
}
