import type { EditorProps } from './types';
import { MailMessageIdField, MailSourceFolderField } from './mailCommon';

export default function MailMarkEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const isRead = config.isRead !== false && config.isRead !== 'false';

  return (
    <div className="space-y-3">
      <MailMessageIdField {...{ config, onChange, onFocusField, fieldErrors }} />
      <MailSourceFolderField {...{ config, onChange, onFocusField, fieldErrors }} />

      <div>
        <label className="label">Read State *</label>
        <select
          value={isRead ? 'read' : 'unread'}
          onChange={(e) => onChange({ ...config, isRead: e.target.value === 'read' })}
          className="input text-xs"
        >
          <option value="read">Read</option>
          <option value="unread">Unread</option>
        </select>
      </div>

      <div className="rounded-md bg-blue-50 border border-blue-100 px-3 py-2 text-xs text-blue-800">
        Output: <code className="bg-white/70 px-0.5 rounded">mailReadState</code>
      </div>
    </div>
  );
}
