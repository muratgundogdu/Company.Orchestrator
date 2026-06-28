import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function PdfReadTextEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const inputArtifactName   = String(config.inputArtifactName ?? 'invoice.pdf');
  const pageRange           = String(config.pageRange ?? '');
  const outputVariable      = String(config.outputVariable ?? 'pdfText');
  const normalizeWhitespace = config.normalizeWhitespace !== false && config.normalizeWhitespace !== 'false';
  const failIfEmpty         = config.failIfEmpty === true || config.failIfEmpty === 'true';

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
          placeholder="invoice.pdf"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="inputArtifactName" />
      </div>

      <div>
        <label className="label">Page Range</label>
        <input
          value={pageRange}
          onChange={(e) => set('pageRange', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'pageRange')}
          className="input font-mono text-xs"
          placeholder="1-5  or  1,3,5  or  1-3,7,9-10"
          spellCheck={false}
        />
        <p className="text-xs text-gray-400 mt-1">
          Leave empty for all pages. Examples: <code className="bg-gray-100 px-0.5 rounded">3</code>,{' '}
          <code className="bg-gray-100 px-0.5 rounded">1-5</code>,{' '}
          <code className="bg-gray-100 px-0.5 rounded">1-3,7,9-10</code>
        </p>
      </div>

      <div>
        <label className="label">Output Variable *</label>
        <input
          value={outputVariable}
          onChange={(e) => set('outputVariable', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
          className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="pdfText"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputVariable" />
        {!fieldErrors?.outputVariable && outputVariable && (
          <p className="text-xs text-gray-400 mt-1">
            Exposes{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}_length}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}_pageCount}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}_first500}}`}</code>
          </p>
        )}
      </div>

      <div className="space-y-2">
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={normalizeWhitespace}
            onChange={(e) => set('normalizeWhitespace', e.target.checked)}
            className="rounded border-gray-300"
          />
          Normalize whitespace (collapse spaces and line breaks)
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={failIfEmpty}
            onChange={(e) => set('failIfEmpty', e.target.checked)}
            className="rounded border-gray-300"
          />
          Fail if no text is extracted (text-based PDF required)
        </label>
      </div>

      <div className="rounded-lg border border-rose-200 bg-rose-50/60 px-2.5 py-2 text-xs text-rose-900 space-y-1">
        <p>Text-based PDFs only — scanned/image PDFs need a future OCR phase.</p>
        <p>Use with Mail Extract Value to pull labels like Invoice No from pdfText.</p>
      </div>
    </div>
  );
}
