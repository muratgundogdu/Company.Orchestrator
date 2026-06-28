import type { EditorProps } from './types';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function MailGetBodyEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const bodyType       = String(config.bodyType ?? 'text');
  const outputVariable = String(config.outputVariable ?? 'mailBody');
  const sourceVariable = String(config.sourceVariable ?? 'receivedMails');
  const messageId      = String(config.messageId ?? '');
  const folder         = String(config.folder ?? '');

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Body Type *</label>
        <select
          value={bodyType}
          onChange={(e) => onChange({ ...config, bodyType: e.target.value })}
          className={`input text-xs ${fieldErrors?.bodyType ? 'border-red-400 focus:ring-red-400' : ''}`}
        >
          <option value="text">Text</option>
          <option value="html">HTML</option>
        </select>
        <FieldMsg errors={fieldErrors} field="bodyType" />
      </div>

      <div>
        <label className="label">Output Variable *</label>
        <input
          value={outputVariable}
          onChange={(e) => onChange({ ...config, outputVariable: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
          className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="mailBody"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputVariable" />
        {!fieldErrors?.outputVariable && (
          <p className="text-xs text-gray-400 mt-1">
            Stored as <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}}}`}</code>
          </p>
        )}
      </div>

      <div>
        <label className="label">Source Variable</label>
        <input
          value={sourceVariable}
          onChange={(e) => onChange({ ...config, sourceVariable: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'sourceVariable')}
          className="input font-mono text-xs"
          placeholder="receivedMails"
          spellCheck={false}
        />
        <p className="text-xs text-gray-400 mt-1">
          JSON from mail.receive, or leave empty to use selectedMessageId from mail.read-attachments.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Message ID</label>
          <input
            value={messageId}
            onChange={(e) => onChange({ ...config, messageId: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'messageId')}
            className="input font-mono text-xs"
            placeholder="optional"
            spellCheck={false}
          />
        </div>
        <div>
          <label className="label">Folder</label>
          <input
            value={folder}
            onChange={(e) => onChange({ ...config, folder: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'folder')}
            className="input font-mono text-xs"
            placeholder="INBOX"
            spellCheck={false}
          />
        </div>
      </div>
    </div>
  );
}
