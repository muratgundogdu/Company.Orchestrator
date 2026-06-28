import type { EditorProps } from './types';

const COMPRESSION_LEVELS = [
  { value: 'optimal', label: 'Optimal (balanced)' },
  { value: 'fastest', label: 'Fastest' },
  { value: 'noCompression', label: 'No compression' },
] as const;

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

function displayArtifactNames(value: unknown): string {
  if (Array.isArray(value)) return value.map(String).join(', ');
  return String(value ?? '');
}

export default function ZipCreateEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const inputArtifactNames = displayArtifactNames(config.inputArtifactNames);
  const outputName         = String(config.outputName ?? 'reports.zip');
  const compressionLevel   = String(config.compressionLevel ?? 'optimal');

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
          placeholder="report.xlsx, summary.csv  or  {{extractedFiles}}"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="inputArtifactNames" />
      </div>

      <div>
        <label className="label">Output Name *</label>
        <input
          value={outputName}
          onChange={(e) => onChange({ ...config, outputName: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputName')}
          className={`input font-mono text-xs ${fieldErrors?.outputName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="reports.zip"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputName" />
      </div>

      <div>
        <label className="label">Compression Level *</label>
        <select
          value={compressionLevel}
          onChange={(e) => onChange({ ...config, compressionLevel: e.target.value })}
          className={`input text-xs ${fieldErrors?.compressionLevel ? 'border-red-400 focus:ring-red-400' : ''}`}
        >
          {COMPRESSION_LEVELS.map((level) => (
            <option key={level.value} value={level.value}>{level.label}</option>
          ))}
        </select>
        <FieldMsg errors={fieldErrors} field="compressionLevel" />
      </div>
    </div>
  );
}
