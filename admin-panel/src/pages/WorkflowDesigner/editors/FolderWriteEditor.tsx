import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function FolderWriteEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const artifactName    = String(config.artifactName    ?? '');
  const destinationPath = String(config.destinationPath ?? '');
  const overwrite       = config.overwrite === true || config.overwrite === 'true';

  function updateField(key: string, value: unknown) {
    onChange({ ...config, [key]: value });
  }

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Artifact Name</label>
        <input
          value={artifactName}
          onChange={(e) => updateField('artifactName', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'artifactName')}
          className={`input font-mono text-xs ${fieldErrors?.artifactName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="transformed-excel.xlsx"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="artifactName" />
        {!fieldErrors?.artifactName && (
          <p className="text-xs text-gray-400 mt-1">
            Name of the artifact to write. Use the variable picker to pick an upstream artifact.
          </p>
        )}
      </div>

      <div>
        <label className="label">Destination Path</label>
        <input
          value={destinationPath}
          onChange={(e) => updateField('destinationPath', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'destinationPath')}
          className={`input font-mono text-xs ${fieldErrors?.destinationPath ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="C:\Temp\AlterOneOutput\result.xlsx"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="destinationPath" />
        {!fieldErrors?.destinationPath && (
          <p className="text-xs text-gray-400 mt-1">Full path where the file will be written.</p>
        )}
      </div>

      <label className="flex items-center gap-2 cursor-pointer select-none">
        <input
          type="checkbox"
          checked={overwrite}
          onChange={(e) => updateField('overwrite', e.target.checked)}
          className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
        />
        <span className="text-sm font-medium text-gray-700">Overwrite if file exists</span>
      </label>
    </div>
  );
}
