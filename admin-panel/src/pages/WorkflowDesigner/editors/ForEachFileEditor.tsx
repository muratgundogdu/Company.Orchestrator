import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

const FILE_FIELDS = ['name', 'fullPath', 'directory', 'extension', 'sizeBytes', 'lastModified'] as const;

export default function ForEachFileEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const collectionVariable = String(config.collectionVariable ?? 'files');
  const fileVariable       = String(config.fileVariable       ?? 'currentFile');
  const indexVariable      = String(config.indexVariable      ?? 'currentIndex');

  function set(key: string, value: string) {
    onChange({ ...config, [key]: value });
  }

  const preview = collectionVariable
    ? `for each file in ${collectionVariable}`
    : 'for each file in <collection>';

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Collection Variable *</label>
        <input
          value={collectionVariable}
          onChange={(e) => set('collectionVariable', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'collectionVariable')}
          className={`input font-mono text-xs ${fieldErrors?.collectionVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="files"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="collectionVariable" />
        {!fieldErrors?.collectionVariable && (
          <p className="text-xs text-gray-400 mt-1">
            JSON array from folder.list-files (e.g. <code className="bg-gray-100 px-0.5 rounded">files</code>).
          </p>
        )}
      </div>

      <div>
        <label className="label">File Variable *</label>
        <input
          value={fileVariable}
          onChange={(e) => set('fileVariable', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'fileVariable')}
          className={`input font-mono text-xs ${fieldErrors?.fileVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="currentFile"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="fileVariable" />
        <p className="text-xs text-gray-400 mt-1">
          Current file JSON as{' '}
          <code className="bg-gray-100 px-0.5 rounded">{`{{${fileVariable || 'currentFile'}}}`}</code>.
        </p>
      </div>

      <div>
        <label className="label">Index Variable *</label>
        <input
          value={indexVariable}
          onChange={(e) => set('indexVariable', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'indexVariable')}
          className={`input font-mono text-xs ${fieldErrors?.indexVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="currentIndex"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="indexVariable" />
        <p className="text-xs text-gray-400 mt-1">
          Zero-based index as{' '}
          <code className="bg-gray-100 px-0.5 rounded">{`{{${indexVariable || 'currentIndex'}}}`}</code>.
        </p>
      </div>

      <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 space-y-1">
        <p className="text-xs text-emerald-700 font-semibold">Loop preview</p>
        <code className="text-xs text-emerald-800 font-mono">{preview}</code>
        <div className="text-xs text-emerald-600 space-y-0.5 mt-1">
          {FILE_FIELDS.map((field) => (
            <div key={field}>
              → <code className="font-mono">{`{{${fileVariable || 'currentFile'}.${field}}}`}</code>
            </div>
          ))}
        </div>
      </div>

      <div className="rounded-lg border border-gray-200 bg-white px-3 py-2.5 space-y-1.5">
        <p className="text-xs font-semibold text-gray-600 mb-1">Output handles</p>
        <div className="flex items-center gap-2">
          <span className="flex h-3.5 w-3.5 items-center justify-center rounded-full bg-cyan-600 text-white font-bold shrink-0" style={{ fontSize: 7 }}>→</span>
          <span className="text-xs text-gray-500">
            <strong>Body</strong> (right handle) — first step executed for each file
          </span>
          {fieldErrors?.['loop-body'] && (
            <span className="text-xs text-red-600 font-medium">⚠ {fieldErrors['loop-body']}</span>
          )}
        </div>
        <div className="flex items-center gap-2">
          <span className="flex h-3.5 w-3.5 items-center justify-center rounded-full bg-green-600 text-white font-bold shrink-0" style={{ fontSize: 7 }}>✓</span>
          <span className="text-xs text-gray-500">
            <strong>Done</strong> (bottom handle) — runs after all files are processed
          </span>
          {fieldErrors?.['completed'] && (
            <span className="text-xs text-red-600 font-medium">⚠ {fieldErrors['completed']}</span>
          )}
        </div>
        <div className="flex items-center gap-2">
          <span className="flex h-3.5 w-3.5 items-center justify-center rounded-full bg-slate-500 text-white font-bold shrink-0" style={{ fontSize: 7 }}>↵</span>
          <span className="text-xs text-gray-500">
            <strong>Back</strong> (top handle) — connect the last body step back here
          </span>
        </div>
      </div>
    </div>
  );
}
