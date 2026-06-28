import type { EditorProps } from './types';
import {
  MailAttachmentsField,
  MailMessageIdField,
  MailSourceFolderField,
} from './mailCommon';

export default function MailReplyEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const body = String(config.body ?? '');
  const replyAll = config.replyAll === true || config.replyAll === 'true';
  const includeOriginalBody = config.includeOriginalBody !== false && config.includeOriginalBody !== 'false';
  const isHtml = config.isHtml === true || config.isHtml === 'true';

  function set(key: string, value: unknown) {
    onChange({ ...config, [key]: value });
  }

  return (
    <div className="space-y-3">
      <MailMessageIdField {...{ config, onChange, onFocusField, fieldErrors }} />
      <MailSourceFolderField {...{ config, onChange, onFocusField, fieldErrors }} />

      <div>
        <label className="label">Body</label>
        <textarea
          value={body}
          onChange={(e) => set('body', e.target.value)}
          onFocus={(e) => onFocusField(e.currentTarget, 'body')}
          rows={5}
          className="input text-sm resize-y"
          placeholder="Your request has been processed."
          spellCheck={false}
        />
      </div>

      <div className="flex flex-wrap gap-4">
        <label className="flex items-center gap-1.5 cursor-pointer select-none">
          <input
            type="checkbox"
            checked={replyAll}
            onChange={(e) => set('replyAll', e.target.checked)}
            className="h-3.5 w-3.5 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
          />
          <span className="text-xs text-gray-600 font-medium">Reply All</span>
        </label>

        <label className="flex items-center gap-1.5 cursor-pointer select-none">
          <input
            type="checkbox"
            checked={includeOriginalBody}
            onChange={(e) => set('includeOriginalBody', e.target.checked)}
            className="h-3.5 w-3.5 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
          />
          <span className="text-xs text-gray-600 font-medium">Include Original Body</span>
        </label>

        <label className="flex items-center gap-1.5 cursor-pointer select-none">
          <input
            type="checkbox"
            checked={isHtml}
            onChange={(e) => set('isHtml', e.target.checked)}
            className="h-3.5 w-3.5 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
          />
          <span className="text-xs text-gray-600 font-medium">HTML</span>
        </label>
      </div>

      <MailAttachmentsField {...{ config, onChange, onFocusField, fieldErrors }} />

      <div className="rounded-md bg-blue-50 border border-blue-100 px-3 py-2 text-xs text-blue-800">
        Outputs: <code className="bg-white/70 px-0.5 rounded">replyMessageId</code>,{' '}
        <code className="bg-white/70 px-0.5 rounded">replyConversationId</code>
      </div>
    </div>
  );
}
