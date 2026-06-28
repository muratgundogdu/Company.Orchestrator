import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function ForEachEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const collection    = String(config.collection    ?? '');
  const itemVariable  = String(config.itemVariable  ?? 'currentItem');
  const indexVariable = String(config.indexVariable ?? 'currentIndex');

  function set(key: string, value: string) {
    onChange({ ...config, [key]: value });
  }

  const preview = collection
    ? `for each item in ${collection}`
    : 'for each item in <collection>';

  return (
    <div className="space-y-3">

      {/* Collection Variable */}
      <div>
        <label className="label">Collection Variable *</label>
        <input
          value={collection}
          onChange={(e) => set('collection', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'collection')}
          className={`input font-mono text-xs ${fieldErrors?.['collection'] ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="{{mailArtifacts}}"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="collection" />
        {!fieldErrors?.['collection'] && (
          <p className="text-xs text-gray-400 mt-1">
            Use the variable picker to insert an upstream collection variable.
          </p>
        )}
      </div>

      {/* Current Item Variable */}
      <div>
        <label className="label">Current Item Variable</label>
        <input
          value={itemVariable}
          onChange={(e) => set('itemVariable', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'itemVariable')}
          className="input font-mono text-xs"
          placeholder="currentItem"
          spellCheck={false}
        />
        <p className="text-xs text-gray-400 mt-1">
          Available inside the loop body as <code className="bg-gray-100 px-0.5 rounded">{`{{${itemVariable || 'currentItem'}}}`}</code>.
        </p>
      </div>

      {/* Current Index Variable */}
      <div>
        <label className="label">Current Index Variable</label>
        <input
          value={indexVariable}
          onChange={(e) => set('indexVariable', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'indexVariable')}
          className="input font-mono text-xs"
          placeholder="currentIndex"
          spellCheck={false}
        />
        <p className="text-xs text-gray-400 mt-1">
          Zero-based index, available as <code className="bg-gray-100 px-0.5 rounded">{`{{${indexVariable || 'currentIndex'}}}`}</code>.
        </p>
      </div>

      {/* Preview */}
      <div className="rounded-lg border border-cyan-200 bg-cyan-50 px-3 py-2 space-y-1">
        <p className="text-xs text-cyan-700 font-semibold">Loop preview</p>
        <code className="text-xs text-cyan-800 font-mono">{preview}</code>
        <div className="text-xs text-cyan-600 space-y-0.5 mt-1">
          <div>→ <code className="font-mono">{`{{${itemVariable || 'currentItem'}}}`}</code> — current item value</div>
          <div>→ <code className="font-mono">{`{{${indexVariable || 'currentIndex'}}}`}</code> — 0-based index</div>
        </div>
      </div>

      {/* Handle guide */}
      <div className="rounded-lg border border-gray-200 bg-white px-3 py-2.5 space-y-1.5">
        <p className="text-xs font-semibold text-gray-600 mb-1">Output handles</p>
        <div className="flex items-center gap-2">
          <span className="flex h-3.5 w-3.5 items-center justify-center rounded-full bg-cyan-600 text-white font-bold shrink-0" style={{ fontSize: 7 }}>→</span>
          <span className="text-xs text-gray-500">
            <strong>Body</strong> (right handle) — drag to first step inside the loop
          </span>
          {fieldErrors?.['loop-body'] && (
            <span className="text-xs text-red-600 font-medium">⚠ {fieldErrors['loop-body']}</span>
          )}
        </div>
        <div className="flex items-center gap-2">
          <span className="flex h-3.5 w-3.5 items-center justify-center rounded-full bg-green-600 text-white font-bold shrink-0" style={{ fontSize: 7 }}>✓</span>
          <span className="text-xs text-gray-500">
            <strong>Done</strong> (bottom handle) — runs after all items are processed
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
