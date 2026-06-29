import { useCallback, useEffect, useRef, useState } from 'react';
import { Crosshair, Loader2 } from 'lucide-react';
import { browserPickerApi } from '../../api/endpoints';
import type { BrowserPickerCandidate, BrowserPickerSelectedResponse } from '../../api/types';

interface PickSelectorModalProps {
  initialUrl?: string;
  onUse: (selector: string) => void;
  onClose: () => void;
}

const EMPTY: BrowserPickerSelectedResponse = {
  primarySelector: '',
  selector: '',
  candidates: [],
  selectedElement: { tagName: '', text: '', id: '', name: '', ariaLabel: '', href: '' },
  originalClickedElement: { tagName: '', text: '', id: '', name: '', ariaLabel: '', href: '' },
  resolvedClickableElement: { tagName: '', text: '', id: '', name: '', ariaLabel: '', href: '' },
  tagName: '',
  text: '',
  id: '',
  name: '',
  ariaLabel: '',
  href: '',
};

function DetailRow({ label, value }: { label: string; value: string }) {
  if (!value) return null;
  return (
    <div className="flex gap-2 text-xs">
      <span className="text-gray-500 shrink-0 w-20">{label}</span>
      <span className="text-gray-800 font-mono break-all">{value}</span>
    </div>
  );
}

function ConfidenceBadge({ confidence }: { confidence: BrowserPickerCandidate['confidence'] }) {
  const styles = {
    high:   'bg-green-100 text-green-800 border-green-200',
    medium: 'bg-amber-100 text-amber-800 border-amber-200',
    low:    'bg-red-100 text-red-800 border-red-200',
  } as const;
  return (
    <span className={`text-[10px] font-semibold uppercase px-1.5 py-0.5 rounded border ${styles[confidence]}`}>
      {confidence}
    </span>
  );
}

const CONFIDENCE_ORDER: Record<BrowserPickerCandidate['confidence'], number> = {
  high: 0,
  medium: 1,
  low: 2,
};

function sortCandidates(
  list: BrowserPickerCandidate[],
  primary: string,
): BrowserPickerCandidate[] {
  return [...list].sort((a, b) => {
    const conf = CONFIDENCE_ORDER[a.confidence] - CONFIDENCE_ORDER[b.confidence];
    if (conf !== 0) return conf;
    if (a.selector === primary) return -1;
    if (b.selector === primary) return 1;
    const matchA = a.matchCount === 1 ? 0 : 1;
    const matchB = b.matchCount === 1 ? 0 : 1;
    if (matchA !== matchB) return matchA - matchB;
    return a.selector.length - b.selector.length;
  });
}

export default function PickSelectorModal({ initialUrl, onUse, onClose }: PickSelectorModalProps) {
  const [url, setUrl]                   = useState(initialUrl?.trim() || 'https://www.google.com');
  const [sessionId, setSessionId]       = useState<string | null>(null);
  const [opening, setOpening]           = useState(false);
  const [error, setError]               = useState<string | null>(null);
  const [pick, setPick]                 = useState<BrowserPickerSelectedResponse>(EMPTY);
  const [chosenSelector, setChosenSelector] = useState('');
  const sessionIdRef                    = useRef<string | null>(null);

  const stopSession = useCallback(async (id: string | null) => {
    if (!id) return;
    try {
      await browserPickerApi.stop(id);
    } catch {
      // ignore cleanup errors
    }
  }, []);

  useEffect(() => {
    sessionIdRef.current = sessionId;
  }, [sessionId]);

  useEffect(() => {
    return () => {
      void stopSession(sessionIdRef.current);
    };
  }, [stopSession]);

  useEffect(() => {
    if (!sessionId) return;

    let cancelled = false;
    const poll = async () => {
      try {
        const res = await browserPickerApi.getSelected(sessionId);
        if (cancelled) return;
        const data = res.data;
        if (data.candidates?.length || data.primarySelector || data.selector) {
          setPick(data);
          setChosenSelector((prev) => prev || data.primarySelector || data.selector);
        }
      } catch {
        if (!cancelled) setError('Lost connection to picker session.');
      }
    };

    void poll();
    const timer = window.setInterval(poll, 500);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [sessionId]);

  async function handleOpenPage() {
    setOpening(true);
    setError(null);
    setPick(EMPTY);
    setChosenSelector('');

    if (sessionId) {
      await stopSession(sessionId);
      setSessionId(null);
    }

    try {
      const res = await browserPickerApi.start(url.trim());
      setSessionId(res.data.sessionId);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to start browser picker');
    } finally {
      setOpening(false);
    }
  }

  async function handleCancel() {
    await stopSession(sessionId);
    setSessionId(null);
    onClose();
  }

  async function handleUseSelector() {
    if (!chosenSelector) return;
    await stopSession(sessionId);
    setSessionId(null);
    onUse(chosenSelector);
    onClose();
  }

  const original = pick.originalClickedElement ?? EMPTY.originalClickedElement;
  const resolved = pick.resolvedClickableElement ?? pick.selectedElement ?? EMPTY.resolvedClickableElement;
  const primary = pick.primarySelector || pick.selector;
  const candidates = sortCandidates(pick.candidates ?? [], primary);
  const primaryCandidate = candidates.find((c) => c.selector === primary);

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/50 p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-xl max-h-[90vh] flex flex-col">
        <div className="px-5 py-4 border-b border-gray-200 shrink-0">
          <h3 className="text-lg font-semibold text-gray-900">Pick Browser Selector</h3>
          <p className="text-xs text-gray-500 mt-1">
            Opens a headed Chromium window on this machine (dev only).
          </p>
        </div>

        <div className="px-5 py-4 space-y-4 overflow-y-auto flex-1">
          <div>
            <label className="label">URL</label>
            <input
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              className="input font-mono text-xs"
              placeholder="https://example.com"
              spellCheck={false}
              disabled={!!sessionId}
            />
          </div>

          <p className="text-sm text-gray-600 bg-teal-50 border border-teal-100 rounded-lg px-3 py-2">
            Click an element on the page to capture selector candidates.
          </p>

          {sessionId && (
            <>
              <div className="rounded-lg border border-gray-200 bg-gray-50 px-3 py-2 space-y-3">
                <div>
                  <p className="text-xs font-medium text-gray-500 mb-1">Clicked Element</p>
                  <p className="text-sm font-mono text-gray-800">
                    &lt;{original.tagName || '…'}&gt;
                    {original.text ? (
                      <span className="text-gray-500 font-sans ml-1 truncate">"{original.text.slice(0, 60)}"</span>
                    ) : null}
                  </p>
                </div>
                <div>
                  <p className="text-xs font-medium text-gray-500 mb-1">Resolved Click Target</p>
                  <p className="text-sm font-mono text-teal-800">
                    &lt;{resolved.tagName || '…'}&gt;
                    {resolved.text ? (
                      <span className="text-gray-600 font-sans ml-1 truncate">"{resolved.text.slice(0, 60)}"</span>
                    ) : null}
                  </p>
                </div>
                {primary && (
                  <div className="pt-2 border-t border-gray-200 space-y-1">
                    <p className="text-xs font-medium text-gray-500">Recommended (Selector Healing V1)</p>
                    <p className="text-xs font-mono text-teal-800 break-all">{primary}</p>
                    {primaryCandidate && (
                      <div className="flex flex-wrap items-center gap-2">
                        <ConfidenceBadge confidence={primaryCandidate.confidence} />
                        <span className="text-[10px] font-medium text-gray-500 uppercase">{primaryCandidate.type}</span>
                        <span className="text-[10px] text-gray-400">matches: {primaryCandidate.matchCount}</span>
                      </div>
                    )}
                  </div>
                )}
                <div className="pt-2 border-t border-gray-200 space-y-1">
                  <DetailRow label="id" value={resolved.id} />
                  <DetailRow label="name" value={resolved.name} />
                  <DetailRow label="aria-label" value={resolved.ariaLabel} />
                  <DetailRow label="href" value={resolved.href} />
                </div>
              </div>

              <div className="space-y-2">
                <p className="text-xs font-medium text-gray-500">Selector candidates (pick any)</p>
                {candidates.length === 0 && (
                  <p className="text-xs text-gray-400 italic">Waiting for click…</p>
                )}
                {candidates.map((c) => {
                  const isRecommended = c.selector === primary;
                  const isSelected = chosenSelector === c.selector;
                  return (
                    <label
                      key={c.selector}
                      className={`block rounded-lg border px-3 py-2 cursor-pointer transition-colors ${
                        isSelected
                          ? 'border-teal-500 bg-teal-50 ring-1 ring-teal-500'
                          : 'border-gray-200 hover:border-gray-300 bg-white'
                      }`}
                    >
                      <div className="flex items-start gap-2">
                        <input
                          type="radio"
                          name="picker-candidate"
                          checked={isSelected}
                          onChange={() => setChosenSelector(c.selector)}
                          className="mt-0.5 shrink-0"
                        />
                        <div className="min-w-0 flex-1 space-y-1">
                          <div className="flex flex-wrap items-center gap-2">
                            {isRecommended && (
                              <span className="text-[10px] font-semibold uppercase text-teal-700 bg-teal-100 px-1.5 py-0.5 rounded">
                                Recommended
                              </span>
                            )}
                            <ConfidenceBadge confidence={c.confidence} />
                            <span className="text-[10px] font-medium text-gray-500 uppercase">{c.type || 'css'}</span>
                            <span className="text-[10px] font-medium text-gray-400 uppercase">{c.strategy}</span>
                            <span className="text-[10px] text-gray-400">matches: {c.matchCount}</span>
                          </div>
                          <p className="text-xs font-mono text-gray-800 break-all">{c.selector}</p>
                          <p className="text-[11px] text-gray-500">{c.reason}</p>
                        </div>
                      </div>
                    </label>
                  );
                })}
              </div>
            </>
          )}

          {error && (
            <p className="text-xs text-red-600">⚠ {error}</p>
          )}
        </div>

        <div className="px-5 py-4 border-t border-gray-200 flex flex-wrap gap-2 justify-end shrink-0">
          <button type="button" onClick={handleCancel} className="btn btn-secondary btn-sm">
            Cancel
          </button>
          <button
            type="button"
            onClick={handleOpenPage}
            disabled={opening || !url.trim()}
            className="btn btn-secondary btn-sm"
          >
            {opening ? <Loader2 size={14} className="animate-spin" /> : <Crosshair size={14} />}
            Open Page
          </button>
          <button
            type="button"
            onClick={handleUseSelector}
            disabled={!chosenSelector}
            className="btn btn-primary btn-sm"
          >
            Use Selector
          </button>
        </div>
      </div>
    </div>
  );
}
