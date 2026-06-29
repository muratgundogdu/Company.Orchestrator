import { useEffect, useState } from 'react';
import { FunctionSquare } from 'lucide-react';
import { normalizeExpressionText } from './transformColumnExpression';

const IC = 'input text-xs';

interface TransformColumnExpressionFieldProps {
  value: string;
  defaultValue?: string;
  onChange: (value: string) => void;
  onFocus?: (e: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) => void;
  hasError?: boolean;
}

function TransformColumnExpressionHelp() {
  return (
    <div className="text-xs text-gray-500 space-y-2 mt-1.5">
      <dl className="space-y-1 rounded-md border border-gray-100 bg-gray-50/80 px-2.5 py-2">
        <div className="grid grid-cols-[auto_1fr] gap-x-2 gap-y-1 items-baseline">
          <dt className="font-mono text-gray-800 font-medium">value</dt>
          <dd className="text-gray-600">current cell</dd>
          <dt className="font-mono text-gray-800 font-medium">row[&quot;Column&quot;]</dt>
          <dd className="text-gray-600">same-row value by header or letter</dd>
          <dt className="font-mono text-gray-800 font-medium">variables.name</dt>
          <dd className="text-gray-600">workflow variable</dd>
        </div>
      </dl>
      <div>
        <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-500 mb-1">Examples</p>
        <ul className="space-y-1 font-mono text-[11px] text-gray-600 leading-relaxed">
          <li>
            <code className="bg-gray-100 px-1 py-0.5 rounded break-all">
              toNumber(value) * toNumber(variables.usdRateText)
            </code>
          </li>
          <li>
            <code className="bg-gray-100 px-1 py-0.5 rounded break-all">
              toNumber(row[&quot;Fiyat&quot;]) * toNumber(variables.eurRateText)
            </code>
          </li>
          <li>
            <code className="bg-gray-100 px-1 py-0.5 rounded">row[&quot;Urun&quot;]</code>
            <span className="text-gray-400 font-sans"> · </span>
            <code className="bg-gray-100 px-1 py-0.5 rounded">toNumber(value) / 100</code>
            <span className="text-gray-400 font-sans"> · </span>
            <code className="bg-gray-100 px-1 py-0.5 rounded">trim(value)</code>
          </li>
        </ul>
      </div>
    </div>
  );
}

interface ExpressionEditorModalProps {
  value: string;
  onSave: (value: string) => void;
  onClose: () => void;
}

function ExpressionEditorModal({ value, onSave, onClose }: ExpressionEditorModalProps) {
  const [draft, setDraft] = useState(value);

  useEffect(() => {
    setDraft(value);
  }, [value]);

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose();
    }
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [onClose]);

  function handleSave() {
    onSave(normalizeExpressionText(draft));
    onClose();
  }

  return (
    <div
      className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50 p-4"
      onClick={onClose}
      role="presentation"
    >
      <div
        className="bg-white rounded-xl shadow-2xl w-full max-w-3xl overflow-hidden"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="expression-editor-title"
      >
        <div className="px-5 py-4 border-b border-gray-200">
          <h2 id="expression-editor-title" className="text-lg font-semibold text-gray-900">
            Expression Editor
          </h2>
          <p className="text-sm text-gray-500 mt-1">
            Multiline editing is supported; saved value is stored as a single expression string.
          </p>
        </div>

        <div className="px-5 py-4">
          <textarea
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            rows={14}
            spellCheck={false}
            autoFocus
            className="input text-sm font-mono w-full resize-y min-h-[280px]"
            data-testid="expression-editor-textarea"
          />
          <TransformColumnExpressionHelp />
        </div>

        <div className="px-5 py-4 border-t border-gray-100 flex justify-end gap-2 bg-gray-50">
          <button type="button" onClick={onClose} className="btn btn-secondary">
            Cancel
          </button>
          <button type="button" onClick={handleSave} className="btn btn-primary" data-testid="expression-editor-save">
            Save
          </button>
        </div>
      </div>
    </div>
  );
}

export default function TransformColumnExpressionField({
  value,
  defaultValue = 'toNumber(value) / 100',
  onChange,
  onFocus,
  hasError = false,
}: TransformColumnExpressionFieldProps) {
  const [modalOpen, setModalOpen] = useState(false);
  const displayValue = value || defaultValue;
  const errorClass = hasError ? ' border-red-400 focus:ring-red-400' : '';

  return (
    <>
      <div className="flex gap-1.5 items-center">
        <input
          value={displayValue}
          onChange={(e) => onChange(e.target.value)}
          onFocus={onFocus}
          className={`${IC} font-mono flex-1 min-w-0${errorClass}`}
          spellCheck={false}
          data-testid="expression-inline-input"
        />
        <button
          type="button"
          title="Expand expression editor"
          aria-label="Expand expression editor"
          onClick={() => setModalOpen(true)}
          className="shrink-0 h-[30px] px-2 rounded border border-gray-300 bg-white text-gray-600 hover:bg-gray-50 hover:text-indigo-600 hover:border-indigo-300 transition-colors flex items-center gap-1 text-[11px] font-semibold"
          data-testid="expression-expand-button"
        >
          <FunctionSquare size={14} aria-hidden />
          <span>fx</span>
        </button>
      </div>

      {!modalOpen && <TransformColumnExpressionHelp />}

      {modalOpen && (
        <ExpressionEditorModal
          value={displayValue}
          onSave={onChange}
          onClose={() => setModalOpen(false)}
        />
      )}
    </>
  );
}
