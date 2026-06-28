import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1 flex items-center gap-1">⚠ {msg}</p> : null;
}

export default function FolderReadEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const sourcePath   = String(config.sourcePath   ?? '');
  const artifactName = String(config.artifactName ?? '');

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Source Path</label>
        <input
          value={sourcePath}
          onChange={(e) => onChange({ ...config, sourcePath: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'sourcePath')}
          className={`input font-mono text-xs ${fieldErrors?.sourcePath ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="{{triggerFilePath}}"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="sourcePath" />
        {!fieldErrors?.sourcePath && (
          <p className="text-xs text-gray-400 mt-1">
            Full path to the file on disk. For FolderWatcher workflows use{' '}
            <code className="bg-gray-100 px-0.5 rounded">{'{{triggerFilePath}}'}</code>.
          </p>
        )}
      </div>

      <div>
        <label className="label">Artifact Name</label>
        <input
          value={artifactName}
          onChange={(e) => onChange({ ...config, artifactName: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'artifactName')}
          className={`input font-mono text-xs ${fieldErrors?.artifactName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="source-file"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="artifactName" />
        {!fieldErrors?.artifactName && (
          <p className="text-xs text-gray-400 mt-1">
            Key used to reference this file in downstream steps.
          </p>
        )}
      </div>
    </div>
  );
}
