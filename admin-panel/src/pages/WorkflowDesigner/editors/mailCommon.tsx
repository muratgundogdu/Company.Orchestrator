import type { EditorProps } from './types';

export function MailFieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export function MailMessageIdField({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const messageId = String(config.messageId ?? '');

  return (
    <div>
      <label className="label">Message ID *</label>
      <input
        value={messageId}
        onChange={(e) => onChange({ ...config, messageId: e.target.value })}
        onFocus={(e) => onFocusField(e.currentTarget, 'messageId')}
        className={`input font-mono text-xs ${fieldErrors?.messageId ? 'border-red-400 focus:ring-red-400' : ''}`}
        placeholder="{{selectedMessageId}}"
        spellCheck={false}
      />
      <MailFieldMsg errors={fieldErrors} field="messageId" />
      {!fieldErrors?.messageId && (
        <p className="text-xs text-gray-400 mt-1">
          IMAP UID from mail.read-attachments. Use the variable picker for{' '}
          <code className="bg-gray-100 px-0.5 rounded">selectedMessageId</code>.
        </p>
      )}
    </div>
  );
}

export function MailSourceFolderField({ config, onChange, onFocusField }: EditorProps) {
  const sourceFolder = String(config.sourceFolder ?? config.folder ?? '');

  return (
    <div>
      <label className="label">Source Folder</label>
      <input
        value={sourceFolder}
        onChange={(e) => onChange({ ...config, sourceFolder: e.target.value })}
        onFocus={(e) => onFocusField(e.currentTarget, 'sourceFolder')}
        className="input font-mono text-xs"
        placeholder="{{selectedMessageFolder}} or INBOX"
        spellCheck={false}
      />
      <p className="text-xs text-gray-400 mt-1">
        Optional — defaults to the folder from mail.read-attachments.
      </p>
    </div>
  );
}

export function MailAddressField({
  label,
  field,
  required,
  placeholder,
  config,
  onChange,
  onFocusField,
  fieldErrors,
}: EditorProps & { label: string; field: string; required?: boolean; placeholder?: string }) {
  const value = String(config[field] ?? '');

  return (
    <div>
      <label className="label">{label}{required ? ' *' : ''}</label>
      <input
        value={value}
        onChange={(e) => onChange({ ...config, [field]: e.target.value })}
        onFocus={(e) => onFocusField(e.currentTarget, field)}
        className={`input text-sm ${fieldErrors?.[field] ? 'border-red-400 focus:ring-red-400' : ''}`}
        placeholder={placeholder ?? 'user@example.com'}
        type="email"
      />
      <MailFieldMsg errors={fieldErrors} field={field} />
    </div>
  );
}

export function MailAttachmentsField({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const attachments = String(config.attachments ?? '');

  return (
    <div>
      <label className="label">Attachments</label>
      <input
        value={attachments}
        onChange={(e) => onChange({ ...config, attachments: e.target.value })}
        onFocus={(e) => onFocusField(e.currentTarget, 'attachments')}
        className={`input font-mono text-xs ${fieldErrors?.attachments ? 'border-amber-400 focus:ring-amber-400' : ''}`}
        placeholder="report.xlsx or {{mailArtifacts_0}}"
        spellCheck={false}
      />
      {fieldErrors?.attachments
        ? <p className="text-xs text-amber-600 mt-1">⚠ {fieldErrors.attachments}</p>
        : <p className="text-xs text-gray-400 mt-1">
            Comma-separated artifact names from upstream steps.
          </p>
      }
    </div>
  );
}
