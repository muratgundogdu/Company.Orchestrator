import { useState } from 'react';
import { Calendar, Clock } from 'lucide-react';
import { triggerApi } from '../../api/endpoints';
import type { TriggerDto } from '../../api/types';

// ── Constants ─────────────────────────────────────────────────────────────────

const WEEK_DAYS = [
  { label: 'Mon', value: 1 },
  { label: 'Tue', value: 2 },
  { label: 'Wed', value: 3 },
  { label: 'Thu', value: 4 },
  { label: 'Fri', value: 5 },
  { label: 'Sat', value: 6 },
  { label: 'Sun', value: 0 },
];

type ScheduleType = 'daily' | 'weekly' | 'monthly' | 'cron';

const SCHEDULE_LABELS: Record<ScheduleType, string> = {
  daily:   'Daily',
  weekly:  'Weekly',
  monthly: 'Monthly',
  cron:    'Cron',
};

// ── Cron builder ──────────────────────────────────────────────────────────────

function buildCron(
  type: ScheduleType,
  time: string,
  weekDays: number[],
  monthDay: number,
  rawCron: string,
): string {
  const parts = time.split(':');
  const h  = parseInt(parts[0] ?? '8',  10);
  const m  = parseInt(parts[1] ?? '0',  10);
  const hh = isNaN(h) ? 8 : Math.min(23, Math.max(0, h));
  const mm = isNaN(m) ? 0 : Math.min(59, Math.max(0, m));

  switch (type) {
    case 'daily':
      return `${mm} ${hh} * * *`;
    case 'weekly': {
      const days = weekDays.length > 0
        ? [...weekDays].sort((a, b) => a - b).join(',')
        : '*';
      return `${mm} ${hh} * * ${days}`;
    }
    case 'monthly':
      return `${mm} ${hh} ${monthDay} * *`;
    case 'cron':
      return rawCron.trim();
    default:
      return `${mm} ${hh} * * *`;
  }
}

/** Human-readable summary of the cron expression shown alongside raw form. */
function cronHint(type: ScheduleType, time: string, weekDays: number[], monthDay: number): string {
  switch (type) {
    case 'daily':   return `Every day at ${time}`;
    case 'weekly': {
      const names = WEEK_DAYS.filter((d) => weekDays.includes(d.value)).map((d) => d.label);
      return names.length > 0 ? `Every ${names.join(', ')} at ${time}` : 'Pick at least one day';
    }
    case 'monthly': return `Day ${monthDay} of every month at ${time}`;
    case 'cron':    return 'Custom cron expression';
    default:        return '';
  }
}

// ── Props ─────────────────────────────────────────────────────────────────────

interface ScheduleModalProps {
  definitionId: string;
  definitionName: string;
  onClose: () => void;
  onScheduled: (trigger: TriggerDto) => void;
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function ScheduleModal({
  definitionId,
  definitionName,
  onClose,
  onScheduled,
}: ScheduleModalProps) {
  const [name, setName]               = useState(`${definitionName}-schedule`);
  const [isActive, setIsActive]       = useState(true);
  const [scheduleType, setScheduleType] = useState<ScheduleType>('daily');
  const [time, setTime]               = useState('08:30');
  const [weekDays, setWeekDays]       = useState<number[]>([1]); // Monday default
  const [monthDay, setMonthDay]       = useState(1);
  const [rawCron, setRawCron]         = useState('30 8 * * *');
  const [inputData, setInputData]     = useState('{}');
  const [saving, setSaving]           = useState(false);
  const [error, setError]             = useState<string | null>(null);

  const cronExpr = buildCron(scheduleType, time, weekDays, monthDay, rawCron);
  const hint     = cronHint(scheduleType, time, weekDays, monthDay);

  function toggleWeekDay(v: number) {
    setWeekDays((prev) =>
      prev.includes(v) ? prev.filter((d) => d !== v) : [...prev, v],
    );
  }

  async function handleSave() {
    if (!name.trim()) { setError('Trigger name is required'); return; }
    if (scheduleType === 'weekly' && weekDays.length === 0) {
      setError('Select at least one day of the week'); return;
    }
    if (scheduleType === 'cron' && !rawCron.trim()) {
      setError('Cron expression is required'); return;
    }

    let parsedInput: string | undefined;
    if (inputData.trim() && inputData.trim() !== '{}') {
      try {
        JSON.parse(inputData);
        parsedInput = inputData.trim();
      } catch {
        setError('Default Input Data is not valid JSON');
        return;
      }
    }

    setSaving(true);
    setError(null);
    try {
      const res = await triggerApi.create({
        processDefinitionId: definitionId,
        name: name.trim(),
        type: 'Scheduled',
        isActive,
        cronExpression: cronExpr,
        defaultInputData: parsedInput,
      });
      onScheduled(res.data);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to create trigger');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="bg-white rounded-xl shadow-2xl w-[560px] max-h-[90vh] flex flex-col overflow-hidden">

        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <div className="flex items-center gap-3">
            <div className="flex h-9 w-9 items-center justify-center rounded-full bg-blue-100 shrink-0">
              <Calendar size={16} className="text-brand-primary" />
            </div>
            <div>
              <h2 className="font-semibold text-gray-900 leading-tight">Schedule Workflow</h2>
              <p className="text-xs text-gray-400 leading-tight mt-0.5">{definitionName}</p>
            </div>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-2xl leading-none">×</button>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto p-5 space-y-5">
          {error && (
            <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-3 py-2">
              {error}
            </div>
          )}

          {/* Name + Active toggle */}
          <div className="flex gap-3 items-end">
            <div className="flex-1">
              <label className="label">Trigger Name *</label>
              <input
                value={name}
                onChange={(e) => setName(e.target.value)}
                className="input"
                placeholder="my-workflow-schedule"
              />
            </div>
            <label className="flex items-center gap-2 pb-2.5 cursor-pointer select-none shrink-0">
              <input
                type="checkbox"
                checked={isActive}
                onChange={(e) => setIsActive(e.target.checked)}
                className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
              />
              <span className="text-sm font-medium text-gray-700">Active</span>
            </label>
          </div>

          {/* Schedule type tabs */}
          <div>
            <label className="label">Schedule Type</label>
            <div className="grid grid-cols-4 rounded-lg border border-gray-200 overflow-hidden divide-x divide-gray-200">
              {(Object.keys(SCHEDULE_LABELS) as ScheduleType[]).map((t) => (
                <button
                  key={t}
                  onClick={() => setScheduleType(t)}
                  className={`py-2 text-sm font-medium transition-colors ${
                    scheduleType === t
                      ? 'bg-brand-primary text-white'
                      : 'bg-white text-gray-600 hover:bg-gray-50'
                  }`}
                >
                  {SCHEDULE_LABELS[t]}
                </button>
              ))}
            </div>
          </div>

          {/* ── Daily ── */}
          {scheduleType === 'daily' && (
            <div>
              <label className="label flex items-center gap-1.5">
                <Clock size={12} /> Run at
              </label>
              <input
                type="time"
                value={time}
                onChange={(e) => setTime(e.target.value)}
                className="input w-36"
              />
            </div>
          )}

          {/* ── Weekly ── */}
          {scheduleType === 'weekly' && (
            <div className="space-y-3">
              <div>
                <label className="label">Days of Week</label>
                <div className="flex gap-1.5 flex-wrap mt-1">
                  {WEEK_DAYS.map((d) => (
                    <button
                      key={d.value}
                      onClick={() => toggleWeekDay(d.value)}
                      className={`px-3 py-1.5 rounded-md text-sm font-medium border transition-colors ${
                        weekDays.includes(d.value)
                          ? 'bg-brand-primary text-white border-brand-primary'
                          : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'
                      }`}
                    >
                      {d.label}
                    </button>
                  ))}
                </div>
              </div>
              <div>
                <label className="label flex items-center gap-1.5">
                  <Clock size={12} /> Run at
                </label>
                <input
                  type="time"
                  value={time}
                  onChange={(e) => setTime(e.target.value)}
                  className="input w-36"
                />
              </div>
            </div>
          )}

          {/* ── Monthly ── */}
          {scheduleType === 'monthly' && (
            <div className="flex gap-4 items-end">
              <div>
                <label className="label">Day of Month</label>
                <input
                  type="number"
                  min={1}
                  max={31}
                  value={monthDay}
                  onChange={(e) =>
                    setMonthDay(Math.min(31, Math.max(1, parseInt(e.target.value, 10) || 1)))
                  }
                  className="input w-24"
                />
              </div>
              <div>
                <label className="label flex items-center gap-1.5">
                  <Clock size={12} /> Run at
                </label>
                <input
                  type="time"
                  value={time}
                  onChange={(e) => setTime(e.target.value)}
                  className="input w-36"
                />
              </div>
            </div>
          )}

          {/* ── Raw Cron ── */}
          {scheduleType === 'cron' && (
            <div>
              <label className="label">Cron Expression</label>
              <input
                value={rawCron}
                onChange={(e) => setRawCron(e.target.value)}
                className="input font-mono"
                placeholder="30 8 * * 1-5"
                spellCheck={false}
              />
              <p className="text-xs text-gray-400 mt-1">
                Format:{' '}
                <code className="bg-gray-100 px-1 rounded">minute hour day-of-month month day-of-week</code>
              </p>
            </div>
          )}

          {/* Cron preview card */}
          <div className="rounded-lg border border-blue-100 bg-blue-50 px-4 py-3 space-y-0.5">
            <div className="flex items-center gap-2">
              <span className="text-xs text-gray-500 shrink-0">Cron expression:</span>
              <code className="text-sm font-mono font-bold text-blue-700">{cronExpr || '—'}</code>
            </div>
            <p className="text-xs text-blue-500">{hint}</p>
          </div>

          {/* Default input data */}
          <div>
            <label className="label">
              Default Input Data{' '}
              <span className="font-normal text-gray-400">(JSON, optional)</span>
            </label>
            <textarea
              value={inputData}
              onChange={(e) => setInputData(e.target.value)}
              rows={4}
              className="input font-mono text-xs resize-y"
              spellCheck={false}
            />
          </div>
        </div>

        {/* Footer */}
        <div className="flex gap-2 justify-end px-5 py-3 border-t border-gray-200 bg-gray-50">
          <button onClick={onClose} className="btn btn-secondary">Cancel</button>
          <button onClick={handleSave} disabled={saving} className="btn btn-primary">
            <Calendar size={13} />
            {saving ? 'Scheduling…' : 'Create Schedule'}
          </button>
        </div>
      </div>
    </div>
  );
}
