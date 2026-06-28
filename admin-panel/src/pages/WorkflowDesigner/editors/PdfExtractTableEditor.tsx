import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

const PARSER_MODES = [
  { value: 'auto', label: 'Auto' },
  { value: 'delimiter', label: 'Delimiter' },
  { value: 'multiSpace', label: 'Multi Space' },
  { value: 'fixedWidth', label: 'Fixed Width (future)', disabled: true },
] as const;

export default function PdfExtractTableEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const inputArtifactName   = String(config.inputArtifactName ?? 'report.pdf');
  const pageRange           = String(config.pageRange ?? '');
  const tableIndex          = Number(config.tableIndex ?? 0);
  const parserMode          = String(config.parserMode ?? 'auto');
  const delimiter           = String(config.delimiter ?? '');
  const outputVariable      = String(config.outputVariable ?? 'pdfTable');
  const hasHeader           = config.hasHeader !== false && config.hasHeader !== 'false';
  const normalizeWhitespace = config.normalizeWhitespace !== false && config.normalizeWhitespace !== 'false';
  const failIfNoTable       = config.failIfNoTable !== false && config.failIfNoTable !== 'false';

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
          placeholder="report.pdf"
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
          Leave empty for all pages. Same format as PDF Read Text.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="label">Table Index</label>
          <input
            type="number"
            min={0}
            value={tableIndex}
            onChange={(e) => set('tableIndex', parseInt(e.target.value, 10) || 0)}
            onFocus={(e) => onFocusField(e.currentTarget, 'tableIndex')}
            className={`input font-mono text-xs ${fieldErrors?.tableIndex ? 'border-red-400 focus:ring-red-400' : ''}`}
          />
          <FieldMsg errors={fieldErrors} field="tableIndex" />
        </div>

        <div>
          <label className="label">Parser Mode *</label>
          <select
            value={parserMode}
            onChange={(e) => set('parserMode', e.target.value)}
            className={`input text-xs ${fieldErrors?.parserMode ? 'border-red-400 focus:ring-red-400' : ''}`}
          >
            {PARSER_MODES.map((mode) => (
              <option key={mode.value} value={mode.value} disabled={'disabled' in mode && mode.disabled}>
                {mode.label}
              </option>
            ))}
          </select>
          <FieldMsg errors={fieldErrors} field="parserMode" />
        </div>
      </div>

      {parserMode === 'delimiter' && (
        <div>
          <label className="label">Delimiter *</label>
          <input
            value={delimiter}
            onChange={(e) => set('delimiter', e.target.value)}
            onFocus={(e) => onFocusField(e.currentTarget, 'delimiter')}
            className={`input font-mono text-xs ${fieldErrors?.delimiter ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder=",  ;  |  tab"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="delimiter" />
        </div>
      )}

      <div>
        <label className="label">Output Variable *</label>
        <input
          value={outputVariable}
          onChange={(e) => set('outputVariable', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
          className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="pdfTable"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputVariable" />
        {!fieldErrors?.outputVariable && outputVariable && (
          <p className="text-xs text-gray-400 mt-1">
            Exposes{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}_count}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}_columns}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}_first}}`}</code>
          </p>
        )}
      </div>

      <div className="space-y-2">
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={hasHeader}
            onChange={(e) => set('hasHeader', e.target.checked)}
            className="rounded border-gray-300"
          />
          First row is header (column names)
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={normalizeWhitespace}
            onChange={(e) => set('normalizeWhitespace', e.target.checked)}
            className="rounded border-gray-300"
          />
          Normalize whitespace before parsing
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={failIfNoTable}
            onChange={(e) => set('failIfNoTable', e.target.checked)}
            className="rounded border-gray-300"
          />
          Fail if no table is found
        </label>
      </div>

      <div className="rounded-lg border border-rose-200 bg-rose-50/60 px-2.5 py-2 text-xs text-rose-900 space-y-1">
        <p>Text-based PDFs only — selectable text with extractable rows.</p>
        <p>Best for exported report PDFs and invoice line items. OCR and scanned PDFs are not supported.</p>
      </div>
    </div>
  );
}
