import type { EditorProps } from './types';
import { MailFieldMsg, MailMessageIdField, MailSourceFolderField } from './mailCommon';

export default function MailMoveEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const targetFolder = String(config.targetFolder ?? '');

  return (
    <div className="space-y-3">
      <MailMessageIdField {...{ config, onChange, onFocusField, fieldErrors }} />
      <MailSourceFolderField {...{ config, onChange, onFocusField, fieldErrors }} />

      <div>
        <label className="label">Target Folder *</label>
        <input
          value={targetFolder}
          onChange={(e) => onChange({ ...config, targetFolder: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'targetFolder')}
          className={`input font-mono text-xs ${fieldErrors?.targetFolder ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="Processed"
          spellCheck={false}
        />
        <MailFieldMsg errors={fieldErrors} field="targetFolder" />
        {!fieldErrors?.targetFolder && (
          <p className="text-xs text-gray-400 mt-1">
            Examples: Processed, Archive, Failed, Completed, Inbox/Processed
          </p>
        )}
      </div>

      <div className="rounded-md bg-blue-50 border border-blue-100 px-3 py-2 text-xs text-blue-800">
        Output: <code className="bg-white/70 px-0.5 rounded">movedFolder</code>
      </div>
    </div>
  );
}
