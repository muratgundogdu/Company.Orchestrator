import type { EditorProps } from './types';
import {
  MailAddressField,
  MailMessageIdField,
  MailSourceFolderField,
} from './mailCommon';

export default function MailForwardEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const body = String(config.body ?? '');
  const includeAttachments = config.includeAttachments !== false && config.includeAttachments !== 'false';
  const isHtml = config.isHtml === true || config.isHtml === 'true';

  function set(key: string, value: unknown) {
    onChange({ ...config, [key]: value });
  }

  return (
    <div className="space-y-3">
      <MailMessageIdField {...{ config, onChange, onFocusField, fieldErrors }} />
      <MailSourceFolderField {...{ config, onChange, onFocusField, fieldErrors }} />

      <MailAddressField
        label="To"
        field="to"
        required
        placeholder="team@company.com"
        {...{ config, onChange, onFocusField, fieldErrors }}
      />
      <MailAddressField
        label="CC"
        field="cc"
        placeholder="cc@company.com"
        {...{ config, onChange, onFocusField, fieldErrors }}
      />
      <MailAddressField
        label="BCC"
        field="bcc"
        placeholder="bcc@company.com"
        {...{ config, onChange, onFocusField, fieldErrors }}
      />

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
          rows={4}
          className="input text-sm resize-y"
          placeholder="Please review."
          spellCheck={false}
        />
      </div>

      <label className="flex items-center gap-1.5 cursor-pointer select-none">
        <input
          type="checkbox"
          checked={includeAttachments}
          onChange={(e) => set('includeAttachments', e.target.checked)}
          className="h-3.5 w-3.5 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
        />
        <span className="text-xs text-gray-600 font-medium">Include Original Attachments</span>
      </label>

      <div className="rounded-md bg-blue-50 border border-blue-100 px-3 py-2 text-xs text-blue-800">
        Output: <code className="bg-white/70 px-0.5 rounded">forwardMessageId</code>
      </div>
    </div>
  );
}
