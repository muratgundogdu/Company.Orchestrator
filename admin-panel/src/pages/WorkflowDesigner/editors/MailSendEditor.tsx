import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function MailSendEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const to          = String(config.to          ?? '');
  const cc          = String(config.cc          ?? '');
  const bcc         = String(config.bcc         ?? '');
  const subject     = String(config.subject     ?? '');
  const body        = String(config.body        ?? '');
  const isHtml      = config.isHtml === true || config.isHtml === 'true';
  const attachments = String(config.attachments ?? '');

  function set(key: string, value: unknown) {
    onChange({ ...config, [key]: value });
  }

  return (
    <div className="space-y-3">

      {/* To */}
      <div>
        <label className="label">To *</label>
        <input
          value={to}
          onChange={(e) => set('to', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'to')}
          className={`input text-sm ${fieldErrors?.to ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="recipient@example.com"
          type="email"
        />
        <FieldMsg errors={fieldErrors} field="to" />
      </div>

      {/* CC */}
      <div>
        <label className="label">CC</label>
        <input
          value={cc}
          onChange={(e) => set('cc', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'cc')}
          className="input text-sm"
          placeholder="cc@example.com"
          type="email"
        />
      </div>

      {/* BCC */}
      <div>
        <label className="label">BCC</label>
        <input
          value={bcc}
          onChange={(e) => set('bcc', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'bcc')}
          className="input text-sm"
          placeholder="bcc@example.com"
          type="email"
        />
      </div>

      {/* Subject */}
      <div>
        <label className="label">Subject *</label>
        <input
          value={subject}
          onChange={(e) => set('subject', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'subject')}
          className={`input text-sm ${fieldErrors?.subject ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="AlterOne Process Completed"
        />
        <FieldMsg errors={fieldErrors} field="subject" />
      </div>

      {/* Body */}
      <div>
        <div className="flex items-center justify-between mb-1">
          <label className="label mb-0">Body</label>
          <label className="flex items-center gap-1.5 cursor-pointer select-none">
            <input
              type="checkbox"
              checked={isHtml}
              onChange={(e) => set('isHtml', e.target.checked)}
              className="h-3.5 w-3.5 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span className="text-xs text-gray-500 font-medium">HTML</span>
          </label>
        </div>
        <textarea
          value={body}
          onChange={(e) => set('body', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'body')}
          rows={5}
          className="input text-sm resize-y"
          placeholder={isHtml ? '<p>The workflow completed successfully.</p>' : 'The workflow completed successfully.'}
          spellCheck={false}
        />
        {isHtml && (
          <p className="text-xs text-gray-400 mt-1">
            HTML is enabled — use tags like <code className="bg-gray-100 px-0.5 rounded">&lt;b&gt;</code>, <code className="bg-gray-100 px-0.5 rounded">&lt;a&gt;</code>.
          </p>
        )}
      </div>

      {/* Attachments */}
      <div>
        <label className="label">Attachments</label>
        <input
          value={attachments}
          onChange={(e) => set('attachments', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'attachments')}
          className={`input font-mono text-xs ${fieldErrors?.attachments ? 'border-amber-400 focus:ring-amber-400' : ''}`}
          placeholder="transformed-excel.xlsx or {{mailArtifacts_0}}"
          spellCheck={false}
        />
        {fieldErrors?.attachments
          ? <p className="text-xs text-amber-600 mt-1">⚠ {fieldErrors.attachments}</p>
          : <p className="text-xs text-gray-400 mt-1">
              Artifact name or variable. Use the variable picker to insert upstream outputs.
            </p>
        }
      </div>

    </div>
  );
}
