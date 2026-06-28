import { useCallback, useState } from 'react';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { NODE_DESCRIPTIONS, TOOLBOX_ITEMS } from './nodeDefinitions';

const STORAGE_KEY = 'alterone-toolbox-groups-v2';

const TOOLBOX_GROUPS = [
  { id: 'mail',    label: 'Mail',          category: 'Mail',    defaultOpen: false },
  { id: 'excel',   label: 'Excel',         category: 'Excel',   defaultOpen: false },
  { id: 'file',    label: 'File / Folder', category: 'Folder',  defaultOpen: false },
  { id: 'logic',       label: 'Logic',         category: 'Logic',       defaultOpen: false },
  { id: 'integration', label: 'Integration',   category: 'Integration', defaultOpen: false },
  { id: 'browser',     label: 'Browser',       category: 'Browser',     defaultOpen: false },
] as const;

function loadGroupState(): Record<string, boolean> {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as Record<string, boolean>;
      const result: Record<string, boolean> = {};
      for (const g of TOOLBOX_GROUPS) {
        result[g.id] = parsed[g.id] ?? g.defaultOpen;
      }
      return result;
    }
  } catch {
    /* ignore */
  }
  return Object.fromEntries(TOOLBOX_GROUPS.map((g) => [g.id, g.defaultOpen]));
}

export default function NodeToolbox() {
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>(loadGroupState);

  const toggleGroup = useCallback((id: string) => {
    setOpenGroups((prev) => {
      const next = { ...prev, [id]: !prev[id] };
      try {
        sessionStorage.setItem(STORAGE_KEY, JSON.stringify(next));
      } catch {
        /* ignore */
      }
      return next;
    });
  }, []);

  function onDragStart(event: React.DragEvent, stepType: string) {
    event.dataTransfer.setData('application/reactflow', stepType);
    event.dataTransfer.effectAllowed = 'move';
  }

  return (
    <aside className="w-52 shrink-0 bg-white border-r border-gray-200 flex flex-col overflow-y-auto">
      <div className="px-3 py-3 border-b border-gray-200">
        <p className="text-[11px] font-semibold text-content uppercase tracking-wider">Step Types</p>
        <p className="text-[11px] text-content/55 mt-1 leading-snug">Expand a group, then drag onto canvas</p>
      </div>

      <div className="flex-1 py-2">
        {TOOLBOX_GROUPS.map((group) => {
          const items = TOOLBOX_ITEMS.filter((i) => i.category === group.category);
          if (items.length === 0) return null;

          const isOpen = openGroups[group.id] ?? group.defaultOpen;

          return (
            <div key={group.id} className="mb-0.5">
              <button
                type="button"
                onClick={() => toggleGroup(group.id)}
                className={`w-full flex items-center gap-2 px-3 py-2 text-left transition-colors border-l-2 ${
                  isOpen
                    ? 'border-brand-primary bg-gray-50'
                    : 'border-transparent hover:bg-gray-50'
                }`}
                aria-expanded={isOpen}
              >
                {isOpen
                  ? <ChevronDown size={13} className="shrink-0 text-content/50" />
                  : <ChevronRight size={13} className="shrink-0 text-content/50" />
                }
                <span className="flex-1 text-[11px] font-semibold text-content uppercase tracking-wide">
                  {group.label}
                </span>
                <span className="text-[10px] font-medium text-content/45 tabular-nums">
                  {items.length}
                </span>
              </button>

              {isOpen && (
                <div className="space-y-0.5 px-2 pb-2 pt-0.5">
                  {items.map((item) => {
                    const description = item.description ?? NODE_DESCRIPTIONS[item.stepType];
                    return (
                      <div
                        key={item.stepType}
                        draggable
                        onDragStart={(e) => onDragStart(e, item.stepType)}
                        className="flex items-start gap-2 rounded-md px-2 py-2 cursor-grab active:cursor-grabbing select-none border border-transparent hover:border-gray-200 hover:bg-gray-50 transition-colors"
                        title={description ? `${item.label} — ${description}` : `Drag to add ${item.label}`}
                      >
                        <div
                          className="flex h-6 w-6 items-center justify-center rounded shrink-0 text-xs mt-0.5"
                          style={{ background: item.color }}
                        >
                          <span>{item.emoji}</span>
                        </div>
                        <div className="min-w-0 flex-1">
                          <span className="text-[12px] font-medium text-content leading-tight block">
                            {item.label}
                          </span>
                          {description && (
                            <span className="text-[10px] text-content/55 leading-snug block mt-0.5 line-clamp-2">
                              {description}
                            </span>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          );
        })}
      </div>

      <div className="px-3 py-3 border-t border-gray-100 text-[11px] text-content/55 leading-relaxed">
        <p className="font-medium text-content/70 mb-1.5">Tips</p>
        <ul className="space-y-1">
          <li>Drag nodes to canvas</li>
          <li>Connect handles to link steps</li>
          <li>Click a node to edit properties</li>
        </ul>
      </div>
    </aside>
  );
}
