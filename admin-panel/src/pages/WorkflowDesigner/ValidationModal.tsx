import type { ValidationResult } from './validation';

interface Props {
  result: ValidationResult;
  /** What action was blocked — affects the modal title and CTA copy. */
  action: 'run' | 'publish';
  onClose: () => void;
}

type Item = { nodeId: string; nodeName: string; message: string };

function groupByNode<T extends Item>(items: T[]): Array<{ name: string; items: T[] }> {
  const order: string[] = [];
  const map = new Map<string, T[]>();
  for (const item of items) {
    const key = item.nodeId || '__graph__';
    if (!map.has(key)) { order.push(key); map.set(key, []); }
    map.get(key)!.push(item);
  }
  return order.map(k => ({ name: map.get(k)![0].nodeName || 'Graph', items: map.get(k)! }));
}

export default function ValidationModal({ result, action, onClose }: Props) {
  const { errors, warnings } = result;
  const errorGroups   = groupByNode(errors);
  const warningGroups = groupByNode(warnings);
  const actionLabel   = action === 'run' ? 'Run' : 'Publish';
  const blocked       = errors.length > 0;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-lg bg-white rounded-xl shadow-2xl flex flex-col max-h-[80vh]">

        {/* Header */}
        <div className={`px-5 py-4 border-b border-gray-200 flex items-start gap-3`}>
          <span className="text-2xl shrink-0">{blocked ? '🚫' : '⚠️'}</span>
          <div>
            <h2 className="text-sm font-semibold text-gray-900">
              {blocked
                ? `Cannot ${actionLabel} — ${errors.length} validation error${errors.length !== 1 ? 's' : ''}`
                : `${warnings.length} warning${warnings.length !== 1 ? 's' : ''} before ${actionLabel}`}
            </h2>
            <p className="text-xs text-gray-500 mt-0.5">
              {blocked
                ? `Fix the errors below, then try ${actionLabel === 'Run' ? 'running' : 'publishing'} again.`
                : 'The workflow can still run, but consider fixing these warnings.'}
            </p>
          </div>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto px-5 py-4 space-y-5">

          {/* Errors */}
          {errorGroups.length > 0 && (
            <section>
              <p className="text-xs font-semibold text-red-700 uppercase tracking-wide mb-2 flex items-center gap-1.5">
                <span className="inline-block h-2 w-2 rounded-full bg-red-600" />
                Errors — {errors.length}
              </p>
              <div className="space-y-2">
                {errorGroups.map(({ name, items }) => (
                  <div key={name} className="rounded-lg border border-red-200 bg-red-50 px-3 py-2.5">
                    <p className="text-xs font-semibold text-red-800 mb-1">{name}</p>
                    <ul className="space-y-0.5">
                      {items.map((e, i) => (
                        <li key={i} className="text-xs text-red-700 flex gap-1.5">
                          <span className="shrink-0">•</span>
                          <span>{e.message}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            </section>
          )}

          {/* Warnings */}
          {warningGroups.length > 0 && (
            <section>
              <p className="text-xs font-semibold text-amber-700 uppercase tracking-wide mb-2 flex items-center gap-1.5">
                <span className="inline-block h-2 w-2 rounded-full bg-amber-500" />
                Warnings — {warnings.length}
              </p>
              <div className="space-y-2">
                {warningGroups.map(({ name, items }) => (
                  <div key={name} className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2.5">
                    <p className="text-xs font-semibold text-amber-800 mb-1">{name}</p>
                    <ul className="space-y-0.5">
                      {items.map((w, i) => (
                        <li key={i} className="text-xs text-amber-700 flex gap-1.5">
                          <span className="shrink-0">•</span>
                          <span>{w.message}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            </section>
          )}
        </div>

        {/* Footer */}
        <div className="px-5 py-3 border-t border-gray-200 flex justify-end">
          <button onClick={onClose} className="btn btn-secondary">
            Close &amp; Fix Issues
          </button>
        </div>
      </div>
    </div>
  );
}
