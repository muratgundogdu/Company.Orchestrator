import { Plus, Trash2, KeyRound } from 'lucide-react';
import type { EditorProps } from './types';
import EditableNumberInput from './EditableNumberInput';
import { useCredentials, credentialOptions } from '../../../hooks/useCredentials';

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

type KeyValuePair = { key: string; value: string };

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'] as const;

function parseKeyValueMap(value: unknown): KeyValuePair[] {
  if (value && typeof value === 'object' && !Array.isArray(value)) {
    const entries = Object.entries(value as Record<string, unknown>);
    if (entries.length === 0) return [{ key: '', value: '' }];
    return entries.map(([key, val]) => ({ key, value: String(val ?? '') }));
  }
  return [{ key: '', value: '' }];
}

function keyValueMapToObject(pairs: KeyValuePair[]): Record<string, string> {
  const result: Record<string, string> = {};
  for (const pair of pairs) {
    const key = pair.key.trim();
    if (key) result[key] = pair.value;
  }
  return result;
}

function KeyValueGrid({
  label,
  pairs,
  onChange,
  fieldErrors,
  keyPlaceholder,
  valuePlaceholder,
  fieldPrefix,
  onFocusField,
}: {
  label: string;
  pairs: KeyValuePair[];
  onChange: (next: KeyValuePair[]) => void;
  fieldErrors?: Record<string, string>;
  keyPlaceholder: string;
  valuePlaceholder: string;
  fieldPrefix: string;
  onFocusField: EditorProps['onFocusField'];
}) {
  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <p className="text-xs font-medium text-gray-600">{label}</p>
        <button
          type="button"
          onClick={() => onChange([...pairs, { key: '', value: '' }])}
          className="inline-flex items-center gap-1 text-xs text-brand-primary hover:underline"
        >
          <Plus className="w-3 h-3" /> Add row
        </button>
      </div>
      {pairs.map((pair, idx) => (
        <div key={idx} className="grid grid-cols-[1fr_1fr_auto] gap-2 items-center">
          <input
            value={pair.key}
            onChange={(e) => {
              const next = [...pairs];
              next[idx] = { ...next[idx], key: e.target.value };
              onChange(next);
            }}
            onFocus={(e) => onFocusField(e.currentTarget, `${fieldPrefix}.${idx}.key`)}
            className={`input font-mono text-xs ${fieldErrors?.[`${fieldPrefix}.${idx}.key`] ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder={keyPlaceholder}
            spellCheck={false}
          />
          <input
            value={pair.value}
            onChange={(e) => {
              const next = [...pairs];
              next[idx] = { ...next[idx], value: e.target.value };
              onChange(next);
            }}
            onFocus={(e) => onFocusField(e.currentTarget, `${fieldPrefix}.${idx}.value`)}
            className={`input font-mono text-xs ${fieldErrors?.[`${fieldPrefix}.${idx}.value`] ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder={valuePlaceholder}
            spellCheck={false}
          />
          <button
            type="button"
            onClick={() => onChange(pairs.filter((_, i) => i !== idx))}
            className="p-1 text-gray-400 hover:text-red-600"
            title="Remove row"
            disabled={pairs.length <= 1 && !pair.key && !pair.value}
          >
            <Trash2 className="w-3.5 h-3.5" />
          </button>
        </div>
      ))}
    </div>
  );
}

export default function HttpRequestEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const { credentials: bearerCredentials } = useCredentials('BearerToken');
  const { credentials: apiKeyCredentials } = useCredentials('ApiKey');
  const bearerOptions = credentialOptions(bearerCredentials);
  const apiKeyOptions = credentialOptions(apiKeyCredentials);

  const url                    = String(config.url ?? 'https://jsonplaceholder.typicode.com/users');
  const method                 = String(config.method ?? 'GET').toUpperCase();
  const body                   = String(config.body ?? '');
  const contentType            = String(config.contentType ?? 'application/json');
  const timeoutSeconds         = Number(config.timeoutSeconds ?? 60);
  const outputVariable         = String(config.outputVariable ?? 'apiResult');
  const failOnNonSuccessStatus = config.failOnNonSuccessStatus !== false && config.failOnNonSuccessStatus !== 'false';
  const bearerTokenCredentialName = String(config.bearerTokenCredentialName ?? '');
  const apiKeyCredentialName      = String(config.apiKeyCredentialName ?? '');
  const apiKeyHeaderName          = String(config.apiKeyHeaderName ?? 'X-Api-Key');
  const headerPairs            = parseKeyValueMap(config.headers);
  const queryPairs             = parseKeyValueMap(config.queryParameters);

  const showBody = method !== 'GET';

  return (
    <div className="space-y-3">
      <div>
        <label className="label">URL *</label>
        <input
          value={url}
          onChange={(e) => onChange({ ...config, url: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'url')}
          className={`input font-mono text-xs ${fieldErrors?.url ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="https://api.company.com/customers"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="url" />
        <p className="text-xs text-gray-400 mt-1">Supports {'{{variable}}'} interpolation in the URL path.</p>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Method *</label>
          <select
            value={method}
            onChange={(e) => onChange({ ...config, method: e.target.value })}
            className={`input text-xs ${fieldErrors?.method ? 'border-red-400 focus:ring-red-400' : ''}`}
          >
            {HTTP_METHODS.map((m) => (
              <option key={m} value={m}>{m}</option>
            ))}
          </select>
          <FieldMsg errors={fieldErrors} field="method" />
        </div>
        <div>
          <label className="label">Output Variable *</label>
          <input
            value={outputVariable}
            onChange={(e) => onChange({ ...config, outputVariable: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'outputVariable')}
            className={`input font-mono text-xs ${fieldErrors?.outputVariable ? 'border-red-400 focus:ring-red-400' : ''}`}
            placeholder="apiResult"
            spellCheck={false}
          />
          <FieldMsg errors={fieldErrors} field="outputVariable" />
        </div>
      </div>

      <div className="rounded-lg border border-violet-200 bg-violet-50/60 px-2.5 py-2 space-y-3">
        <div className="flex items-center gap-2 text-xs font-medium text-violet-900">
          <KeyRound className="w-3.5 h-3.5" /> Credential Vault
        </div>
        <div className="grid grid-cols-2 gap-2">
          <div>
            <label className="label">Bearer Token Credential</label>
            <select
              value={bearerTokenCredentialName}
              onChange={(e) => onChange({ ...config, bearerTokenCredentialName: e.target.value })}
              className="input text-xs"
            >
              <option value="">— None —</option>
              {bearerOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>{opt.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="label">API Key Credential</label>
            <select
              value={apiKeyCredentialName}
              onChange={(e) => onChange({ ...config, apiKeyCredentialName: e.target.value })}
              className="input text-xs"
            >
              <option value="">— None —</option>
              {apiKeyOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>{opt.label}</option>
              ))}
            </select>
          </div>
        </div>
        <div>
          <label className="label">API Key Header Name</label>
          <input
            value={apiKeyHeaderName}
            onChange={(e) => onChange({ ...config, apiKeyHeaderName: e.target.value })}
            onFocus={(e) => onFocusField(e.currentTarget, 'apiKeyHeaderName')}
            className="input font-mono text-xs"
            placeholder="X-Api-Key"
            spellCheck={false}
          />
        </div>
        <p className="text-xs text-violet-800">
          Credentials are resolved at runtime. Workflow JSON stores only credential names, never secret values.
        </p>
      </div>

      <KeyValueGrid
        label="Headers"
        pairs={headerPairs}
        onChange={(next) => onChange({ ...config, headers: keyValueMapToObject(next) })}
        fieldErrors={fieldErrors}
        keyPlaceholder="Authorization"
        valuePlaceholder="Bearer {{token}}"
        fieldPrefix="headers"
        onFocusField={onFocusField}
      />

      <KeyValueGrid
        label="Query Parameters"
        pairs={queryPairs}
        onChange={(next) => onChange({ ...config, queryParameters: keyValueMapToObject(next) })}
        fieldErrors={fieldErrors}
        keyPlaceholder="customerNo"
        valuePlaceholder="{{currentRow.CustomerNo}}"
        fieldPrefix="queryParameters"
        onFocusField={onFocusField}
      />

      {showBody && (
        <>
          <div>
            <label className="label">Body</label>
            <textarea
              value={body}
              onChange={(e) => onChange({ ...config, body: e.target.value })}
              onFocus={(e) => onFocusField(e.currentTarget, 'body')}
              className="input font-mono text-xs min-h-[80px]"
              placeholder='{"customerNo": "{{currentRow.CustomerNo}}"}'
              spellCheck={false}
            />
            <p className="text-xs text-gray-400 mt-1">Supports {'{{variable}}'} interpolation.</p>
          </div>

          <div>
            <label className="label">Content Type</label>
            <input
              value={contentType}
              onChange={(e) => onChange({ ...config, contentType: e.target.value })}
              onFocus={(e) => onFocusField(e.currentTarget, 'contentType')}
              className="input font-mono text-xs"
              placeholder="application/json"
              spellCheck={false}
            />
          </div>
        </>
      )}

      <div>
        <label className="label">Timeout Seconds *</label>
        <EditableNumberInput
          value={timeoutSeconds}
          min={1}
          fallback={60}
          onValueChange={(timeoutSeconds) => onChange({ ...config, timeoutSeconds })}
          onFocus={(e) => onFocusField(e.currentTarget, 'timeoutSeconds')}
          className={`input text-xs ${fieldErrors?.timeoutSeconds ? 'border-red-400 focus:ring-red-400' : ''}`}
        />
        <FieldMsg errors={fieldErrors} field="timeoutSeconds" />
      </div>

      <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
        <input
          type="checkbox"
          checked={failOnNonSuccessStatus}
          onChange={(e) => onChange({ ...config, failOnNonSuccessStatus: e.target.checked })}
          className="rounded border-gray-300"
        />
        Fail on non-success HTTP status (4xx / 5xx)
      </label>

      <div className="rounded-lg border border-sky-200 bg-sky-50/60 px-2.5 py-2 text-xs text-sky-900 space-y-1">
        <p>Example: GET https://jsonplaceholder.typicode.com/users → {'{{apiResult_body}}'}</p>
        <p>Use {'{{apiResult_statusCode}}'}, {'{{apiResult_isSuccess}}'} in downstream steps.</p>
      </div>
    </div>
  );
}
