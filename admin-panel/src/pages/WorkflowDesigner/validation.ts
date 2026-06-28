import type { Node, Edge } from 'reactflow';
import type { WorkflowNodeData } from './types';

// ── Public types ───────────────────────────────────────────────────────────────

export interface ValidationError {
  nodeId: string;
  nodeName: string;
  /** Config-level field key (dot-notation for nested, e.g. "condition.left"). */
  field?: string;
  message: string;
}

export interface ValidationWarning {
  nodeId: string;
  nodeName: string;
  field?: string;
  message: string;
}

export interface ValidationResult {
  errors: ValidationError[];
  warnings: ValidationWarning[];
}

// ── Helpers ────────────────────────────────────────────────────────────────────

function strVal(v: unknown): string {
  return typeof v === 'string' ? v.trim() : String(v ?? '').trim();
}

function isEmpty(v: unknown): boolean {
  return v === undefined || v === null || strVal(v) === '';
}

function parseColumnList(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value.map((item) => strVal(item)).filter(Boolean);
  }
  if (typeof value === 'string') {
    return value.split(',').map((s) => s.trim()).filter(Boolean);
  }
  return [];
}

/** Foreach loop-back edge — the only edge type excluded from graph analysis. */
function isLoopBackEdge(e: Edge): boolean {
  return e.targetHandle === 'loop-back';
}

/** Append target to adj[src] once (avoid duplicate-edge false cycle positives). */
function addEdge(
  adj: Map<string, string[]>,
  src: string,
  tgt: string,
): void {
  const list = adj.get(src);
  if (!list) return;
  if (!list.includes(tgt)) list.push(tgt);
}

/**
 * Build adjacency lists for graph validation.
 * - cycle/reach forward (adjOut): all edges except loop-back
 * - root detection (adjIn): all incoming except loop-back (keeps Mail Receive → ForEach input)
 */
function buildGraphAdjacency(
  nodes: Node<WorkflowNodeData>[],
  edges: Edge[],
): { adjOut: Map<string, string[]>; adjIn: Map<string, string[]> } {
  const adjOut = new Map<string, string[]>();
  const adjIn  = new Map<string, string[]>();
  for (const n of nodes) {
    adjOut.set(n.id, []);
    adjIn.set(n.id, []);
  }
  for (const e of edges) {
    if (isLoopBackEdge(e)) continue;
    addEdge(adjOut, e.source, e.target);
    addEdge(adjIn, e.target, e.source);
  }
  return { adjOut, adjIn };
}

function isValidColumn(v: unknown): boolean {
  const s = strVal(v);
  if (!s) return false;
  if (/^\d+$/.test(s)) return parseInt(s, 10) >= 1;
  return /^[A-Za-z]+$/.test(s);
}

function isValidStartRow(v: unknown): boolean {
  if (v === undefined || v === null || v === '') return true;
  const n = typeof v === 'number' ? v : parseInt(String(v), 10);
  return !Number.isNaN(n) && n >= 1;
}

function reqOpField(
  nodeId: string,
  nodeName: string,
  opIdx: number,
  opLabel: string,
  field: string,
  label: string,
  value: unknown,
  addErr: (nodeId: string, nodeName: string, message: string, field?: string) => void,
): void {
  if (isEmpty(value)) {
    addErr(nodeId, nodeName, `${opLabel}: ${label} is required`, `operations.${opIdx}.${field}`);
  }
}

function validateExcelTransformOperations(
  nodeId: string,
  nodeName: string,
  ops: Record<string, unknown>[],
  addErr: (nodeId: string, nodeName: string, message: string, field?: string) => void,
): void {
  const opLabel = (idx: number, type: string) => `Operation ${idx + 1} (${type})`;

  ops.forEach((op, idx) => {
    const type = strVal(op.type);
    if (!type) {
      addErr(nodeId, nodeName, `Operation ${idx + 1}: type is required`, `operations.${idx}.type`);
      return;
    }

    const label = opLabel(idx, type);
    const reqCol = (field: string, fieldLabel: string) => {
      reqOpField(nodeId, nodeName, idx, label, field, fieldLabel, op[field], addErr);
      if (!isEmpty(op[field]) && !isValidColumn(op[field])) {
        addErr(nodeId, nodeName, `${label}: ${fieldLabel} must be a column letter or number`, `operations.${idx}.${field}`);
      }
    };
    const reqStartRow = (field = 'startRow') => {
      if (!isValidStartRow(op[field])) {
        addErr(nodeId, nodeName, `${label}: Start Row must be at least 1`, `operations.${idx}.${field}`);
      }
    };

    switch (type) {
      case 'importTextToSheet':
        reqOpField(nodeId, nodeName, idx, label, 'textArtifactName', 'Text Artifact Name', op.textArtifactName, addErr);
        reqOpField(nodeId, nodeName, idx, label, 'targetSheet', 'Target Sheet', op.targetSheet, addErr);
        reqOpField(nodeId, nodeName, idx, label, 'delimiter', 'Delimiter', op.delimiter, addErr);
        if (strVal(op.delimiter) === 'custom' && isEmpty(op.customDelimiter)) {
          addErr(nodeId, nodeName, `${label}: Custom Delimiter is required`, `operations.${idx}.customDelimiter`);
        }
        reqOpField(nodeId, nodeName, idx, label, 'encoding', 'Encoding', op.encoding, addErr);
        reqOpField(nodeId, nodeName, idx, label, 'startCell', 'Start Cell', op.startCell, addErr);
        break;
      case 'transformColumn':
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqCol('sourceColumn', 'Source Column');
        reqCol('targetColumn', 'Target Column');
        reqOpField(nodeId, nodeName, idx, label, 'expression', 'Expression', op.expression, addErr);
        reqStartRow();
        break;
      case 'copyColumnValues':
        reqOpField(nodeId, nodeName, idx, label, 'sourceSheet', 'Source Sheet', op.sourceSheet, addErr);
        reqCol('sourceColumn', 'Source Column');
        reqOpField(nodeId, nodeName, idx, label, 'targetSheet', 'Target Sheet', op.targetSheet, addErr);
        reqCol('targetColumn', 'Target Column');
        reqStartRow();
        break;
      case 'replaceColumnValues':
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqCol('sourceColumn', 'Source Column');
        reqCol('targetColumn', 'Target Column');
        reqStartRow();
        break;
      case 'calculateFormulaValues':
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqCol('sourceFormulaColumn', 'Source Formula Column');
        reqCol('targetColumn', 'Target Column');
        reqStartRow();
        break;
      case 'lookupColumn':
        reqOpField(nodeId, nodeName, idx, label, 'sourceSheet', 'Source Sheet', op.sourceSheet, addErr);
        reqCol('lookupColumn', 'Lookup Column');
        reqOpField(nodeId, nodeName, idx, label, 'referenceSheet', 'Reference Sheet', op.referenceSheet, addErr);
        reqCol('referenceKeyColumn', 'Reference Key Column');
        reqCol('referenceReturnColumn', 'Reference Return Column');
        reqCol('targetColumn', 'Target Column');
        reqStartRow();
        break;
      case 'multiColumnLookup':
        reqOpField(nodeId, nodeName, idx, label, 'sourceSheet', 'Source Sheet', op.sourceSheet, addErr);
        reqCol('lookupColumn', 'Lookup Column');
        reqOpField(nodeId, nodeName, idx, label, 'referenceSheet', 'Reference Sheet', op.referenceSheet, addErr);
        reqCol('referenceKeyColumn', 'Reference Key Column');
        {
          const mappings = op.mappings;
          if (!Array.isArray(mappings) || mappings.length === 0) {
            addErr(nodeId, nodeName, `${label}: at least one mapping is required`, `operations.${idx}.mappings`);
          }
        }
        reqStartRow();
        break;
      case 'compositeLookup':
        reqOpField(nodeId, nodeName, idx, label, 'sourceSheet', 'Source Sheet', op.sourceSheet, addErr);
        reqOpField(nodeId, nodeName, idx, label, 'referenceSheet', 'Reference Sheet', op.referenceSheet, addErr);
        reqCol('referenceReturnColumn', 'Reference Return Column');
        reqCol('targetColumn', 'Target Column');
        {
          const lookupCols = op.lookupColumns;
          const emptyLookup = lookupCols === undefined || lookupCols === null
            || (Array.isArray(lookupCols) && lookupCols.length === 0)
            || (typeof lookupCols === 'string' && strVal(lookupCols) === '');
          if (emptyLookup) {
            addErr(nodeId, nodeName, `${label}: at least one lookup column is required`, `operations.${idx}.lookupColumns`);
          }
          const refKeyCols = op.referenceKeyColumns;
          const emptyRef = refKeyCols === undefined || refKeyCols === null
            || (Array.isArray(refKeyCols) && refKeyCols.length === 0)
            || (typeof refKeyCols === 'string' && strVal(refKeyCols) === '');
          if (emptyRef) {
            addErr(nodeId, nodeName, `${label}: at least one reference key column is required`, `operations.${idx}.referenceKeyColumns`);
          }
        }
        reqStartRow();
        break;
      case 'replaceWithLookupResult':
        reqOpField(nodeId, nodeName, idx, label, 'sourceSheet', 'Source Sheet', op.sourceSheet, addErr);
        reqCol('lookupColumn', 'Lookup Column');
        reqOpField(nodeId, nodeName, idx, label, 'referenceSheet', 'Reference Sheet', op.referenceSheet, addErr);
        reqCol('referenceKeyColumn', 'Reference Key Column');
        reqCol('referenceReturnColumn', 'Reference Return Column');
        reqCol('targetColumn', 'Target Column');
        reqStartRow();
        break;
      case 'insertColumn':
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqCol('afterColumn', 'After Column');
        reqCol('newColumn', 'New Column');
        break;
      case 'copyColumn':
        reqOpField(nodeId, nodeName, idx, label, 'sourceSheet', 'Source Sheet', op.sourceSheet, addErr);
        reqCol('sourceColumn', 'Source Column');
        reqOpField(nodeId, nodeName, idx, label, 'targetSheet', 'Target Sheet', op.targetSheet, addErr);
        reqCol('targetColumn', 'Target Column');
        break;
      case 'convertColumnToNumber':
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqCol('column', 'Column');
        reqStartRow();
        break;
      case 'setFormula':
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqOpField(nodeId, nodeName, idx, label, 'cell', 'Cell', op.cell, addErr);
        reqOpField(nodeId, nodeName, idx, label, 'formula', 'Formula', op.formula, addErr);
        break;
      case 'fillFormulaDown':
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqCol('column', 'Column');
        reqOpField(nodeId, nodeName, idx, label, 'startRow', 'Start Row', op.startRow, addErr);
        reqStartRow();
        if (op.endRow !== undefined && op.endRow !== null && op.endRow !== '' && !isValidStartRow(op.endRow)) {
          addErr(nodeId, nodeName, `${label}: End Row must be at least 1`, `operations.${idx}.endRow`);
        }
        reqOpField(nodeId, nodeName, idx, label, 'formulaTemplate', 'Formula Template', op.formulaTemplate, addErr);
        break;
      case 'setColumnFormat':
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqCol('column', 'Column');
        reqOpField(nodeId, nodeName, idx, label, 'format', 'Format', op.format, addErr);
        break;
      case 'setHeader':
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqCol('column', 'Column');
        reqOpField(nodeId, nodeName, idx, label, 'header', 'Header', op.header, addErr);
        break;
      case 'autoFitColumns':
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        break;
      case 'createSheetFromColumns':
        reqOpField(nodeId, nodeName, idx, label, 'sourceSheet', 'Source Sheet', op.sourceSheet, addErr);
        reqOpField(nodeId, nodeName, idx, label, 'targetSheet', 'Target Sheet', op.targetSheet, addErr);
        {
          const cols = op.columns;
          const empty = cols === undefined || cols === null
            || (Array.isArray(cols) && cols.length === 0)
            || (typeof cols === 'string' && strVal(cols) === '');
          if (empty) {
            addErr(nodeId, nodeName, `${label}: at least one column is required`, `operations.${idx}.columns`);
          } else if (Array.isArray(cols)) {
            cols.forEach((c, ci) => {
              if (!isValidColumn(c)) {
                addErr(nodeId, nodeName, `${label}: column ${ci + 1} is invalid`, `operations.${idx}.columns`);
              }
            });
          }
        }
        break;
      case 'setCellStyle':
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqOpField(nodeId, nodeName, idx, label, 'range', 'Range', op.range, addErr);
        break;
      case 'addSheet':
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        break;
      case 'deleteRow':
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqOpField(nodeId, nodeName, idx, label, 'rowNumber', 'Row Number', op.rowNumber, addErr);
        break;
      case 'multiplyColumn':
        reqOpField(nodeId, nodeName, idx, label, 'sourceSheet', 'Source Sheet', op.sourceSheet, addErr);
        reqCol('sourceColumn', 'Source Column');
        reqOpField(nodeId, nodeName, idx, label, 'targetSheet', 'Target Sheet', op.targetSheet, addErr);
        reqCol('targetColumn', 'Target Column');
        break;
      case 'filterRows': {
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqOpField(nodeId, nodeName, idx, label, 'column', 'Column', op.column, addErr);
        reqStartRow();
        const mode = strVal(op.mode ?? 'keep').toLowerCase();
        if (mode !== 'keep' && mode !== 'remove') {
          addErr(nodeId, nodeName, `${label}: Mode must be 'keep' or 'remove'`, `operations.${idx}.mode`);
        }
        const join = strVal(op.conditionJoin ?? 'and').toLowerCase();
        if (join !== 'and' && join !== 'or') {
          addErr(nodeId, nodeName, `${label}: Condition Join must be 'and' or 'or'`, `operations.${idx}.conditionJoin`);
        }
        const conditions = op.conditions;
        if (!Array.isArray(conditions) || conditions.length === 0) {
          addErr(nodeId, nodeName, `${label}: at least one condition is required`, `operations.${idx}.conditions`);
        } else {
          const valueOps = new Set([
            'equals', 'notequals', 'contains', 'notcontains', 'startswith', 'endswith',
            'greaterthan', 'greaterorequal', 'lessthan', 'lessorequal',
          ]);
          const valuesOps = new Set(['in', 'notin']);
          conditions.forEach((rawCond, ci) => {
            const cond = rawCond as Record<string, unknown>;
            const operator = strVal(cond.operator).toLowerCase();
            if (!operator) {
              addErr(nodeId, nodeName, `${label}: condition ${ci + 1} operator is required`, `operations.${idx}.conditions.${ci}.operator`);
              return;
            }
            if (valueOps.has(operator) && isEmpty(cond.value)) {
              addErr(nodeId, nodeName, `${label}: condition ${ci + 1} value is required`, `operations.${idx}.conditions.${ci}.value`);
            }
            if (valuesOps.has(operator)) {
              const vals = cond.values;
              const emptyVals = vals === undefined || vals === null
                || (Array.isArray(vals) && vals.length === 0)
                || (typeof vals === 'string' && strVal(vals) === '');
              if (emptyVals) {
                addErr(nodeId, nodeName, `${label}: condition ${ci + 1} values are required`, `operations.${idx}.conditions.${ci}.values`);
              }
            }
          });
        }
        break;
      }
      case 'sortRows': {
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqStartRow();
        const sorts = op.sorts;
        if (!Array.isArray(sorts) || sorts.length === 0) {
          addErr(nodeId, nodeName, `${label}: at least one sort key is required`, `operations.${idx}.sorts`);
        } else {
          const validDataTypes = new Set(['text', 'number', 'date', 'auto']);
          sorts.forEach((rawSort, si) => {
            const sort = rawSort as Record<string, unknown>;
            if (isEmpty(sort.column)) {
              addErr(nodeId, nodeName, `${label}: sort ${si + 1} column is required`, `operations.${idx}.sorts.${si}.column`);
            }
            const direction = strVal(sort.direction ?? 'asc').toLowerCase();
            if (direction !== 'asc' && direction !== 'desc') {
              addErr(nodeId, nodeName, `${label}: sort ${si + 1} direction must be asc or desc`, `operations.${idx}.sorts.${si}.direction`);
            }
            const dataType = strVal(sort.dataType ?? 'auto').toLowerCase();
            if (!validDataTypes.has(dataType)) {
              addErr(nodeId, nodeName, `${label}: sort ${si + 1} data type must be text, number, date, or auto`, `operations.${idx}.sorts.${si}.dataType`);
            }
          });
        }
        break;
      }
      case 'removeDuplicates': {
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqStartRow();
        const keep = strVal(op.keep ?? 'first').toLowerCase();
        if (keep !== 'first' && keep !== 'last') {
          addErr(nodeId, nodeName, `${label}: Keep must be 'first' or 'last'`, `operations.${idx}.keep`);
        }
        const cols = op.columns;
        const emptyCols = cols === undefined || cols === null
          || (Array.isArray(cols) && cols.length === 0)
          || (typeof cols === 'string' && strVal(cols) === '');
        if (emptyCols) {
          addErr(nodeId, nodeName, `${label}: at least one column is required`, `operations.${idx}.columns`);
        } else if (Array.isArray(cols)) {
          cols.forEach((c, ci) => {
            if (isEmpty(c)) {
              addErr(nodeId, nodeName, `${label}: column ${ci + 1} must not be empty`, `operations.${idx}.columns.${ci}`);
            }
          });
        }
        break;
      }
      case 'removeEmptyRows':
        reqOpField(nodeId, nodeName, idx, label, 'sheetName', 'Sheet Name', op.sheetName, addErr);
        reqStartRow();
        break;
      default:
        break;
    }
  });
}

// ── Main validator ─────────────────────────────────────────────────────────────

export function validateWorkflow(
  nodes: Node<WorkflowNodeData>[],
  edges: Edge[],
): ValidationResult {
  const errors:   ValidationError[]   = [];
  const warnings: ValidationWarning[] = [];

  const addErr  = (nodeId: string, nodeName: string, message: string, field?: string) =>
    errors.push({ nodeId, nodeName, message, field });
  const addWarn = (nodeId: string, nodeName: string, message: string, field?: string) =>
    warnings.push({ nodeId, nodeName, message, field });

  // ── 1. Per-step required-field rules ────────────────────────────────────────
  for (const node of nodes) {
    const { id } = node;
    const { stepType, config, name } = node.data;

    /** Adds an error if config[field] is empty. */
    const req = (field: string, label: string) => {
      if (isEmpty(config[field])) addErr(id, name, `${label} is required`, field);
    };

    switch (stepType) {
      case 'folder.read-file':
        req('sourcePath',   'Source Path');
        req('artifactName', 'Artifact Name');
        break;

      case 'folder.write-file':
        req('artifactName',    'Artifact Name');
        req('destinationPath', 'Destination Path');
        break;

      case 'folder.list-files':
        req('folderPath',     'Folder Path');
        req('outputVariable', 'Output Variable');
        break;

      case 'csv.read': {
        req('inputArtifactName', 'Input Artifact Name');
        req('outputVariable',    'Output Variable');
        const delim = strVal(config.delimiter ?? ',');
        if (!delim) addErr(id, name, 'Delimiter is required', 'delimiter');
        if (delim.length > 1) addErr(id, name, 'Delimiter must be a single character', 'delimiter');
        break;
      }

      case 'csv.write': {
        req('sourceVariable', 'Source Variable');
        req('outputName',     'Output Name');
        const delim = strVal(config.delimiter ?? ',');
        if (!delim) addErr(id, name, 'Delimiter is required', 'delimiter');
        if (delim.length > 1) addErr(id, name, 'Delimiter must be a single character', 'delimiter');
        break;
      }

      case 'json.read-file': {
        req('inputArtifactName', 'Input Artifact Name');
        req('outputVariable',    'Output Variable');
        req('outputMode',        'Output Mode');
        const mode = strVal(config.outputMode ?? 'json').toLowerCase();
        if (mode && mode !== 'value' && mode !== 'json' && mode !== 'table') {
          addErr(id, name, "Output Mode must be 'value', 'json', or 'table'", 'outputMode');
        }
        break;
      }

      case 'json.write-file': {
        req('sourceVariable', 'Source Variable');
        req('outputName',     'Output Name');
        break;
      }

      case 'zip.extract': {
        req('inputArtifactName', 'Input Artifact Name');
        req('outputPrefix',      'Output Prefix');
        req('outputVariable',    'Output Variable');
        break;
      }

      case 'zip.create': {
        req('outputName', 'Output Name');
        {
          const names = config.inputArtifactNames;
          const empty = names === undefined || names === null
            || (typeof names === 'string' && strVal(names) === '')
            || (Array.isArray(names) && names.length === 0);
          if (empty) addErr(id, name, 'Input Artifact Names is required', 'inputArtifactNames');
        }
        const level = strVal(config.compressionLevel ?? 'optimal').toLowerCase();
        if (level && level !== 'fastest' && level !== 'optimal' && level !== 'nocompression') {
          addErr(id, name, "Compression Level must be 'fastest', 'optimal', or 'noCompression'", 'compressionLevel');
        }
        break;
      }

      case 'pdf.read-text': {
        req('inputArtifactName', 'Input Artifact Name');
        req('outputVariable',    'Output Variable');
        break;
      }

      case 'pdf.extract-table': {
        req('inputArtifactName', 'Input Artifact Name');
        req('outputVariable',    'Output Variable');
        const idx = config.tableIndex;
        if (idx !== undefined && idx !== null && idx !== '') {
          const n = Number(idx);
          if (Number.isNaN(n) || n < 0) {
            addErr(id, name, 'Table Index must be >= 0', 'tableIndex');
          }
        }
        const mode = strVal(config.parserMode ?? 'auto').toLowerCase();
        if (!mode) {
          addErr(id, name, 'Parser Mode is required', 'parserMode');
        } else if (mode === 'delimiter' && isEmpty(config.delimiter)) {
          addErr(id, name, 'Delimiter is required when Parser Mode is Delimiter', 'delimiter');
        }
        break;
      }

      case 'word.fill-template': {
        req('inputArtifactName', 'Input Artifact Name');
        req('outputName',        'Output Name');
        req('outputVariable',    'Output Variable');
        break;
      }

      case 'mail.send':
        req('to',      'Recipient (To)');
        req('subject', 'Subject');
        if (isEmpty(config.attachments)) {
          addWarn(id, name, 'No attachment configured — add an artifact name or variable in the Attachments field', 'attachments');
        }
        break;

      case 'mail.reply':
        req('messageId', 'Message ID');
        break;

      case 'mail.forward':
        req('messageId', 'Message ID');
        req('to',        'Recipient (To)');
        break;

      case 'mail.move':
        req('messageId',    'Message ID');
        req('targetFolder', 'Target Folder');
        break;

      case 'mail.mark':
        req('messageId', 'Message ID');
        break;

      case 'mail.delete':
        req('messageId', 'Message ID');
        break;

      case 'mail.get-body': {
        req('outputVariable', 'Output Variable');
        const bt = strVal(config.bodyType ?? 'text').toLowerCase();
        if (bt !== 'text' && bt !== 'html') {
          addErr(id, name, "Body Type must be 'text' or 'html'", 'bodyType');
        }
        break;
      }

      case 'mail.extract-value': {
        req('sourceVariable', 'Source Variable');
        req('outputVariable', 'Output Variable');
        const hasLabel   = !isEmpty(config.label);
        const hasPattern = !isEmpty(config.pattern);
        if (!hasLabel && !hasPattern) {
          addErr(id, name, 'Either Label or Pattern is required', 'extraction');
        }
        if (hasLabel && hasPattern) {
          addErr(id, name, 'Use Label or Pattern, not both', 'extraction');
        }
        break;
      }

      case 'mail.extract-table': {
        req('sourceVariable', 'Source Variable');
        req('outputVariable', 'Output Variable');
        {
          const ti = config.tableIndex ?? 0;
          const tableIdx = typeof ti === 'number' ? ti : parseInt(String(ti ?? '0'), 10);
          if (Number.isNaN(tableIdx) || tableIdx < 0) {
            addErr(id, name, 'Table Index must be 0 or greater', 'tableIndex');
          }
        }
        const mode = strVal(config.mode ?? 'cell').toLowerCase();
        if (!['cell', 'headercell', 'lookup', 'tablejson'].includes(mode)) {
          addErr(id, name, "Mode must be 'cell', 'headerCell', 'lookup', or 'tableJson'", 'mode');
        }
        if (mode === 'cell') {
          if (config.rowIndex === undefined || config.rowIndex === '') {
            addErr(id, name, 'Row Index is required for Cell mode', 'rowIndex');
          }
          if (config.columnIndex === undefined || config.columnIndex === '') {
            addErr(id, name, 'Column Index is required for Cell mode', 'columnIndex');
          }
        }
        if (mode === 'headercell') {
          if (config.rowIndex === undefined || config.rowIndex === '') {
            addErr(id, name, 'Row Index is required for Header Cell mode', 'rowIndex');
          }
          req('returnColumn', 'Return Column');
        }
        if (mode === 'lookup') {
          req('lookupColumn', 'Lookup Column');
          req('lookupValue', 'Lookup Value');
          req('returnColumn', 'Return Column');
        }
        break;
      }

      case 'mail.read-attachments':
        req('folder',         'Folder');
        req('outputVariable', 'Output Variable');
        {
          const mc = config.maxCount;
          if (mc !== '') {
            const maxVal = typeof mc === 'number' ? mc : parseInt(String(mc ?? '1'), 10);
            if (Number.isNaN(maxVal) || maxVal < 1) {
              addErr(id, name, 'Max Count must be at least 1', 'maxCount');
            }
          }
        }
        break;

      case 'excel.transform': {
        req('inputArtifactName', 'Input Artifact Name');
        req('outputName',        'Output Name');
        const ops = config.operations;
        if (!Array.isArray(ops) || ops.length === 0) {
          addErr(id, name, 'At least one operation is required', 'operations');
        } else {
          validateExcelTransformOperations(id, name, ops as Record<string, unknown>[], addErr);
        }
        break;
      }

      case 'excel.read-range': {
        req('inputArtifactName', 'Input Artifact Name');
        req('sheetName',         'Sheet Name');
        req('outputVariable',    'Output Variable');
        break;
      }

      case 'excel.write-datatable': {
        req('inputArtifactName', 'Input Artifact Name');
        req('sheetName',         'Sheet Name');
        req('sourceVariable',    'Source Variable');
        req('startCell',           'Start Cell');
        const cell = strVal(config.startCell ?? 'A1');
        if (cell && !/^[A-Za-z]{1,3}[1-9]\d*$/.test(cell)) {
          addErr(id, name, 'Start Cell must be a valid cell address like A1', 'startCell');
        }
        break;
      }

      case 'excel.append-datatable': {
        req('inputArtifactName', 'Input Artifact Name');
        req('sheetName',         'Sheet Name');
        req('sourceVariable',    'Source Variable');
        break;
      }

      case 'excel.merge': {
        req('outputName', 'Output Name');
        req('sourceSheetName', 'Source Sheet Name');
        req('targetSheetName', 'Target Sheet Name');
        {
          const names = config.inputArtifactNames;
          const empty = names === undefined || names === null
            || (typeof names === 'string' && strVal(names) === '')
            || (Array.isArray(names) && names.length === 0);
          if (empty) {
            addErr(id, name, 'Input Artifact Names is required', 'inputArtifactNames');
          }
        }
        if (!isValidStartRow(config.startRow ?? 1)) {
          addErr(id, name, 'Start Row must be at least 1', 'startRow');
        }
        break;
      }

      case 'excel.split': {
        req('inputArtifactName', 'Input Artifact Name');
        req('sourceSheetName', 'Source Sheet Name');
        req('splitColumn', 'Split Column');
        req('outputNamePattern', 'Output Name Pattern');
        if (!isValidStartRow(config.startRow ?? 2)) {
          addErr(id, name, 'Start Row must be at least 1', 'startRow');
        }
        break;
      }

      case 'condition.if': {
        const cond = (config.condition as Record<string, unknown>) ?? {};
        if (isEmpty(cond.left))     addErr(id, name, 'Left value is required',  'condition.left');
        if (isEmpty(cond.operator)) addErr(id, name, 'Operator is required',    'condition.operator');
        const hasTrueBranch  = edges.some(e => e.source === id && e.sourceHandle === 'true');
        const hasFalseBranch = edges.some(e => e.source === id && e.sourceHandle === 'false');
        if (!hasTrueBranch)  addErr(id, name, 'True branch (green handle) must be connected',  'branches.true');
        if (!hasFalseBranch) addErr(id, name, 'False branch (red handle) must be connected',   'branches.false');
        break;
      }

      case 'set.variable': {
        req('variableName', 'Variable Name');
        req('value', 'Value');
        req('valueType', 'Value Type');
        const mode = strVal(config.mode ?? 'literal').toLowerCase();
        if (mode !== 'literal' && mode !== 'expression') {
          addErr(id, name, "Mode must be 'literal' or 'expression'", 'mode');
        }
        const vt = strVal(config.valueType ?? 'string').toLowerCase();
        if (isEmpty(config.valueType)) {
          addErr(id, name, 'Value Type is required', 'valueType');
        } else if (!['string', 'number', 'boolean', 'json'].includes(vt)) {
          addErr(id, name, "Value Type must be 'string', 'number', 'boolean', or 'json'", 'valueType');
        }
        break;
      }

      case 'foreach.loop': {
        req('collection', 'Collection Variable');
        const hasLoopBody  = edges.some(e => e.source === id && e.sourceHandle === 'loop-body');
        const hasCompleted = edges.some(e => e.source === id && e.sourceHandle === 'completed');
        if (!hasLoopBody)  addErr(id, name, 'Loop Body (right handle) must be connected to the first body step',   'loop-body');
        if (!hasCompleted) addErr(id, name, 'Completed (bottom handle) must be connected to the post-loop step', 'completed');
        if (edges.some(e => e.source === id && e.target === id))
          addErr(id, name, 'A foreach node cannot connect directly to itself', 'self-loop');
        break;
      }

      case 'foreach.row': {
        req('collectionVariable', 'Collection Variable');
        req('rowVariable',        'Row Variable');
        req('indexVariable',      'Index Variable');
        const hasLoopBody  = edges.some(e => e.source === id && e.sourceHandle === 'loop-body');
        const hasCompleted = edges.some(e => e.source === id && e.sourceHandle === 'completed');
        if (!hasLoopBody)  addErr(id, name, 'Loop Body (right handle) must be connected to the first body step',   'loop-body');
        if (!hasCompleted) addErr(id, name, 'Completed (bottom handle) must be connected to the post-loop step', 'completed');
        if (edges.some(e => e.source === id && e.target === id))
          addErr(id, name, 'A foreach node cannot connect directly to itself', 'self-loop');
        break;
      }

      case 'foreach.file': {
        req('collectionVariable', 'Collection Variable');
        req('fileVariable',       'File Variable');
        req('indexVariable',      'Index Variable');
        const hasLoopBody  = edges.some(e => e.source === id && e.sourceHandle === 'loop-body');
        const hasCompleted = edges.some(e => e.source === id && e.sourceHandle === 'completed');
        if (!hasLoopBody)  addErr(id, name, 'Loop Body (right handle) must be connected to the first body step',   'loop-body');
        if (!hasCompleted) addErr(id, name, 'Completed (bottom handle) must be connected to the post-loop step', 'completed');
        if (edges.some(e => e.source === id && e.target === id))
          addErr(id, name, 'A foreach node cannot connect directly to itself', 'self-loop');
        break;
      }

      case 'datatable.aggregate': {
        req('sourceVariable', 'Source Variable');
        req('operation',      'Operation');
        req('outputVariable', 'Output Variable');
        const op = strVal(config.operation ?? '').toLowerCase();
        const validOps = new Set([
          'count', 'countnonempty', 'countdistinct', 'sum', 'average', 'min', 'max',
        ]);
        if (op && !validOps.has(op)) {
          addErr(id, name, 'Operation must be count, countNonEmpty, countDistinct, sum, average, min, or max', 'operation');
        }
        if (op !== 'count' && isEmpty(config.column)) {
          addErr(id, name, 'Column is required for this operation', 'column');
        }
        break;
      }

      case 'datatable.join': {
        req('leftVariable',   'Left Variable');
        req('rightVariable',  'Right Variable');
        req('outputVariable', 'Output Variable');
        const joinType = strVal(config.joinType ?? 'left').toLowerCase();
        if (joinType !== 'left' && joinType !== 'inner') {
          addErr(id, name, "Join Type must be 'left' or 'inner'", 'joinType');
        }
        const leftKeys  = parseColumnList(config.leftKeyColumns);
        const rightKeys = parseColumnList(config.rightKeyColumns);
        if (leftKeys.length === 0) {
          addErr(id, name, 'Left Key Columns is required', 'leftKeyColumns');
        }
        if (rightKeys.length === 0) {
          addErr(id, name, 'Right Key Columns is required', 'rightKeyColumns');
        }
        if (leftKeys.length > 0 && rightKeys.length > 0 && leftKeys.length !== rightKeys.length) {
          addErr(id, name, 'Left and Right Key Columns must have the same number of entries', 'rightKeyColumns');
        }
        const mappings = config.rightColumnsToAdd;
        if (!Array.isArray(mappings) || mappings.length === 0) {
          addErr(id, name, 'At least one right column mapping is required', 'rightColumnsToAdd');
        } else {
          mappings.forEach((raw, mi) => {
            const mapping = raw as Record<string, unknown>;
            if (isEmpty(mapping.sourceColumn)) {
              addErr(id, name, `Mapping ${mi + 1}: source column is required`, `rightColumnsToAdd.${mi}.sourceColumn`);
            }
            if (isEmpty(mapping.targetColumn)) {
              addErr(id, name, `Mapping ${mi + 1}: target column is required`, `rightColumnsToAdd.${mi}.targetColumn`);
            }
          });
        }
        break;
      }

      case 'http.request': {
        req('url',            'URL');
        req('method',         'Method');
        req('outputVariable', 'Output Variable');
        const method = strVal(config.method ?? 'GET').toUpperCase();
        const validMethods = new Set(['GET', 'POST', 'PUT', 'PATCH', 'DELETE']);
        if (method && !validMethods.has(method)) {
          addErr(id, name, 'Method must be GET, POST, PUT, PATCH, or DELETE', 'method');
        }
        const timeout = Number(config.timeoutSeconds ?? 60);
        if (!Number.isFinite(timeout) || timeout <= 0) {
          addErr(id, name, 'Timeout Seconds must be greater than 0', 'timeoutSeconds');
        }
        break;
      }

      case 'json.parse': {
        req('sourceVariable', 'Source Variable');
        req('path',           'JSON Path');
        req('outputVariable', 'Output Variable');
        req('outputMode',     'Output Mode');
        const mode = strVal(config.outputMode ?? 'value').toLowerCase();
        if (mode && mode !== 'value' && mode !== 'json' && mode !== 'table') {
          addErr(id, name, "Output Mode must be 'value', 'json', or 'table'", 'outputMode');
        }
        break;
      }

      case 'sql.query': {
        req('query',          'Query');
        req('outputVariable', 'Output Variable');
        const connStr  = strVal(config.connectionString);
        const connName = strVal(config.connectionName);
        if (!connStr && !connName) {
          addErr(id, name, 'Either Connection String or Connection Name is required', 'connectionString');
        }
        const timeout = Number(config.timeoutSeconds ?? 60);
        if (!Number.isFinite(timeout) || timeout <= 0) {
          addErr(id, name, 'Timeout Seconds must be greater than 0', 'timeoutSeconds');
        }
        const queryText = strVal(config.query);
        if (queryText && !/^\s*(WITH\b|SELECT\b)/i.test(queryText)) {
          addWarn(id, name, 'Query should start with SELECT (only SELECT queries are allowed)', 'query');
        }
        break;
      }

      case 'sql.execute': {
        req('sql', 'SQL');
        const connStr  = strVal(config.connectionString);
        const connName = strVal(config.connectionName);
        if (!connStr && !connName) {
          addErr(id, name, 'Either Connection String or Connection Name is required', 'connectionString');
        }
        const timeout = Number(config.timeoutSeconds ?? 300);
        if (!Number.isFinite(timeout) || timeout <= 0) {
          addErr(id, name, 'Timeout Seconds must be greater than 0', 'timeoutSeconds');
        }
        break;
      }

      case 'sql.stored-procedure': {
        req('procedureName', 'Procedure Name');
        req('outputVariable', 'Output Variable');
        const connStr  = strVal(config.connectionString);
        const connName = strVal(config.connectionName);
        if (!connStr && !connName) {
          addErr(id, name, 'Either Connection String or Connection Name is required', 'connectionString');
        }
        const timeout = Number(config.timeoutSeconds ?? 300);
        if (!Number.isFinite(timeout) || timeout <= 0) {
          addErr(id, name, 'Timeout Seconds must be greater than 0', 'timeoutSeconds');
        }
        break;
      }

      case 'browser.navigate':
        req('url', 'URL');
        break;

      case 'browser.click':
        req('selector', 'Selector');
        break;

      case 'browser.type':
        req('selector', 'Selector');
        req('text', 'Text');
        break;

      case 'browser.wait-for-selector':
        req('selector', 'Selector');
        break;

      case 'browser.get-text':
        req('selector', 'Selector');
        req('outputVariable', 'Output Variable');
        break;

      case 'browser.get-attribute':
        req('selector', 'Selector');
        req('attribute', 'Attribute');
        req('outputVariable', 'Output Variable');
        break;

      case 'browser.screenshot':
        req('artifactName', 'Artifact Name');
        break;

      case 'browser.download':
        req('clickSelector', 'Click Selector');
        req('artifactName', 'Artifact Name');
        break;

      case 'browser.press-key':
        req('key', 'Key');
        break;

      case 'browser.clear':
        req('selector', 'Selector');
        break;

      case 'browser.scroll': {
        const direction = strVal(config.direction ?? 'down').toLowerCase();
        if (!['up', 'down', 'left', 'right'].includes(direction)) {
          addErr(id, name, "Direction must be 'up', 'down', 'left', or 'right'", 'direction');
        }
        const amount = Number(config.amount ?? 1000);
        if (!Number.isFinite(amount) || amount <= 0) {
          addErr(id, name, 'Amount must be greater than 0', 'amount');
        }
        break;
      }

      case 'browser.select': {
        req('selector', 'Selector');
        const mode = strVal(config.mode ?? 'value').toLowerCase();
        if (mode === 'value') req('value', 'Value');
        else if (mode === 'label') req('label', 'Label');
        else if (mode === 'index') {
          const index = Number(config.index ?? 0);
          if (!Number.isFinite(index) || index < 0) {
            addErr(id, name, 'Index must be >= 0', 'index');
          }
        } else {
          addErr(id, name, "Mode must be 'value', 'label', or 'index'", 'mode');
        }
        break;
      }

      case 'browser.hover':
        req('selector', 'Selector');
        break;

      case 'browser.wait-url': {
        const hasUrl = !isEmpty(config.urlContains) || !isEmpty(config.pattern);
        if (!hasUrl) addErr(id, name, 'URL Contains / Pattern is required', 'urlContains');
        const mode = strVal(config.matchMode ?? 'contains').toLowerCase();
        if (mode && !['contains', 'equals', 'startswith', 'regex'].includes(mode)) {
          addErr(id, name, "Match Mode must be 'contains', 'equals', 'startsWith', or 'regex'", 'matchMode');
        }
        const urlTimeout = Number(config.timeoutMs ?? 30000);
        if (!Number.isFinite(urlTimeout) || urlTimeout <= 0) {
          addErr(id, name, 'Timeout must be greater than 0', 'timeoutMs');
        }
        break;
      }

      case 'browser.wait-text': {
        req('text', 'Text');
        const textTimeout = Number(config.timeoutMs ?? 30000);
        if (!Number.isFinite(textTimeout) || textTimeout <= 0) {
          addErr(id, name, 'Timeout must be greater than 0', 'timeoutMs');
        }
        break;
      }

      case 'browser.wait-download': {
        req('clickSelector', 'Click Selector');
        req('artifactName', 'Artifact Name');
        const dlTimeout = Number(config.timeoutMs ?? 60000);
        if (!Number.isFinite(dlTimeout) || dlTimeout <= 0) {
          addErr(id, name, 'Timeout must be greater than 0', 'timeoutMs');
        }
        break;
      }

      case 'browser.wait-network-idle': {
        const idleTimeout = Number(config.timeoutMs ?? 30000);
        if (!Number.isFinite(idleTimeout) || idleTimeout <= 0) {
          addErr(id, name, 'Timeout must be greater than 0', 'timeoutMs');
        }
        break;
      }

      case 'browser.element-exists': {
        req('selector', 'Selector');
        req('outputVariable', 'Output Variable');
        const existsTimeout = Number(config.timeoutMs ?? 5000);
        if (!Number.isFinite(existsTimeout) || existsTimeout <= 0) {
          addErr(id, name, 'Timeout must be greater than 0', 'timeoutMs');
        }
        break;
      }

      case 'browser.upload-file': {
        req('selector', 'Selector');
        const hasArtifact = !isEmpty(config.artifactName);
        const hasPath = !isEmpty(config.filePath);
        if (!hasArtifact && !hasPath) {
          addErr(id, name, 'Either Artifact Name or File Path is required', 'artifactName');
        }
        const uploadTimeout = Number(config.timeoutMs ?? 30000);
        if (!Number.isFinite(uploadTimeout) || uploadTimeout <= 0) {
          addErr(id, name, 'Timeout must be greater than 0', 'timeoutMs');
        }
        break;
      }

      case 'browser.switch-tab': {
        const mode = strVal(config.mode ?? 'last').toLowerCase();
        if (mode === 'byurl' && isEmpty(config.urlContains)) {
          addErr(id, name, 'URL Contains is required when mode is byUrl', 'urlContains');
        }
        if (mode === 'bytitle' && isEmpty(config.titleContains)) {
          addErr(id, name, 'Title Contains is required when mode is byTitle', 'titleContains');
        }
        const switchTimeout = Number(config.timeoutMs ?? 30000);
        if (!Number.isFinite(switchTimeout) || switchTimeout <= 0) {
          addErr(id, name, 'Timeout must be greater than 0', 'timeoutMs');
        }
        break;
      }

      case 'browser.close-tab':
        break;

      case 'browser.handle-alert':
      case 'browser.click-and-handle-alert': {
        const action = strVal(config.action ?? 'accept').toLowerCase();
        if (action && action !== 'accept' && action !== 'dismiss') {
          addErr(id, name, "Action must be 'accept' or 'dismiss'", 'action');
        }
        const alertTimeout = Number(config.timeoutMs ?? 30000);
        if (!Number.isFinite(alertTimeout) || alertTimeout <= 0) {
          addErr(id, name, 'Timeout must be greater than 0', 'timeoutMs');
        }
        if (stepType === 'browser.click-and-handle-alert') {
          req('clickSelector', 'Click Selector');
        }
        break;
      }

      case 'browser.wait-popup': {
        req('clickSelector', 'Click Selector');
        const popupTimeout = Number(config.timeoutMs ?? 30000);
        if (!Number.isFinite(popupTimeout) || popupTimeout <= 0) {
          addErr(id, name, 'Timeout must be greater than 0', 'timeoutMs');
        }
        break;
      }

      case 'browser.extract-table': {
        req('outputVariable', 'Output Variable');
        const extractMode = strVal(config.mode ?? 'htmlTable').toLowerCase();
        if (extractMode !== 'htmltable' && extractMode !== 'cssgrid') {
          addErr(id, name, "Mode must be 'htmlTable' or 'cssGrid'", 'mode');
        }
        if (extractMode === 'htmltable') {
          req('selector', 'Selector');
        } else {
          req('rowSelector', 'Row Selector');
          req('cellSelector', 'Cell Selector');
        }
        const extractTimeout = Number(config.timeoutMs ?? 30000);
        if (!Number.isFinite(extractTimeout) || extractTimeout <= 0) {
          addErr(id, name, 'Timeout must be greater than 0', 'timeoutMs');
        }
        const idx = Number(config.tableIndex ?? 0);
        if (!Number.isFinite(idx) || idx < 0) {
          addErr(id, name, 'Table Index must be >= 0', 'tableIndex');
        }
        break;
      }

      case 'browser.wait-for-text':
        req('text', 'Text');
        break;

      case 'browser.wait-for-url':
        req('urlContains', 'URL Contains');
        break;

      case 'browser.select-option': {
        req('selector', 'Selector');
        const hasValue = !isEmpty(config.value);
        const hasLabel = !isEmpty(config.label);
        if (!hasValue && !hasLabel) {
          addErr(id, name, 'Either Value or Label is required', 'value');
        }
        break;
      }

      case 'browser.evaluate':
        req('script', 'Script');
        req('outputVariable', 'Output Variable');
        break;

      case 'browser.wait-for-download':
        req('clickSelector', 'Click Selector');
        req('artifactName', 'Artifact Name');
        break;
    }

    // ── Per-step retry policy validation ──────────────────────────────────────
    const retry = node.data.retry;
    if (retry) {
      if (typeof retry.maxAttempts !== 'number' || retry.maxAttempts < 1) {
        addErr(id, name, 'Retry: Max Attempts must be at least 1', 'retry.maxAttempts');
      }
      if (typeof retry.delaySeconds !== 'number' || retry.delaySeconds < 0) {
        addErr(id, name, 'Retry: Delay Seconds must be 0 or greater', 'retry.delaySeconds');
      }
    }
  }

  // ── 2. Graph-level rules ────────────────────────────────────────────────────
  if (nodes.length === 0) return { errors, warnings };

  // Forward graph (adjOut) and incoming graph (adjIn) — loop-back edges excluded
  // from both so that:
  //   • cycle detection ignores the intentional foreach loop-back
  //   • reachability follows Mail Receive → ForEach → body / completed
  //   • root detection keeps the normal input edge into ForEach (targetHandle "in")
  const { adjOut, adjIn } = buildGraphAdjacency(nodes, edges);

  // ── 2a. Root detection ──────────────────────────────────────────────────────
  const roots = nodes.filter(n => adjIn.get(n.id)!.length === 0);

  if (roots.length === 0) {
    errors.push({
      nodeId: '', nodeName: 'Graph',
      message: 'No start node found — all nodes have incoming edges (possible cycle).',
    });
  } else if (roots.length > 1) {
    const names = roots.map(r => `"${r.data.name}"`).join(', ');
    warnings.push({
      nodeId: '', nodeName: 'Graph',
      message: `Multiple start nodes detected: ${names}. Consider connecting them or using a Condition node.`,
    });
  }

  // ── 2b. Cycle detection (iterative DFS with colouring) ─────────────────────
  const WHITE = 0, GRAY = 1, BLACK = 2;
  const color = new Map<string, number>(nodes.map(n => [n.id, WHITE]));
  let hasCycle = false;

  for (const startNode of nodes) {
    if (color.get(startNode.id) !== WHITE || hasCycle) continue;
    const stack: Array<{ id: string; ci: number }> = [{ id: startNode.id, ci: 0 }];
    color.set(startNode.id, GRAY);
    while (stack.length > 0 && !hasCycle) {
      const top = stack[stack.length - 1];
      const children = adjOut.get(top.id) ?? [];
      if (top.ci < children.length) {
        const child = children[top.ci++];
        if (color.get(child) === GRAY) {
          hasCycle = true;
        } else if (color.get(child) === WHITE) {
          color.set(child, GRAY);
          stack.push({ id: child, ci: 0 });
        }
      } else {
        color.set(top.id, BLACK);
        stack.pop();
      }
    }
  }

  if (hasCycle) {
    errors.push({
      nodeId: '', nodeName: 'Graph',
      message: 'Workflow contains a cycle — remove circular connections.',
    });
  }

  // ── 2c. Reachability from roots (BFS) ──────────────────────────────────────
  const reachable = new Set<string>(roots.map(r => r.id));
  const bfsQ = [...roots.map(r => r.id)];
  let qi = 0;
  while (qi < bfsQ.length) {
    const curr = bfsQ[qi++];
    for (const next of adjOut.get(curr) ?? []) {
      if (!reachable.has(next)) { reachable.add(next); bfsQ.push(next); }
    }
  }
  for (const n of nodes) {
    if (!reachable.has(n.id)) {
      addErr(n.id, n.data.name, 'Node is unreachable from any start node');
    }
  }

  // ── 2d. Orphan detection (no edges at all) ─────────────────────────────────
  if (nodes.length > 1) {
    for (const n of nodes) {
      const noOut = (adjOut.get(n.id)?.length ?? 0) === 0;
      const noIn  = (adjIn.get(n.id)?.length  ?? 0) === 0;
      if (noOut && noIn) {
        addWarn(n.id, n.data.name, 'Node has no connections — drag its handles to link it');
      }
    }
  }

  return { errors, warnings };
}

// ── Derived helpers ────────────────────────────────────────────────────────────

/** Returns a map of nodeId → field → message for quick lookup in editors. */
export function buildFieldErrorMap(
  errors: ValidationError[],
): Map<string, Record<string, string>> {
  const map = new Map<string, Record<string, string>>();
  for (const e of errors) {
    if (!e.field) continue;
    if (!map.has(e.nodeId)) map.set(e.nodeId, {});
    map.get(e.nodeId)![e.field] = e.message;
  }
  return map;
}
