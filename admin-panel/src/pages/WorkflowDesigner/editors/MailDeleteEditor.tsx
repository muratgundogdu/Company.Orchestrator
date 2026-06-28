import type { EditorProps } from './types';
import { MailMessageIdField, MailSourceFolderField } from './mailCommon';

export default function MailDeleteEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const permanent = config.permanent === true || config.permanent === 'true';

  return (
    <div className="space-y-3">
      <MailMessageIdField {...{ config, onChange, onFocusField, fieldErrors }} />
      <MailSourceFolderField {...{ config, onChange, onFocusField, fieldErrors }} />

      <label className="flex items-center gap-1.5 cursor-pointer select-none">
        <input
          type="checkbox"
          checked={permanent}
          onChange={(e) => onChange({ ...config, permanent: e.target.checked })}
          className="h-3.5 w-3.5 rounded border-gray-300 text-red-600 focus:ring-red-500"
        />
        <span className="text-xs text-gray-600 font-medium">Permanent Delete</span>
      </label>
      <p className="text-xs text-gray-400 -mt-1">
        When unchecked, moves the message to Deleted Items. Permanent delete requires provider support.
      </p>

      <div className="rounded-md bg-blue-50 border border-blue-100 px-3 py-2 text-xs text-blue-800">
        Output: <code className="bg-white/70 px-0.5 rounded">mailDeleted</code>
      </div>
    </div>
  );
}
