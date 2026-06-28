import type { EditorProps } from './types';
import EditableNumberInput from './EditableNumberInput';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function MailReadEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const folder                   = String(config.folder                   ?? 'INBOX');
  const subjectContains          = String(config.subjectContains          ?? '');
  const fromContains             = String(config.fromContains             ?? '');
  const unreadOnly               = config.unreadOnly !== false && config.unreadOnly !== 'false';
  const latestOnly               = config.latestOnly !== false && config.latestOnly !== 'false';
  const sortOrder                = String(config.sortOrder                ?? 'newest');
  const attachmentNameContains   = String(config.attachmentNameContains   ?? '');
  const attachmentPattern        = String(config.attachmentPattern        ?? '*.xlsx');
  const markAsReadAfterProcessing = config.markAsReadAfterProcessing === true || config.markAsReadAfterProcessing === 'true';
  const artifactPrefix           = String(config.artifactPrefix           ?? 'mail-file');
  const outputVariable           = String(config.outputVariable           ?? 'mailArtifacts');

  return (
    <div className="space-y-3">
      <div>
        <label className="label">Folder *</label>
        <input
          value={folder}
          onChange={(e) => onChange({ ...config, folder: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'folder')}
          className={`input ${fieldErrors?.folder ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="INBOX"
        />
        <FieldMsg errors={fieldErrors} field="folder" />
      </div>

      <div>
        <label className="label">Subject Contains</label>
        <input
          value={subjectContains}
          onChange={(e) => onChange({ ...config, subjectContains: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'subjectContains')}
          className="input"
          placeholder="e.g. RAPOR TEST"
        />
        <p className="text-xs text-gray-400 mt-1">Leave empty to match all subjects.</p>
      </div>

      <div>
        <label className="label">From Contains</label>
        <input
          value={fromContains}
          onChange={(e) => onChange({ ...config, fromContains: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'fromContains')}
          className="input"
          placeholder="e.g. reports@company.com"
        />
        <p className="text-xs text-gray-400 mt-1">Filter by sender address or display name.</p>
      </div>

      <div className="flex flex-col gap-2">
        <label className="flex items-center gap-2 cursor-pointer select-none">
          <input
            type="checkbox"
            checked={unreadOnly}
            onChange={(e) => onChange({ ...config, unreadOnly: e.target.checked })}
            className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
          />
          <span className="text-sm font-medium text-gray-700">Unread Only</span>
        </label>

        <label className="flex items-center gap-2 cursor-pointer select-none">
          <input
            type="checkbox"
            checked={latestOnly}
            onChange={(e) => onChange({ ...config, latestOnly: e.target.checked })}
            className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
          />
          <span className="text-sm font-medium text-gray-700">Latest Only</span>
        </label>
        <p className="text-xs text-gray-400 ml-6">When enabled, only the newest matching email is processed.</p>
      </div>

      <div>
        <label className="label">Max Count</label>
        <EditableNumberInput
          min={1}
          max={100}
          fallback={1}
          value={config.maxCount}
          onValueChange={(maxCount) => onChange({ ...config, maxCount })}
          onFocus={(e) => onFocusField(e.currentTarget, 'maxCount')}
          className={`input w-24 ${fieldErrors?.maxCount ? 'border-red-400 focus:ring-red-400' : ''} ${latestOnly ? 'opacity-60' : ''}`}
          aria-describedby="max-count-hint"
        />
        <FieldMsg errors={fieldErrors} field="maxCount" />
        <p id="max-count-hint" className="text-xs text-gray-400 mt-1">
          {latestOnly
            ? 'Ignored when Latest Only is enabled (always 1 message). Does not limit attachments per message.'
            : 'Maximum number of matching emails to process.'}
        </p>
      </div>

      <div>
        <label className="label">Max Attachment Count</label>
        <input
          type="number"
          min={1}
          value={config.maxAttachmentCount != null ? String(config.maxAttachmentCount) : ''}
          onChange={(e) => {
            const raw = e.target.value;
            if (raw === '') {
              const next = { ...config };
              delete next.maxAttachmentCount;
              onChange(next);
            } else {
              onChange({ ...config, maxAttachmentCount: parseInt(raw, 10) });
            }
          }}
          onFocus={(e) => onFocusField(e.currentTarget, 'maxAttachmentCount')}
          className={`input w-24 ${fieldErrors?.maxAttachmentCount ? 'border-red-400 focus:ring-red-400' : ''}`}
          aria-describedby="max-attachment-count-hint"
        />
        <FieldMsg errors={fieldErrors} field="maxAttachmentCount" />
        <p id="max-attachment-count-hint" className="text-xs text-gray-400 mt-1">
          Leave empty to download all matching attachments from each selected email.
        </p>
      </div>

      <div>
        <label className="label">Sort Order</label>
        <select
          value={sortOrder}
          onChange={(e) => onChange({ ...config, sortOrder: e.target.value })}
          className="input"
        >
          <option value="newest">Newest first</option>
          <option value="oldest">Oldest first</option>
        </select>
      </div>

      <div>
        <label className="label">Attachment Name Contains</label>
        <input
          value={attachmentNameContains}
          onChange={(e) => onChange({ ...config, attachmentNameContains: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'attachmentNameContains')}
          className="input font-mono text-xs"
          placeholder="e.g. RAPOR"
          spellCheck={false}
        />
        <p className="text-xs text-gray-400 mt-1">Only download attachments whose file name contains this text.</p>
      </div>

      <div>
        <label className="label">Attachment Pattern</label>
        <input
          value={attachmentPattern}
          onChange={(e) => onChange({ ...config, attachmentPattern: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'attachmentPattern')}
          className="input font-mono text-xs"
          placeholder="*.xlsx"
          spellCheck={false}
        />
        <p className="text-xs text-gray-400 mt-1">Wildcard pattern, e.g. <code className="bg-gray-100 px-0.5 rounded">*.xlsx</code> or <code className="bg-gray-100 px-0.5 rounded">file*.xlsx</code>. Leave empty to allow all.</p>
      </div>

      <label className="flex items-center gap-2 cursor-pointer select-none">
        <input
          type="checkbox"
          checked={markAsReadAfterProcessing}
          onChange={(e) => onChange({ ...config, markAsReadAfterProcessing: e.target.checked })}
          className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
        />
        <span className="text-sm font-medium text-gray-700">Mark As Read After Processing</span>
      </label>

      <div>
        <label className="label">Artifact Prefix</label>
        <input
          value={artifactPrefix}
          onChange={(e) => onChange({ ...config, artifactPrefix: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'artifactPrefix')}
          className="input font-mono text-xs"
          placeholder="mail-file"
          spellCheck={false}
        />
      </div>

      <div>
        <label className="label">Output Variable *</label>
        <input
          value={outputVariable}
          onChange={(e) => onChange({ ...config, outputVariable: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
          className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="mailArtifacts"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputVariable" />
        {!fieldErrors?.outputVariable && (
          <p className="text-xs text-gray-400 mt-1">
            Referenced downstream as{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}}}`}</code>,{' '}
            <code className="bg-gray-100 px-0.5 rounded">{`{{${outputVariable}_0}}`}</code>, …
          </p>
        )}
      </div>
    </div>
  );
}
