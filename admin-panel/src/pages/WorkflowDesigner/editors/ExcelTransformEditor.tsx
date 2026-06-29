import { useState } from 'react';
import { Plus, Trash2, ArrowUp, ArrowDown } from 'lucide-react';
import type { EditorProps } from './types';
import EditableNumberInput from './EditableNumberInput';
import TransformColumnExpressionField from './TransformColumnExpressionField';

// ── Operation metadata ────────────────────────────────────────────────────────

const OP_TYPES = [
  'importTextToSheet',
  'transformColumn',
  'copyColumnValues',
  'replaceColumnValues',
  'calculateFormulaValues',
  'lookupColumn',
  'multiColumnLookup',
  'compositeLookup',
  'replaceWithLookupResult',
  'insertColumn',
  'copyColumn',
  'convertColumnToNumber',
  'setFormula',
  'fillFormulaDown',
  'setColumnFormat',
  'setHeader',
  'autoFitColumns',
  'createSheetFromColumns',
  'setCellStyle',
  'addSheet',
  'deleteRow',
  'multiplyColumn',
  'filterRows',
  'sortRows',
  'removeDuplicates',
  'removeEmptyRows',
] as const;

type OpType = typeof OP_TYPES[number];
type Op     = Record<string, unknown>;

const OP_LABELS: Record<OpType, string> = {
  importTextToSheet:      'Import Text/CSV To Sheet',
  transformColumn:        'Transform Column',
  copyColumnValues:       'Copy Column Values',
  replaceColumnValues:    'Replace Column Values',
  calculateFormulaValues: 'Calculate Formula Values',
  lookupColumn:           'Lookup Column',
  multiColumnLookup:      'Multi Column Lookup',
  compositeLookup:        'Composite Lookup',
  replaceWithLookupResult:'Replace With Lookup Result',
  insertColumn:           'Insert Column',
  copyColumn:             'Copy Column',
  convertColumnToNumber:  'Convert Column To Number',
  setFormula:             'Set Formula',
  fillFormulaDown:        'Fill Formula Down',
  setColumnFormat:        'Set Column Format',
  setHeader:              'Set Header',
  autoFitColumns:         'Auto Fit Columns',
  createSheetFromColumns: 'Create Sheet From Columns',
  setCellStyle:           'Set Cell Style',
  addSheet:               'Add Sheet',
  deleteRow:              'Delete Row',
  multiplyColumn:         'Multiply Column',
  filterRows:             'Filter Rows',
  sortRows:               'Sort Rows',
  removeDuplicates:       'Remove Duplicates',
  removeEmptyRows:        'Remove Empty Rows',
};

const OP_COLORS: Record<OpType, string> = {
  importTextToSheet:      '#0f766e',
  transformColumn:        '#4f46e5',
  copyColumnValues:       '#4338ca',
  replaceColumnValues:    '#6366f1',
  calculateFormulaValues: '#7c3aed',
  lookupColumn:           '#1d4ed8',
  multiColumnLookup:      '#2563eb',
  compositeLookup:        '#3b82f6',
  replaceWithLookupResult:'#1e40af',
  insertColumn:           '#0891b2',
  copyColumn:             '#2563eb',
  convertColumnToNumber:  '#7c3aed',
  setFormula:             '#059669',
  fillFormulaDown:        '#0d9488',
  setColumnFormat:        '#6366f1',
  setHeader:              '#db2777',
  autoFitColumns:         '#64748b',
  createSheetFromColumns: '#ea580c',
  setCellStyle:           '#9333ea',
  addSheet:               '#059669',
  deleteRow:              '#dc2626',
  multiplyColumn:         '#d97706',
  filterRows:             '#b45309',
  sortRows:               '#92400e',
  removeDuplicates:       '#78350f',
  removeEmptyRows:        '#7c3aed',
};

const OP_DEFAULTS: Record<OpType, Op> = {
  importTextToSheet: {
    textArtifactName: 'elastic-report.csv',
    targetSheet: 'ElasticData',
    delimiter: 'comma',
    customDelimiter: ',',
    encoding: 'UTF-8',
    hasHeader: true,
    startCell: 'A1',
    quoteChar: '"',
    trimValues: true,
    parseNumbers: false,
  },
  transformColumn: {
    sheetName: 'Data',
    sourceColumn: 'B',
    targetColumn: 'C',
    startRow: 2,
    expression: 'toNumber(value) / 100',
    targetHeader: 'Converted Amount',
    numberFormat: '#,##0.00',
  },
  copyColumnValues: {
    sourceSheet: 'Temp',
    sourceColumn: 'B',
    targetSheet: 'Data',
    targetColumn: 'C',
    startRow: 2,
    includeHeader: false,
  },
  replaceColumnValues: {
    sheetName: 'Data',
    sourceColumn: 'C',
    targetColumn: 'B',
    startRow: 2,
  },
  calculateFormulaValues: {
    sheetName: 'Temp',
    sourceFormulaColumn: 'B',
    targetColumn: 'C',
    startRow: 2,
    numberFormat: '#,##0.00',
  },
  lookupColumn: {
    sourceSheet: 'Data',
    lookupColumn: 'A',
    referenceSheet: 'Customers',
    referenceKeyColumn: 'A',
    referenceReturnColumn: 'B',
    targetColumn: 'C',
    targetHeader: 'Customer Name',
    startRow: 2,
    notFoundValue: '',
    ignoreCase: true,
  },
  multiColumnLookup: {
    sourceSheet: 'Data',
    lookupColumn: 'A',
    referenceSheet: 'Customers',
    referenceKeyColumn: 'A',
    mappings: [
      { referenceColumn: 'B', targetColumn: 'C', targetHeader: 'Customer Name' },
      { referenceColumn: 'C', targetColumn: 'D', targetHeader: 'Region' },
    ],
    startRow: 2,
    ignoreCase: true,
  },
  compositeLookup: {
    sourceSheet: 'Data',
    lookupColumns: ['A', 'B'],
    referenceSheet: 'Reference',
    referenceKeyColumns: ['A', 'B'],
    referenceReturnColumn: 'C',
    targetColumn: 'D',
    separator: '|',
    startRow: 2,
    ignoreCase: true,
  },
  replaceWithLookupResult: {
    sourceSheet: 'Data',
    lookupColumn: 'A',
    referenceSheet: 'Customers',
    referenceKeyColumn: 'A',
    referenceReturnColumn: 'B',
    targetColumn: 'A',
    startRow: 2,
    ignoreCase: true,
  },
  insertColumn: {
    sheetName: 'Data',
    afterColumn: 'B',
    newColumn: 'C',
    header: 'New Column',
  },
  copyColumn: {
    sourceSheet: 'Data',
    sourceColumn: 'B',
    targetSheet: 'Data',
    targetColumn: 'C',
    targetHeader: 'Copied B',
  },
  convertColumnToNumber: {
    sheetName: 'Data',
    column: 'C',
    startRow: 2,
    numberFormat: '#,##0.00',
  },
  setFormula: {
    sheetName: 'Data',
    cell: 'D2',
    formula: 'C2*2',
  },
  fillFormulaDown: {
    sheetName: 'Data',
    column: 'D',
    startRow: 2,
    endRow: null,
    formulaTemplate: 'C{row}*2',
  },
  setColumnFormat: {
    sheetName: 'Data',
    column: 'C',
    format: '#,##0.00',
  },
  setHeader: {
    sheetName: 'Data',
    column: 'C',
    header: 'Amount',
  },
  autoFitColumns: {
    sheetName: 'Data',
  },
  createSheetFromColumns: {
    sourceSheet: 'Data',
    targetSheet: 'Result',
    columns: ['A', 'B', 'C'],
  },
  setCellStyle: {
    sheetName: 'Data',
    range: 'A1:D1',
    bold: true,
    backgroundColor: '#D9EAF7',
  },
  addSheet:        { sheetName: 'Result' },
  deleteRow:       { sheetName: 'Data', rowNumber: 1 },
  multiplyColumn:  { sourceSheet: 'Data', sourceColumn: 'C', factor: 2, targetSheet: 'Result', targetColumn: 'A', targetHeader: 'C x 2' },
  filterRows: {
    sheetName: 'Data',
    column: 'Customer',
    startRow: 2,
    mode: 'keep',
    conditionJoin: 'and',
    conditions: [
      { operator: 'isNotEmpty' },
      { operator: 'notEquals', value: '0' },
      { operator: 'notEquals', value: 'null' },
    ],
  },
  sortRows: {
    sheetName: 'Data',
    startRow: 2,
    sorts: [
      { column: 'Amount', direction: 'desc', dataType: 'number' },
    ],
  },
  removeDuplicates: {
    sheetName: 'Data',
    startRow: 2,
    columns: ['CustomerNo'],
    keep: 'first',
    ignoreCase: true,
    trimValues: true,
  },
  removeEmptyRows: { sheetName: 'Data', startRow: 2 },
};

function opFieldKey(idx: number, field: string) {
  return `operations.${idx}.${field}`;
}

// ── Operation summary (shown in card header subtitle) ─────────────────────────

function opSummary(op: Op): string {
  switch (op.type as OpType) {
    case 'importTextToSheet':
      return `${op.textArtifactName ?? '?'} → "${op.targetSheet ?? '?'}"`;
    case 'transformColumn':
      return `${op.sourceColumn ?? '?'} → ${op.targetColumn ?? '?'} (${op.expression ?? '?'})`;
    case 'copyColumnValues':
      return `${op.sourceSheet}.${op.sourceColumn} → ${op.targetSheet}.${op.targetColumn}`;
    case 'replaceColumnValues':
      return `${op.sourceColumn ?? '?'} → ${op.targetColumn ?? '?'} in "${op.sheetName ?? '?'}"`;
    case 'calculateFormulaValues':
      return `formula ${op.sourceFormulaColumn ?? '?'} → ${op.targetColumn ?? '?'} in "${op.sheetName ?? '?'}"`;
    case 'lookupColumn':
      return `${op.lookupColumn ?? '?'} → ${op.targetColumn ?? '?'} via ${op.referenceSheet ?? '?'}`;
    case 'multiColumnLookup':
      return `${op.lookupColumn ?? '?'} → ${Array.isArray(op.mappings) ? op.mappings.length : 0} cols via ${op.referenceSheet ?? '?'}`;
    case 'compositeLookup':
      return `[${Array.isArray(op.lookupColumns) ? op.lookupColumns.join(',') : '?'}] → ${op.targetColumn ?? '?'}`;
    case 'replaceWithLookupResult':
      return `replace ${op.lookupColumn ?? '?'} via ${op.referenceSheet ?? '?'}`;
    case 'insertColumn':
      return `after ${op.afterColumn ?? '?'} → ${op.newColumn ?? '?'} in "${op.sheetName ?? '?'}"`;
    case 'copyColumn':
      return `${op.sourceSheet}.${op.sourceColumn} → ${op.targetSheet}.${op.targetColumn}`;
    case 'convertColumnToNumber':
      return `col ${op.column ?? '?'} from row ${op.startRow ?? '?'} in "${op.sheetName ?? '?'}"`;
    case 'setFormula':
      return `${op.cell ?? '?'} = ${op.formula ?? '?'}`;
    case 'fillFormulaDown':
      return `col ${op.column ?? '?'} rows ${op.startRow ?? '?'}–${op.endRow ?? 'last'}`;
    case 'setColumnFormat':
      return `col ${op.column ?? '?'} → ${op.format ?? '?'}`;
    case 'setHeader':
      return `col ${op.column ?? '?'} = "${op.header ?? '?'}"`;
    case 'autoFitColumns':
      return `"${op.sheetName ?? '?'}"`;
    case 'createSheetFromColumns':
      return `${op.sourceSheet} → ${op.targetSheet} [${Array.isArray(op.columns) ? op.columns.join(', ') : op.columns ?? '?'}]`;
    case 'setCellStyle':
      return `${op.range ?? '?'} in "${op.sheetName ?? '?'}"`;
    case 'addSheet':
      return `"${op.sheetName ?? '?'}"`;
    case 'deleteRow':
      return `row ${op.rowNumber ?? '?'} in "${op.sheetName ?? '?'}"`;
    case 'multiplyColumn':
      return `${op.sourceSheet}.${op.sourceColumn} × ${op.factor} → ${op.targetSheet}.${op.targetColumn}`;
    case 'filterRows': {
      const conds = Array.isArray(op.conditions) ? op.conditions.length : 0;
      return `${op.mode ?? 'keep'} rows in "${op.sheetName ?? '?'}" col ${op.column ?? '?'} (${conds} condition${conds === 1 ? '' : 's'})`;
    }
    case 'sortRows': {
      const keys = Array.isArray(op.sorts) ? op.sorts.length : 0;
      const first = Array.isArray(op.sorts) && op.sorts[0]
        ? `${(op.sorts[0] as Record<string, unknown>).column ?? '?'} ${(op.sorts[0] as Record<string, unknown>).direction ?? 'asc'}`
        : '?';
      return `"${op.sheetName ?? '?'}" by ${first}${keys > 1 ? ` (+${keys - 1} more)` : ''}`;
    }
    case 'removeDuplicates': {
      const cols = Array.isArray(op.columns) ? op.columns.join(', ') : String(op.columns ?? '?');
      return `"${op.sheetName ?? '?'}" on [${cols}] keep ${op.keep ?? 'first'}`;
    }
    case 'removeEmptyRows':
      return `from row ${op.startRow ?? '?'} in "${op.sheetName ?? '?'}"`;
    default:
      return '';
  }
}

// ── Compact field wrapper ─────────────────────────────────────────────────────

function F({
  label,
  children,
  error,
}: {
  label: string;
  children: React.ReactNode;
  error?: string;
}) {
  return (
    <div>
      <p className="text-xs text-gray-500 font-medium mb-0.5 leading-tight">{label}</p>
      {children}
      {error && <p className="text-xs text-red-600 mt-0.5">⚠ {error}</p>}
    </div>
  );
}

const IC = 'input text-xs';

type FilterCondition = { operator: string; value?: string; values?: string[] };

const FILTER_OPERATORS = [
  { value: 'equals', label: 'Equals' },
  { value: 'notEquals', label: 'Not Equals' },
  { value: 'contains', label: 'Contains' },
  { value: 'notContains', label: 'Not Contains' },
  { value: 'startsWith', label: 'Starts With' },
  { value: 'endsWith', label: 'Ends With' },
  { value: 'greaterThan', label: 'Greater Than' },
  { value: 'greaterOrEqual', label: 'Greater Or Equal' },
  { value: 'lessThan', label: 'Less Than' },
  { value: 'lessOrEqual', label: 'Less Or Equal' },
  { value: 'isEmpty', label: 'Is Empty' },
  { value: 'isNotEmpty', label: 'Is Not Empty' },
  { value: 'in', label: 'In List' },
  { value: 'notIn', label: 'Not In List' },
] as const;

function filterOpNeedsValue(operator: string): boolean {
  return ['equals', 'notEquals', 'contains', 'notContains', 'startsWith', 'endsWith',
    'greaterThan', 'greaterOrEqual', 'lessThan', 'lessOrEqual'].includes(operator);
}

function filterOpNeedsValues(operator: string): boolean {
  return operator === 'in' || operator === 'notIn';
}

function parseFilterConditions(op: Op): FilterCondition[] {
  const raw = op.conditions;
  if (!Array.isArray(raw)) return [];
  return raw.map((c) => {
    const item = c as Record<string, unknown>;
    return {
      operator: String(item.operator ?? 'equals'),
      value: item.value !== undefined ? String(item.value) : undefined,
      values: Array.isArray(item.values)
        ? item.values.map(String)
        : typeof item.values === 'string'
          ? item.values.split(',').map((s) => s.trim()).filter(Boolean)
          : undefined,
    };
  });
}

function FilterRowsFields({
  op,
  fieldErrors,
  onUpdate,
  str,
  err,
  focus,
}: {
  op: Op;
  fieldErrors?: Record<string, string>;
  onUpdate: (patch: Op) => void;
  str: (k: string, fb?: string) => string;
  err: (field: string) => string | undefined;
  focus: (field: string) => (e: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) => void;
}) {
  const conditions = parseFilterConditions(op);
  const mode = str('mode', 'keep');

  function setConditions(next: FilterCondition[]) {
    onUpdate({ conditions: next });
  }

  function updateCondition(cIdx: number, patch: Partial<FilterCondition>) {
    const next = [...conditions];
    next[cIdx] = { ...next[cIdx], ...patch };
    setConditions(next);
  }

  return (
    <div className="space-y-2">
      <div className="grid grid-cols-2 gap-2">
        <F label="Sheet Name" error={err('sheetName')}>
          <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} placeholder="Data" />
        </F>
        <F label="Column" error={err('column')}>
          <input
            value={str('column', 'Customer')}
            onChange={(e) => onUpdate({ column: e.target.value })}
            className={`${IC} font-mono${err('column') ? ' border-red-400 focus:ring-red-400' : ''}`}
            placeholder="Customer or B"
            spellCheck={false}
          />
        </F>
      </div>
      <div className="grid grid-cols-3 gap-2">
        <F label="Start Row" error={err('startRow')}>
          <EditableNumberInput min={1} fallback={2} value={op.startRow} onValueChange={(startRow) => onUpdate({ startRow })} className={IC} />
        </F>
        <F label="Mode" error={err('mode')}>
          <select value={mode} onChange={(e) => onUpdate({ mode: e.target.value })} className={IC}>
            <option value="keep">Keep matching rows</option>
            <option value="remove">Remove matching rows</option>
          </select>
        </F>
        <F label="Condition Join" error={err('conditionJoin')}>
          <select value={str('conditionJoin', 'and')} onChange={(e) => onUpdate({ conditionJoin: e.target.value })} className={IC}>
            <option value="and">AND</option>
            <option value="or">OR</option>
          </select>
        </F>
      </div>

      <div className="rounded-lg border border-amber-200 bg-amber-50/60 px-2.5 py-2 text-xs text-amber-900">
        Example: Keep rows where Customer is not empty and not equal to 0.
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <p className="text-xs font-medium text-gray-600">Conditions</p>
          <button
            type="button"
            onClick={() => setConditions([...conditions, { operator: 'isNotEmpty' }])}
            className="inline-flex items-center gap-1 text-xs text-brand-primary hover:underline"
          >
            <Plus className="w-3 h-3" /> Add condition
          </button>
        </div>
        {conditions.length === 0 && (
          <p className="text-xs text-red-600">⚠ At least one condition is required</p>
        )}
        {conditions.map((cond, cIdx) => (
          <div key={cIdx} className="rounded border border-gray-200 bg-gray-50/80 p-2 space-y-2">
            <div className="flex items-start gap-2">
              <div className="flex-1 grid grid-cols-2 gap-2">
                <F label="Operator" error={err(`conditions.${cIdx}.operator`)}>
                  <select
                    value={cond.operator}
                    onChange={(e) => updateCondition(cIdx, { operator: e.target.value, value: undefined, values: undefined })}
                    className={IC}
                  >
                    {FILTER_OPERATORS.map((o) => (
                      <option key={o.value} value={o.value}>{o.label}</option>
                    ))}
                  </select>
                </F>
                {filterOpNeedsValue(cond.operator) && (
                  <F label="Value" error={err(`conditions.${cIdx}.value`)}>
                    <input
                      value={cond.value ?? ''}
                      onChange={(e) => updateCondition(cIdx, { value: e.target.value })}
                      onFocus={focus(`conditions.${cIdx}.value`)}
                      className={`${IC} font-mono${err(`conditions.${cIdx}.value`) ? ' border-red-400 focus:ring-red-400' : ''}`}
                      placeholder="0"
                      spellCheck={false}
                    />
                  </F>
                )}
                {filterOpNeedsValues(cond.operator) && (
                  <F label="Values (comma-separated)" error={err(`conditions.${cIdx}.values`)}>
                    <input
                      value={(cond.values ?? []).join(', ')}
                      onChange={(e) => updateCondition(cIdx, {
                        values: e.target.value.split(',').map((s) => s.trim()).filter(Boolean),
                      })}
                      className={`${IC} font-mono${err(`conditions.${cIdx}.values`) ? ' border-red-400 focus:ring-red-400' : ''}`}
                      placeholder="1001, 1002"
                      spellCheck={false}
                    />
                  </F>
                )}
              </div>
              <button
                type="button"
                onClick={() => setConditions(conditions.filter((_, i) => i !== cIdx))}
                className="mt-5 p-1 text-gray-400 hover:text-red-600"
                title="Remove condition"
              >
                <Trash2 className="w-3.5 h-3.5" />
              </button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function parseSortKeys(op: Op): Array<{ column: string; direction: string; dataType: string }> {
  const raw = op.sorts;
  if (!Array.isArray(raw)) return [];
  return raw.map((s) => {
    const item = s as Record<string, unknown>;
    return {
      column: String(item.column ?? 'Amount'),
      direction: String(item.direction ?? 'asc'),
      dataType: String(item.dataType ?? 'auto'),
    };
  });
}

function SortRowsFields({
  op,
  fieldErrors,
  onUpdate,
  str,
  err,
}: {
  op: Op;
  fieldErrors?: Record<string, string>;
  onUpdate: (patch: Op) => void;
  str: (k: string, fb?: string) => string;
  err: (field: string) => string | undefined;
}) {
  const sorts = parseSortKeys(op);

  function setSorts(next: Array<{ column: string; direction: string; dataType: string }>) {
    onUpdate({ sorts: next });
  }

  function updateSort(sIdx: number, patch: Partial<{ column: string; direction: string; dataType: string }>) {
    const next = [...sorts];
    next[sIdx] = { ...next[sIdx], ...patch };
    setSorts(next);
  }

  return (
    <div className="space-y-2">
      <div className="grid grid-cols-2 gap-2">
        <F label="Sheet Name" error={err('sheetName')}>
          <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} placeholder="Data" />
        </F>
        <F label="Start Row" error={err('startRow')}>
          <EditableNumberInput min={1} fallback={2} value={op.startRow} onValueChange={(startRow) => onUpdate({ startRow })} className={IC} />
        </F>
      </div>

      <div className="rounded-lg border border-amber-200 bg-amber-50/60 px-2.5 py-2 text-xs text-amber-900 space-y-1">
        <p>Example: Sort by Amount descending.</p>
        <p>Example: Sort by Date ascending, then Customer ascending.</p>
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <p className="text-xs font-medium text-gray-600">Sort Keys</p>
          <button
            type="button"
            onClick={() => setSorts([...sorts, { column: 'Customer', direction: 'asc', dataType: 'text' }])}
            className="inline-flex items-center gap-1 text-xs text-brand-primary hover:underline"
          >
            <Plus className="w-3 h-3" /> Add sort key
          </button>
        </div>
        {sorts.length === 0 && (
          <p className="text-xs text-red-600">⚠ At least one sort key is required</p>
        )}
        {sorts.map((sort, sIdx) => (
          <div key={sIdx} className="rounded border border-gray-200 bg-gray-50/80 p-2">
            <div className="flex items-start gap-2">
              <div className="flex-1 grid grid-cols-3 gap-2">
                <F label="Column" error={err(`sorts.${sIdx}.column`)}>
                  <input
                    value={sort.column}
                    onChange={(e) => updateSort(sIdx, { column: e.target.value })}
                    className={`${IC} font-mono${err(`sorts.${sIdx}.column`) ? ' border-red-400 focus:ring-red-400' : ''}`}
                    placeholder="Amount"
                    spellCheck={false}
                  />
                </F>
                <F label="Direction" error={err(`sorts.${sIdx}.direction`)}>
                  <select
                    value={sort.direction}
                    onChange={(e) => updateSort(sIdx, { direction: e.target.value })}
                    className={IC}
                  >
                    <option value="asc">Ascending</option>
                    <option value="desc">Descending</option>
                  </select>
                </F>
                <F label="Data Type" error={err(`sorts.${sIdx}.dataType`)}>
                  <select
                    value={sort.dataType}
                    onChange={(e) => updateSort(sIdx, { dataType: e.target.value })}
                    className={IC}
                  >
                    <option value="auto">Auto</option>
                    <option value="text">Text</option>
                    <option value="number">Number</option>
                    <option value="date">Date</option>
                  </select>
                </F>
              </div>
              <button
                type="button"
                onClick={() => setSorts(sorts.filter((_, i) => i !== sIdx))}
                className="mt-5 p-1 text-gray-400 hover:text-red-600"
                title="Remove sort key"
              >
                <Trash2 className="w-3.5 h-3.5" />
              </button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function RemoveDuplicatesFields({
  op,
  onUpdate,
  str,
  err,
}: {
  op: Op;
  onUpdate: (patch: Op) => void;
  str: (k: string, fb?: string) => string;
  err: (field: string) => string | undefined;
}) {
  const columns = Array.isArray(op.columns)
    ? op.columns.map(String)
    : str('columns') ? [str('columns')] : ['CustomerNo'];
  const keep = str('keep', 'first');
  const ignoreCase = op.ignoreCase !== false && op.ignoreCase !== 'false';
  const trimValues = op.trimValues !== false && op.trimValues !== 'false';

  function setColumns(next: string[]) {
    onUpdate({ columns: next });
  }

  return (
    <div className="space-y-2">
      <div className="grid grid-cols-2 gap-2">
        <F label="Sheet Name" error={err('sheetName')}>
          <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} placeholder="Data" />
        </F>
        <F label="Start Row" error={err('startRow')}>
          <EditableNumberInput min={1} fallback={2} value={op.startRow} onValueChange={(startRow) => onUpdate({ startRow })} className={IC} />
        </F>
      </div>

      <F label="Keep" error={err('keep')}>
        <select value={keep} onChange={(e) => onUpdate({ keep: e.target.value })} className={IC}>
          <option value="first">First occurrence</option>
          <option value="last">Last occurrence</option>
        </select>
      </F>

      <div className="rounded-lg border border-amber-200 bg-amber-50/60 px-2.5 py-2 text-xs text-amber-900 space-y-1">
        <p>Example: Remove duplicate customers by CustomerNo.</p>
        <p>Example: Remove duplicate customer-region combinations using columns CustomerNo and Region.</p>
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <p className="text-xs font-medium text-gray-600">Columns</p>
          <button
            type="button"
            onClick={() => setColumns([...columns, 'Region'])}
            className="inline-flex items-center gap-1 text-xs text-brand-primary hover:underline"
          >
            <Plus className="w-3 h-3" /> Add column
          </button>
        </div>
        {columns.length === 0 && (
          <p className="text-xs text-red-600">⚠ At least one column is required</p>
        )}
        {columns.map((col, cIdx) => (
          <div key={cIdx} className="flex items-center gap-2">
            <input
              value={col}
              onChange={(e) => {
                const next = [...columns];
                next[cIdx] = e.target.value;
                setColumns(next);
              }}
              className={`${IC} font-mono flex-1${err(`columns.${cIdx}`) ? ' border-red-400 focus:ring-red-400' : ''}`}
              placeholder="CustomerNo"
              spellCheck={false}
            />
            <button
              type="button"
              onClick={() => setColumns(columns.filter((_, i) => i !== cIdx))}
              className="p-1 text-gray-400 hover:text-red-600"
              title="Remove column"
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </div>
        ))}
      </div>

      <div className="space-y-2">
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={ignoreCase}
            onChange={(e) => onUpdate({ ignoreCase: e.target.checked })}
            className="rounded border-gray-300"
          />
          Ignore case when matching (ABC = abc)
        </label>
        <label className="flex items-center gap-2 text-xs text-content cursor-pointer">
          <input
            type="checkbox"
            checked={trimValues}
            onChange={(e) => onUpdate({ trimValues: e.target.checked })}
            className="rounded border-gray-300"
          />
          Trim whitespace before matching
        </label>
      </div>
    </div>
  );
}

function ColInput({
  value,
  onChange,
  placeholder,
  error,
}: {
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  error?: string;
}) {
  return (
    <input
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className={`${IC}${error ? ' border-red-400 focus:ring-red-400' : ''}`}
      placeholder={placeholder ?? 'A'}
      spellCheck={false}
    />
  );
}

// ── Per-operation field forms ─────────────────────────────────────────────────

function OpFields({
  op,
  idx,
  fieldErrors,
  onUpdate,
  onFocusField,
}: {
  op: Op;
  idx: number;
  fieldErrors?: Record<string, string>;
  onUpdate: (patch: Op) => void;
  onFocusField: (el: HTMLInputElement | HTMLTextAreaElement, key: string) => void;
}) {
  const str = (k: string, fb = '') => String(op[k] ?? fb);
  const err = (field: string) => fieldErrors?.[opFieldKey(idx, field)];
  const focus = (field: string) => (e: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) =>
    onFocusField(e.currentTarget, opFieldKey(idx, field));

  switch (op.type as OpType) {
    case 'importTextToSheet':
      return (
        <div className="space-y-2">
          <F label="Text Artifact Name" error={err('textArtifactName')}>
            <input
              value={str('textArtifactName', 'elastic-report.csv')}
              onChange={(e) => onUpdate({ textArtifactName: e.target.value })}
              onFocus={focus('textArtifactName')}
              className={`${IC} font-mono${err('textArtifactName') ? ' border-red-400 focus:ring-red-400' : ''}`}
              placeholder="elastic-report.csv"
              spellCheck={false}
            />
          </F>
          <F label="Target Sheet" error={err('targetSheet')}>
            <input
              value={str('targetSheet', 'ElasticData')}
              onChange={(e) => onUpdate({ targetSheet: e.target.value })}
              onFocus={focus('targetSheet')}
              className={IC}
              placeholder="ElasticData"
            />
          </F>
          <div className="grid grid-cols-2 gap-2">
            <F label="Delimiter" error={err('delimiter')}>
              <select
                value={str('delimiter', 'comma')}
                onChange={(e) => onUpdate({ delimiter: e.target.value })}
                className={IC}
              >
                <option value="comma">Comma (,)</option>
                <option value="semicolon">Semicolon (;)</option>
                <option value="tab">Tab</option>
                <option value="pipe">Pipe (|)</option>
                <option value="custom">Custom</option>
              </select>
            </F>
            {str('delimiter', 'comma') === 'custom' && (
              <F label="Custom Delimiter" error={err('customDelimiter')}>
                <input
                  value={str('customDelimiter', ',')}
                  onChange={(e) => onUpdate({ customDelimiter: e.target.value })}
                  className={IC}
                  placeholder=","
                  maxLength={4}
                />
              </F>
            )}
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Encoding" error={err('encoding')}>
              <input
                value={str('encoding', 'UTF-8')}
                onChange={(e) => onUpdate({ encoding: e.target.value })}
                className={IC}
                placeholder="UTF-8"
              />
            </F>
            <F label="Start Cell" error={err('startCell')}>
              <input
                value={str('startCell', 'A1')}
                onChange={(e) => onUpdate({ startCell: e.target.value })}
                className={`${IC} font-mono`}
                placeholder="A1"
                spellCheck={false}
              />
            </F>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Quote Character">
              <input
                value={str('quoteChar', '"')}
                onChange={(e) => onUpdate({ quoteChar: e.target.value })}
                className={IC}
                placeholder='"'
                maxLength={2}
              />
            </F>
            <F label="Has Header">
              <label className="flex items-center gap-2 text-xs text-gray-700 cursor-pointer mt-1">
                <input
                  type="checkbox"
                  checked={op.hasHeader !== false && op.hasHeader !== 'false'}
                  onChange={(e) => onUpdate({ hasHeader: e.target.checked })}
                  className="rounded"
                />
                First row is a header
              </label>
            </F>
          </div>
          <div className="flex flex-wrap gap-4">
            <label className="flex items-center gap-2 text-xs text-gray-700 cursor-pointer">
              <input
                type="checkbox"
                checked={op.trimValues !== false && op.trimValues !== 'false'}
                onChange={(e) => onUpdate({ trimValues: e.target.checked })}
                className="rounded"
              />
              Trim Values
            </label>
            <label className="flex items-center gap-2 text-xs text-gray-700 cursor-pointer">
              <input
                type="checkbox"
                checked={op.parseNumbers === true || op.parseNumbers === 'true'}
                onChange={(e) => onUpdate({ parseNumbers: e.target.checked })}
                className="rounded"
              />
              Parse Numbers
            </label>
          </div>
        </div>
      );

    case 'transformColumn':
      return (
        <div className="space-y-2">
          <F label="Sheet Name" error={err('sheetName')}>
            <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} />
          </F>
          <div className="grid grid-cols-2 gap-2">
            <F label="Source Column" error={err('sourceColumn')}>
              <ColInput value={str('sourceColumn', 'B')} onChange={(v) => onUpdate({ sourceColumn: v })} error={err('sourceColumn')} />
            </F>
            <F label="Target Column" error={err('targetColumn')}>
              <ColInput value={str('targetColumn', 'C')} onChange={(v) => onUpdate({ targetColumn: v })} error={err('targetColumn')} />
            </F>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Start Row" error={err('startRow')}>
              <EditableNumberInput min={1} fallback={2} value={op.startRow} onValueChange={(startRow) => onUpdate({ startRow })} className={IC} />
            </F>
            <F label="Number Format">
              <input value={str('numberFormat', '#,##0.00')} onChange={(e) => onUpdate({ numberFormat: e.target.value })} className={IC} />
            </F>
          </div>
          <F label="Expression" error={err('expression')}>
            <TransformColumnExpressionField
              value={str('expression', 'toNumber(value) / 100')}
              onChange={(expression) => onUpdate({ expression })}
              onFocus={focus('expression')}
              hasError={Boolean(err('expression'))}
            />
          </F>
          <F label="Target Header">
            <input value={str('targetHeader', 'Converted Amount')} onChange={(e) => onUpdate({ targetHeader: e.target.value })} className={IC} />
          </F>
        </div>
      );

    case 'copyColumnValues':
      return (
        <div className="space-y-2">
          <div className="grid grid-cols-2 gap-2">
            <F label="Source Sheet" error={err('sourceSheet')}>
              <input value={str('sourceSheet', 'Temp')} onChange={(e) => onUpdate({ sourceSheet: e.target.value })} className={IC} />
            </F>
            <F label="Source Column" error={err('sourceColumn')}>
              <ColInput value={str('sourceColumn', 'B')} onChange={(v) => onUpdate({ sourceColumn: v })} error={err('sourceColumn')} />
            </F>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Target Sheet" error={err('targetSheet')}>
              <input value={str('targetSheet', 'Data')} onChange={(e) => onUpdate({ targetSheet: e.target.value })} className={IC} />
            </F>
            <F label="Target Column" error={err('targetColumn')}>
              <ColInput value={str('targetColumn', 'C')} onChange={(v) => onUpdate({ targetColumn: v })} error={err('targetColumn')} />
            </F>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Start Row" error={err('startRow')}>
              <EditableNumberInput min={1} fallback={2} value={op.startRow} onValueChange={(startRow) => onUpdate({ startRow })} className={IC} />
            </F>
            <F label="Include Header">
              <label className="flex items-center gap-2 text-xs text-gray-700 cursor-pointer mt-1">
                <input type="checkbox" checked={op.includeHeader === true || op.includeHeader === 'true'} onChange={(e) => onUpdate({ includeHeader: e.target.checked })} className="rounded" />
                Copy row 1 header
              </label>
            </F>
          </div>
        </div>
      );

    case 'replaceColumnValues':
      return (
        <div className="space-y-2">
          <F label="Sheet Name" error={err('sheetName')}>
            <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} />
          </F>
          <div className="grid grid-cols-2 gap-2">
            <F label="Source Column" error={err('sourceColumn')}>
              <ColInput value={str('sourceColumn', 'C')} onChange={(v) => onUpdate({ sourceColumn: v })} error={err('sourceColumn')} />
            </F>
            <F label="Target Column" error={err('targetColumn')}>
              <ColInput value={str('targetColumn', 'B')} onChange={(v) => onUpdate({ targetColumn: v })} error={err('targetColumn')} />
            </F>
          </div>
          <F label="Start Row" error={err('startRow')}>
            <EditableNumberInput min={1} fallback={2} value={op.startRow} onValueChange={(startRow) => onUpdate({ startRow })} className={IC} />
          </F>
        </div>
      );

    case 'calculateFormulaValues':
      return (
        <div className="space-y-2">
          <F label="Sheet Name" error={err('sheetName')}>
            <input value={str('sheetName', 'Temp')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} />
          </F>
          <div className="grid grid-cols-2 gap-2">
            <F label="Source Formula Column" error={err('sourceFormulaColumn')}>
              <ColInput value={str('sourceFormulaColumn', 'B')} onChange={(v) => onUpdate({ sourceFormulaColumn: v })} error={err('sourceFormulaColumn')} />
            </F>
            <F label="Target Column" error={err('targetColumn')}>
              <ColInput value={str('targetColumn', 'C')} onChange={(v) => onUpdate({ targetColumn: v })} error={err('targetColumn')} />
            </F>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Start Row" error={err('startRow')}>
              <EditableNumberInput min={1} fallback={2} value={op.startRow} onValueChange={(startRow) => onUpdate({ startRow })} className={IC} />
            </F>
            <F label="Number Format">
              <input value={str('numberFormat', '#,##0.00')} onChange={(e) => onUpdate({ numberFormat: e.target.value })} className={IC} />
            </F>
          </div>
        </div>
      );

    case 'lookupColumn':
      return (
        <div className="space-y-2">
          <div className="grid grid-cols-2 gap-2">
            <F label="Source Sheet" error={err('sourceSheet')}>
              <input value={str('sourceSheet', 'Data')} onChange={(e) => onUpdate({ sourceSheet: e.target.value })} className={IC} />
            </F>
            <F label="Lookup Column" error={err('lookupColumn')}>
              <ColInput value={str('lookupColumn', 'A')} onChange={(v) => onUpdate({ lookupColumn: v })} error={err('lookupColumn')} />
            </F>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Reference Sheet" error={err('referenceSheet')}>
              <input value={str('referenceSheet', 'Customers')} onChange={(e) => onUpdate({ referenceSheet: e.target.value })} className={IC} />
            </F>
            <F label="Reference Key Column" error={err('referenceKeyColumn')}>
              <ColInput value={str('referenceKeyColumn', 'A')} onChange={(v) => onUpdate({ referenceKeyColumn: v })} error={err('referenceKeyColumn')} />
            </F>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Reference Return Column" error={err('referenceReturnColumn')}>
              <ColInput value={str('referenceReturnColumn', 'B')} onChange={(v) => onUpdate({ referenceReturnColumn: v })} error={err('referenceReturnColumn')} />
            </F>
            <F label="Target Column" error={err('targetColumn')}>
              <ColInput value={str('targetColumn', 'C')} onChange={(v) => onUpdate({ targetColumn: v })} error={err('targetColumn')} />
            </F>
          </div>
          <F label="Target Header">
            <input value={str('targetHeader', 'Customer Name')} onChange={(e) => onUpdate({ targetHeader: e.target.value })} className={IC} />
          </F>
          <div className="grid grid-cols-2 gap-2">
            <F label="Start Row" error={err('startRow')}>
              <EditableNumberInput min={1} fallback={2} value={op.startRow} onValueChange={(startRow) => onUpdate({ startRow })} className={IC} />
            </F>
            <F label="Not Found Value">
              <input value={str('notFoundValue')} onChange={(e) => onUpdate({ notFoundValue: e.target.value })} className={IC} />
            </F>
          </div>
          <label className="flex items-center gap-2 text-xs text-gray-700 cursor-pointer">
            <input type="checkbox" checked={op.ignoreCase !== false && op.ignoreCase !== 'false'} onChange={(e) => onUpdate({ ignoreCase: e.target.checked })} className="rounded" />
            Ignore Case
          </label>
        </div>
      );

    case 'multiColumnLookup': {
      const mappings = Array.isArray(op.mappings)
        ? (op.mappings as Array<Record<string, unknown>>)
        : [{ referenceColumn: 'B', targetColumn: 'C', targetHeader: 'Customer Name' }];

      function updateMapping(i: number, patch: Record<string, unknown>) {
        onUpdate({ mappings: mappings.map((m, idx) => (idx === i ? { ...m, ...patch } : m)) });
      }

      return (
        <div className="space-y-2">
          <div className="grid grid-cols-2 gap-2">
            <F label="Source Sheet" error={err('sourceSheet')}>
              <input value={str('sourceSheet', 'Data')} onChange={(e) => onUpdate({ sourceSheet: e.target.value })} className={IC} />
            </F>
            <F label="Lookup Column" error={err('lookupColumn')}>
              <ColInput value={str('lookupColumn', 'A')} onChange={(v) => onUpdate({ lookupColumn: v })} error={err('lookupColumn')} />
            </F>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Reference Sheet" error={err('referenceSheet')}>
              <input value={str('referenceSheet', 'Customers')} onChange={(e) => onUpdate({ referenceSheet: e.target.value })} className={IC} />
            </F>
            <F label="Reference Key Column" error={err('referenceKeyColumn')}>
              <ColInput value={str('referenceKeyColumn', 'A')} onChange={(v) => onUpdate({ referenceKeyColumn: v })} error={err('referenceKeyColumn')} />
            </F>
          </div>
          <div>
            <div className="flex items-center justify-between mb-1">
              <p className="text-xs text-gray-500 font-medium">Column Mappings</p>
              <button
                type="button"
                onClick={() => onUpdate({ mappings: [...mappings, { referenceColumn: 'B', targetColumn: 'C', targetHeader: '' }] })}
                className="text-xs text-blue-600 hover:underline"
              >
                + Add mapping
              </button>
            </div>
            <FieldMsg errors={fieldErrors} field={opFieldKey(idx, 'mappings')} />
            <div className="space-y-1.5">
              {mappings.map((m, mi) => (
                <div key={mi} className="grid grid-cols-[1fr_1fr_1fr_auto] gap-1 items-end border border-gray-100 rounded p-1.5">
                  <F label="Ref Col">
                    <ColInput value={String(m.referenceColumn ?? 'B')} onChange={(v) => updateMapping(mi, { referenceColumn: v })} />
                  </F>
                  <F label="Target Col">
                    <ColInput value={String(m.targetColumn ?? 'C')} onChange={(v) => updateMapping(mi, { targetColumn: v })} />
                  </F>
                  <F label="Header">
                    <input value={String(m.targetHeader ?? '')} onChange={(e) => updateMapping(mi, { targetHeader: e.target.value })} className={IC} />
                  </F>
                  <button
                    type="button"
                    title="Remove mapping"
                    disabled={mappings.length <= 1}
                    onClick={() => onUpdate({ mappings: mappings.filter((_, j) => j !== mi) })}
                    className="btn btn-secondary btn-sm px-2 mb-0.5 disabled:opacity-30"
                  >
                    <Trash2 size={12} />
                  </button>
                </div>
              ))}
            </div>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Start Row" error={err('startRow')}>
              <EditableNumberInput min={1} fallback={2} value={op.startRow} onValueChange={(startRow) => onUpdate({ startRow })} className={IC} />
            </F>
            <F label="Ignore Case">
              <label className="flex items-center gap-2 text-xs text-gray-700 cursor-pointer mt-1">
                <input type="checkbox" checked={op.ignoreCase !== false && op.ignoreCase !== 'false'} onChange={(e) => onUpdate({ ignoreCase: e.target.checked })} className="rounded" />
                Case-insensitive match
              </label>
            </F>
          </div>
        </div>
      );
    }

    case 'compositeLookup':
      return (
        <div className="space-y-2">
          <F label="Source Sheet" error={err('sourceSheet')}>
            <input value={str('sourceSheet', 'Data')} onChange={(e) => onUpdate({ sourceSheet: e.target.value })} className={IC} />
          </F>
          <F label="Lookup Columns (comma-separated)" error={err('lookupColumns')}>
            <input
              value={Array.isArray(op.lookupColumns) ? op.lookupColumns.join(', ') : str('lookupColumns', 'A, B')}
              onChange={(e) => {
                const cols = e.target.value.split(',').map((c) => c.trim()).filter(Boolean);
                onUpdate({ lookupColumns: cols });
              }}
              className={IC}
              placeholder="A, B"
            />
          </F>
          <F label="Reference Sheet" error={err('referenceSheet')}>
            <input value={str('referenceSheet', 'Reference')} onChange={(e) => onUpdate({ referenceSheet: e.target.value })} className={IC} />
          </F>
          <F label="Reference Key Columns (comma-separated)" error={err('referenceKeyColumns')}>
            <input
              value={Array.isArray(op.referenceKeyColumns) ? op.referenceKeyColumns.join(', ') : str('referenceKeyColumns', 'A, B')}
              onChange={(e) => {
                const cols = e.target.value.split(',').map((c) => c.trim()).filter(Boolean);
                onUpdate({ referenceKeyColumns: cols });
              }}
              className={IC}
              placeholder="A, B"
            />
          </F>
          <div className="grid grid-cols-2 gap-2">
            <F label="Reference Return Column" error={err('referenceReturnColumn')}>
              <ColInput value={str('referenceReturnColumn', 'C')} onChange={(v) => onUpdate({ referenceReturnColumn: v })} error={err('referenceReturnColumn')} />
            </F>
            <F label="Target Column" error={err('targetColumn')}>
              <ColInput value={str('targetColumn', 'D')} onChange={(v) => onUpdate({ targetColumn: v })} error={err('targetColumn')} />
            </F>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Separator">
              <input value={str('separator', '|')} onChange={(e) => onUpdate({ separator: e.target.value })} className={IC} maxLength={4} />
            </F>
            <F label="Start Row" error={err('startRow')}>
              <EditableNumberInput min={1} fallback={2} value={op.startRow} onValueChange={(startRow) => onUpdate({ startRow })} className={IC} />
            </F>
          </div>
          <label className="flex items-center gap-2 text-xs text-gray-700 cursor-pointer">
            <input type="checkbox" checked={op.ignoreCase !== false && op.ignoreCase !== 'false'} onChange={(e) => onUpdate({ ignoreCase: e.target.checked })} className="rounded" />
            Ignore Case
          </label>
        </div>
      );

    case 'replaceWithLookupResult':
      return (
        <div className="space-y-2">
          <div className="grid grid-cols-2 gap-2">
            <F label="Source Sheet" error={err('sourceSheet')}>
              <input value={str('sourceSheet', 'Data')} onChange={(e) => onUpdate({ sourceSheet: e.target.value })} className={IC} />
            </F>
            <F label="Lookup Column" error={err('lookupColumn')}>
              <ColInput value={str('lookupColumn', 'A')} onChange={(v) => onUpdate({ lookupColumn: v })} error={err('lookupColumn')} />
            </F>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Reference Sheet" error={err('referenceSheet')}>
              <input value={str('referenceSheet', 'Customers')} onChange={(e) => onUpdate({ referenceSheet: e.target.value })} className={IC} />
            </F>
            <F label="Reference Key Column" error={err('referenceKeyColumn')}>
              <ColInput value={str('referenceKeyColumn', 'A')} onChange={(v) => onUpdate({ referenceKeyColumn: v })} error={err('referenceKeyColumn')} />
            </F>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Reference Return Column" error={err('referenceReturnColumn')}>
              <ColInput value={str('referenceReturnColumn', 'B')} onChange={(v) => onUpdate({ referenceReturnColumn: v })} error={err('referenceReturnColumn')} />
            </F>
            <F label="Target Column" error={err('targetColumn')}>
              <ColInput value={str('targetColumn', 'A')} onChange={(v) => onUpdate({ targetColumn: v })} error={err('targetColumn')} />
            </F>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Start Row" error={err('startRow')}>
              <EditableNumberInput min={1} fallback={2} value={op.startRow} onValueChange={(startRow) => onUpdate({ startRow })} className={IC} />
            </F>
            <F label="Ignore Case">
              <label className="flex items-center gap-2 text-xs text-gray-700 cursor-pointer mt-1">
                <input type="checkbox" checked={op.ignoreCase !== false && op.ignoreCase !== 'false'} onChange={(e) => onUpdate({ ignoreCase: e.target.checked })} className="rounded" />
                Case-insensitive match
              </label>
            </F>
          </div>
          <p className="text-xs text-gray-400">Replaces codes in the lookup column with resolved names (e.g. CustomerNo → CustomerName).</p>
        </div>
      );

    case 'insertColumn':
      return (
        <div className="space-y-2">
          <F label="Sheet Name" error={err('sheetName')}>
            <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} />
          </F>
          <div className="grid grid-cols-2 gap-2">
            <F label="After Column" error={err('afterColumn')}>
              <ColInput value={str('afterColumn', 'B')} onChange={(v) => onUpdate({ afterColumn: v })} error={err('afterColumn')} />
            </F>
            <F label="New Column" error={err('newColumn')}>
              <ColInput value={str('newColumn', 'C')} onChange={(v) => onUpdate({ newColumn: v })} error={err('newColumn')} />
            </F>
          </div>
          <F label="Header">
            <input value={str('header')} onChange={(e) => onUpdate({ header: e.target.value })} className={IC} />
          </F>
        </div>
      );

    case 'copyColumn':
      return (
        <div className="space-y-2">
          <div className="grid grid-cols-2 gap-2">
            <F label="Source Sheet" error={err('sourceSheet')}>
              <input value={str('sourceSheet', 'Data')} onChange={(e) => onUpdate({ sourceSheet: e.target.value })} className={IC} />
            </F>
            <F label="Source Column" error={err('sourceColumn')}>
              <ColInput value={str('sourceColumn', 'B')} onChange={(v) => onUpdate({ sourceColumn: v })} error={err('sourceColumn')} />
            </F>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <F label="Target Sheet" error={err('targetSheet')}>
              <input value={str('targetSheet', 'Data')} onChange={(e) => onUpdate({ targetSheet: e.target.value })} className={IC} />
            </F>
            <F label="Target Column" error={err('targetColumn')}>
              <ColInput value={str('targetColumn', 'C')} onChange={(v) => onUpdate({ targetColumn: v })} error={err('targetColumn')} />
            </F>
          </div>
          <F label="Target Header">
            <input value={str('targetHeader', 'Copied B')} onChange={(e) => onUpdate({ targetHeader: e.target.value })} className={IC} />
          </F>
        </div>
      );

    case 'convertColumnToNumber':
      return (
        <div className="space-y-2">
          <F label="Sheet Name" error={err('sheetName')}>
            <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} />
          </F>
          <div className="grid grid-cols-2 gap-2">
            <F label="Column" error={err('column')}>
              <ColInput value={str('column', 'C')} onChange={(v) => onUpdate({ column: v })} error={err('column')} />
            </F>
            <F label="Start Row" error={err('startRow')}>
              <EditableNumberInput min={1} fallback={2} value={op.startRow} onValueChange={(startRow) => onUpdate({ startRow })} className={IC} />
            </F>
          </div>
          <F label="Number Format">
            <input value={str('numberFormat', '#,##0.00')} onChange={(e) => onUpdate({ numberFormat: e.target.value })} className={IC} />
          </F>
        </div>
      );

    case 'setFormula':
      return (
        <div className="space-y-2">
          <F label="Sheet Name" error={err('sheetName')}>
            <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} />
          </F>
          <div className="grid grid-cols-2 gap-2">
            <F label="Cell" error={err('cell')}>
              <input value={str('cell', 'D2')} onChange={(e) => onUpdate({ cell: e.target.value })} className={`${IC} font-mono`} spellCheck={false} />
            </F>
            <F label="Formula" error={err('formula')}>
              <input value={str('formula', 'C2*2')} onChange={(e) => onUpdate({ formula: e.target.value })} className={`${IC} font-mono`} spellCheck={false} />
            </F>
          </div>
        </div>
      );

    case 'fillFormulaDown':
      return (
        <div className="space-y-2">
          <F label="Sheet Name" error={err('sheetName')}>
            <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} />
          </F>
          <div className="grid grid-cols-3 gap-2">
            <F label="Column" error={err('column')}>
              <ColInput value={str('column', 'D')} onChange={(v) => onUpdate({ column: v })} error={err('column')} />
            </F>
            <F label="Start Row" error={err('startRow')}>
              <EditableNumberInput min={1} fallback={2} value={op.startRow} onValueChange={(startRow) => onUpdate({ startRow })} className={IC} />
            </F>
            <F label="End Row (empty = last)" error={err('endRow')}>
              <EditableNumberInput min={1} fallback={0} value={op.endRow} onValueChange={(endRow) => onUpdate({ endRow: endRow || null })} className={IC} placeholder="last" />
            </F>
          </div>
          <F label="Formula Template ({'{row}'} placeholder)" error={err('formulaTemplate')}>
            <input value={str('formulaTemplate', 'C{row}*2')} onChange={(e) => onUpdate({ formulaTemplate: e.target.value })} className={`${IC} font-mono`} spellCheck={false} />
          </F>
        </div>
      );

    case 'setColumnFormat':
      return (
        <div className="space-y-2">
          <F label="Sheet Name" error={err('sheetName')}>
            <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} />
          </F>
          <div className="grid grid-cols-2 gap-2">
            <F label="Column" error={err('column')}>
              <ColInput value={str('column', 'C')} onChange={(v) => onUpdate({ column: v })} error={err('column')} />
            </F>
            <F label="Format" error={err('format')}>
              <input value={str('format', '#,##0.00')} onChange={(e) => onUpdate({ format: e.target.value })} className={IC} />
            </F>
          </div>
        </div>
      );

    case 'setHeader':
      return (
        <div className="space-y-2">
          <F label="Sheet Name" error={err('sheetName')}>
            <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} />
          </F>
          <div className="grid grid-cols-2 gap-2">
            <F label="Column" error={err('column')}>
              <ColInput value={str('column', 'C')} onChange={(v) => onUpdate({ column: v })} error={err('column')} />
            </F>
            <F label="Header" error={err('header')}>
              <input value={str('header', 'Amount')} onChange={(e) => onUpdate({ header: e.target.value })} className={IC} />
            </F>
          </div>
        </div>
      );

    case 'autoFitColumns':
      return (
        <F label="Sheet Name" error={err('sheetName')}>
          <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} />
        </F>
      );

    case 'createSheetFromColumns':
      return (
        <div className="space-y-2">
          <div className="grid grid-cols-2 gap-2">
            <F label="Source Sheet" error={err('sourceSheet')}>
              <input value={str('sourceSheet', 'Data')} onChange={(e) => onUpdate({ sourceSheet: e.target.value })} className={IC} />
            </F>
            <F label="Target Sheet" error={err('targetSheet')}>
              <input value={str('targetSheet', 'Result')} onChange={(e) => onUpdate({ targetSheet: e.target.value })} className={IC} />
            </F>
          </div>
          <F label="Columns (comma-separated)" error={err('columns')}>
            <input
              value={Array.isArray(op.columns) ? op.columns.join(', ') : str('columns', 'A, B, C')}
              onChange={(e) => {
                const cols = e.target.value.split(',').map((c) => c.trim()).filter(Boolean);
                onUpdate({ columns: cols });
              }}
              className={IC}
              placeholder="A, B, C"
            />
          </F>
        </div>
      );

    case 'setCellStyle':
      return (
        <div className="space-y-2">
          <F label="Sheet Name" error={err('sheetName')}>
            <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} />
          </F>
          <F label="Range" error={err('range')}>
            <input value={str('range', 'A1:D1')} onChange={(e) => onUpdate({ range: e.target.value })} className={`${IC} font-mono`} spellCheck={false} />
          </F>
          <div className="grid grid-cols-2 gap-2">
            <F label="Bold">
              <label className="flex items-center gap-2 text-xs text-gray-700 cursor-pointer">
                <input type="checkbox" checked={!!op.bold} onChange={(e) => onUpdate({ bold: e.target.checked })} className="rounded" />
                Apply bold
              </label>
            </F>
            <F label="Background Color">
              <input value={str('backgroundColor', '#D9EAF7')} onChange={(e) => onUpdate({ backgroundColor: e.target.value })} className={IC} placeholder="#D9EAF7" />
            </F>
          </div>
        </div>
      );

    case 'addSheet':
      return (
        <F label="Sheet Name" error={err('sheetName')}>
          <input value={str('sheetName', 'Result')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} placeholder="Result" />
        </F>
      );

    case 'deleteRow':
      return (
        <div className="grid grid-cols-2 gap-2">
          <F label="Sheet Name" error={err('sheetName')}>
            <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} placeholder="Data" />
          </F>
          <F label="Row Number" error={err('rowNumber')}>
            <EditableNumberInput min={1} fallback={1} value={op.rowNumber} onValueChange={(rowNumber) => onUpdate({ rowNumber })} className={IC} />
          </F>
        </div>
      );

    case 'multiplyColumn':
      return (
        <div className="space-y-2">
          <div className="grid grid-cols-2 gap-2">
            <F label="Source Sheet" error={err('sourceSheet')}>
              <input value={str('sourceSheet', 'Data')} onChange={(e) => onUpdate({ sourceSheet: e.target.value })} className={IC} />
            </F>
            <F label="Source Column" error={err('sourceColumn')}>
              <ColInput value={str('sourceColumn', 'C')} onChange={(v) => onUpdate({ sourceColumn: v })} error={err('sourceColumn')} />
            </F>
          </div>
          <F label="Factor">
            <EditableNumberInput parseAs="float" step="any" fallback={2} value={op.factor} onValueChange={(factor) => onUpdate({ factor })} className={`${IC} w-24`} />
          </F>
          <div className="grid grid-cols-2 gap-2">
            <F label="Target Sheet" error={err('targetSheet')}>
              <input value={str('targetSheet', 'Result')} onChange={(e) => onUpdate({ targetSheet: e.target.value })} className={IC} />
            </F>
            <F label="Target Column" error={err('targetColumn')}>
              <ColInput value={str('targetColumn', 'A')} onChange={(v) => onUpdate({ targetColumn: v })} error={err('targetColumn')} />
            </F>
          </div>
          <F label="Target Header">
            <input value={str('targetHeader', 'C x 2')} onChange={(e) => onUpdate({ targetHeader: e.target.value })} className={IC} />
          </F>
        </div>
      );

    case 'filterRows':
      return (
        <FilterRowsFields
          op={op}
          fieldErrors={fieldErrors}
          onUpdate={onUpdate}
          str={str}
          err={err}
          focus={focus}
        />
      );

    case 'sortRows':
      return (
        <SortRowsFields
          op={op}
          fieldErrors={fieldErrors}
          onUpdate={onUpdate}
          str={str}
          err={err}
        />
      );

    case 'removeDuplicates':
      return (
        <RemoveDuplicatesFields
          op={op}
          onUpdate={onUpdate}
          str={str}
          err={err}
        />
      );

    case 'removeEmptyRows':
      return (
        <div className="grid grid-cols-2 gap-2">
          <F label="Sheet Name" error={err('sheetName')}>
            <input value={str('sheetName', 'Data')} onChange={(e) => onUpdate({ sheetName: e.target.value })} className={IC} placeholder="Data" />
          </F>
          <F label="Start Row" error={err('startRow')}>
            <EditableNumberInput min={1} fallback={2} value={op.startRow} onValueChange={(startRow) => onUpdate({ startRow })} className={IC} />
          </F>
        </div>
      );

    default:
      return <p className="text-xs text-gray-400 italic">Unknown operation: {String(op.type)}</p>;
  }
}

// ── Main editor ───────────────────────────────────────────────────────────────

function FieldMsg({ errors, field }: { errors?: Record<string, string>; field: string }) {
  const msg = errors?.[field];
  return msg ? <p className="text-xs text-red-600 mt-1">⚠ {msg}</p> : null;
}

export default function ExcelTransformEditor({ config, onChange, onFocusField, fieldErrors }: EditorProps) {
  const inputArtifactName = String(config.inputArtifactName ?? '');
  const outputName        = String(config.outputName        ?? 'transformed-excel');
  const operations: Op[]  = Array.isArray(config.operations) ? (config.operations as Op[]) : [];

  const [addType, setAddType] = useState<OpType>('insertColumn');

  function setOps(newOps: Op[]) {
    onChange({ ...config, operations: newOps });
  }

  function addOp() {
    setOps([...operations, { type: addType, ...OP_DEFAULTS[addType] }]);
  }

  function removeOp(idx: number) {
    setOps(operations.filter((_, i) => i !== idx));
  }

  function moveUp(idx: number) {
    if (idx === 0) return;
    const ops = [...operations];
    [ops[idx - 1], ops[idx]] = [ops[idx], ops[idx - 1]];
    setOps(ops);
  }

  function moveDown(idx: number) {
    if (idx >= operations.length - 1) return;
    const ops = [...operations];
    [ops[idx], ops[idx + 1]] = [ops[idx + 1], ops[idx]];
    setOps(ops);
  }

  function patchOp(idx: number, patch: Op) {
    setOps(operations.map((op, i) => (i === idx ? { ...op, ...patch } : op)));
  }

  return (
    <div className="space-y-3">

      <div>
        <label className="label">Input Artifact Name *</label>
        <input
          value={inputArtifactName}
          onChange={(e) => onChange({ ...config, inputArtifactName: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'inputArtifactName')}
          className={`input font-mono text-xs ${fieldErrors?.inputArtifactName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="source-file"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="inputArtifactName" />
        {!fieldErrors?.inputArtifactName && (
          <p className="text-xs text-gray-400 mt-1">
            Artifact produced by an upstream step. Use the variable picker to insert.
          </p>
        )}
      </div>

      <div>
        <label className="label">Output Name *</label>
        <input
          value={outputName}
          onChange={(e) => onChange({ ...config, outputName: e.target.value })}
          onFocus={(e) => onFocusField(e.currentTarget, 'outputName')}
          className={`input font-mono text-xs ${fieldErrors?.outputName ? 'border-red-400 focus:ring-red-400' : ''}`}
          placeholder="transformed-excel"
          spellCheck={false}
        />
        <FieldMsg errors={fieldErrors} field="outputName" />
        {!fieldErrors?.outputName && (
          <p className="text-xs text-gray-400 mt-1">
            Output artifact:{' '}
            <code className="bg-gray-100 px-0.5 rounded">{outputName || '…'}.xlsx</code>
          </p>
        )}
      </div>

      <div>
        <div className="flex items-center justify-between mb-2">
          <label className={`label mb-0 ${fieldErrors?.operations ? 'text-red-700' : ''}`}>
            Operations
            <span className="ml-1.5 text-gray-400 font-normal">({operations.length})</span>
          </label>
        </div>

        <FieldMsg errors={fieldErrors} field="operations" />

        <div className="space-y-2">
          {operations.length === 0 && (
            <div className={`border border-dashed rounded-lg px-3 py-3 text-center ${fieldErrors?.operations ? 'border-red-300 bg-red-50' : 'border-gray-200'}`}>
              <p className={`text-xs ${fieldErrors?.operations ? 'text-red-600' : 'text-gray-400'}`}>No operations yet — add one below.</p>
            </div>
          )}

          {operations.map((op, idx) => {
            const type  = (op.type ?? '') as OpType;
            const color = OP_COLORS[type] ?? '#6b7280';
            const label = OP_LABELS[type] ?? String(type);

            return (
              <div key={idx} className="rounded-lg border border-gray-200 overflow-hidden shadow-sm">
                <div
                  className="flex items-start justify-between px-2.5 py-1.5 gap-2"
                  style={{ background: color }}
                >
                  <div className="min-w-0">
                    <p className="text-xs font-bold text-white leading-tight">
                      {idx + 1}. {label}
                    </p>
                    <p className="text-xs text-white/70 leading-tight truncate mt-0.5">
                      {opSummary(op)}
                    </p>
                  </div>
                  <div className="flex gap-0.5 shrink-0">
                    <button
                      onClick={() => moveUp(idx)}
                      disabled={idx === 0}
                      title="Move up"
                      className="h-5 w-5 rounded flex items-center justify-center text-white/70 hover:text-white hover:bg-white/20 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                    >
                      <ArrowUp size={10} />
                    </button>
                    <button
                      onClick={() => moveDown(idx)}
                      disabled={idx === operations.length - 1}
                      title="Move down"
                      className="h-5 w-5 rounded flex items-center justify-center text-white/70 hover:text-white hover:bg-white/20 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                    >
                      <ArrowDown size={10} />
                    </button>
                    <button
                      onClick={() => { if (confirm(`Remove operation ${idx + 1}?`)) removeOp(idx); }}
                      title="Delete operation"
                      className="h-5 w-5 rounded flex items-center justify-center text-white/70 hover:text-white hover:bg-black/20 transition-colors"
                    >
                      <Trash2 size={10} />
                    </button>
                  </div>
                </div>

                <div className="px-3 py-2.5 bg-white">
                  <OpFields
                    op={op}
                    idx={idx}
                    fieldErrors={fieldErrors}
                    onUpdate={(patch) => patchOp(idx, patch)}
                    onFocusField={onFocusField}
                  />
                </div>
              </div>
            );
          })}
        </div>

        <div className="flex gap-2 mt-2">
          <select
            value={addType}
            onChange={(e) => setAddType(e.target.value as OpType)}
            className="input text-xs flex-1"
          >
            {OP_TYPES.map((t) => (
              <option key={t} value={t}>{OP_LABELS[t]}</option>
            ))}
          </select>
          <button onClick={addOp} className="btn btn-primary btn-sm shrink-0">
            <Plus size={12} /> Add
          </button>
        </div>
      </div>
    </div>
  );
}
