import type { AvailableVariable } from './types';

export interface NodeOutputBase {
  sourceNodeId: string;
  sourceNodeName: string;
  sourceStepType: string;
}

type NodeOutputResolver = (
  config: Record<string, unknown>,
  base: NodeOutputBase,
) => AvailableVariable[];

/** Ensures artifact names include the step's default extension when missing. */
export function normalizeArtifactOutputName(name: string, extension: string): string {
  const trimmed = name.trim();
  if (!trimmed) return trimmed;

  const ext = extension.startsWith('.') ? extension : `.${extension}`;
  if (trimmed.toLowerCase().endsWith(ext.toLowerCase())) return trimmed;
  return `${trimmed}${ext}`;
}

function artifact(
  base: NodeOutputBase,
  name: string,
  description: string,
): AvailableVariable {
  return {
    ...base,
    kind: 'artifact',
    insertValue: name,
    label: name,
    description,
  };
}

function stepVar(
  base: NodeOutputBase,
  insertValue: string,
  label: string,
  description: string,
): AvailableVariable {
  return {
    ...base,
    kind: 'variable',
    insertValue,
    label,
    description,
  };
}

function resolveMailReadAttachments(
  config: Record<string, unknown>,
  base: NodeOutputBase,
): AvailableVariable[] {
  const v = String(config.outputVariable ?? config.outputVar ?? 'mailArtifacts');
  return [
    stepVar(base, `{{${v}_0}}`, `${v}_0`, 'First attachment artifact name'),
    stepVar(base, `{{${v}_1}}`, `${v}_1`, 'Second attachment artifact name'),
    stepVar(base, `{{${v}_first}}`, `${v}_first`, 'Alias for _0 — first attachment'),
    stepVar(base, `{{${v}_count}}`, `${v}_count`, 'Total number of attachments downloaded'),
    stepVar(base, `{{${v}}}`, v, 'JSON array of all artifact names'),
    stepVar(base, '{{selectedMessageId}}', 'selectedMessageId', 'IMAP UID of the processed email'),
    stepVar(base, '{{selectedMessageFolder}}', 'selectedMessageFolder', 'Folder of the processed email'),
    stepVar(base, '{{selectedEmailSubject}}', 'selectedEmailSubject', 'Subject of the processed email'),
    stepVar(base, '{{selectedEmailFrom}}', 'selectedEmailFrom', 'Sender of the processed email'),
  ];
}

function resolveExcelTransform(
  config: Record<string, unknown>,
  base: NodeOutputBase,
): AvailableVariable[] {
  const raw = String(config.outputName ?? 'transformed-excel');
  const name = normalizeArtifactOutputName(raw, '.xlsx');
  return [
    artifact(base, name, 'Artifact name of the transformed Excel file'),
    stepVar(base, '{{transformedArtifactName}}', 'transformedArtifactName', 'Artifact name for downstream steps'),
    stepVar(base, '{{transformedArtifact_sheetNames}}', 'transformedArtifact_sheetNames', 'Comma-separated sheet names'),
    stepVar(base, '{{transformedArtifact_rowCount}}', 'transformedArtifact_rowCount', 'Row count of first sheet'),
    stepVar(base, '{{transformedArtifact_colCount}}', 'transformedArtifact_colCount', 'Column count of first sheet'),
  ];
}

function resolveExcelWrite(
  config: Record<string, unknown>,
  base: NodeOutputBase,
): AvailableVariable[] {
  const name = normalizeArtifactOutputName(String(config.outputName ?? 'report.xlsx'), '.xlsx');
  const varBase = name.replace(/[^a-zA-Z0-9_]/g, '_');
  return [
    artifact(base, name, 'Excel workbook artifact produced by this step'),
    stepVar(base, `{{${varBase}_artifactId}}`, `${varBase}_artifactId`, 'Guid of the produced artifact'),
    stepVar(base, `{{${varBase}_rowCount}}`, `${varBase}_rowCount`, 'Number of data rows written'),
  ];
}

function resolveExcelWriteDataTable(
  config: Record<string, unknown>,
  base: NodeOutputBase,
): AvailableVariable[] {
  const raw = String(config.inputArtifactName ?? 'report.xlsx');
  const name = normalizeArtifactOutputName(raw, '.xlsx');
  return [
    artifact(base, name, 'Updated Excel workbook artifact'),
    stepVar(base, '{{outputArtifactName}}', 'outputArtifactName', 'Name of the updated workbook artifact'),
    stepVar(base, '{{rowsWritten}}', 'rowsWritten', 'Number of data rows written'),
    stepVar(base, '{{columnsWritten}}', 'columnsWritten', 'Number of columns written'),
  ];
}

function resolveCsvWrite(
  config: Record<string, unknown>,
  base: NodeOutputBase,
): AvailableVariable[] {
  const name = normalizeArtifactOutputName(String(config.outputName ?? 'output.csv'), '.csv');
  return [
    artifact(base, name, 'Output CSV artifact name'),
    stepVar(base, '{{outputName}}', 'outputName', 'Name of the CSV artifact produced by this step'),
    stepVar(base, '{{rowsWritten}}', 'rowsWritten', 'Number of data rows written'),
    stepVar(base, '{{columnsWritten}}', 'columnsWritten', 'Number of columns written'),
  ];
}

function resolveJsonWrite(
  config: Record<string, unknown>,
  base: NodeOutputBase,
): AvailableVariable[] {
  const name = normalizeArtifactOutputName(String(config.outputName ?? 'output.json'), '.json');
  return [
    artifact(base, name, 'Output JSON artifact name'),
    stepVar(base, '{{outputName}}', 'outputName', 'Name of the JSON artifact produced by this step'),
    stepVar(base, '{{bytesWritten}}', 'bytesWritten', 'Size of the written JSON file in bytes'),
  ];
}

function resolveWordFillTemplate(
  config: Record<string, unknown>,
  base: NodeOutputBase,
): AvailableVariable[] {
  const inputArtifact = String(config.inputArtifactName ?? 'template.docx');
  const outputRaw = String(config.outputName ?? 'generated.docx');
  const outputName = normalizeArtifactOutputName(outputRaw, '.docx');
  const v = String(config.outputVariable ?? 'generatedDocument');
  return [
    artifact(base, inputArtifact, 'Input Word template artifact name'),
    artifact(base, outputName, 'Generated Word document artifact name'),
    stepVar(base, `{{${v}}}`, v, 'Generated document artifact name (same as outputName)'),
    stepVar(base, '{{outputName}}', 'outputName', 'Name of the generated Word document artifact'),
    stepVar(base, '{{placeholdersReplaced}}', 'placeholdersReplaced', 'Number of placeholders successfully replaced'),
    stepVar(base, '{{missingPlaceholders}}', 'missingPlaceholders', 'Comma-separated list of placeholders with missing variables'),
  ];
}

function resolveZipCreate(
  config: Record<string, unknown>,
  base: NodeOutputBase,
): AvailableVariable[] {
  const name = normalizeArtifactOutputName(String(config.outputName ?? 'reports.zip'), '.zip');
  return [
    artifact(base, name, 'Output ZIP artifact name'),
    stepVar(base, '{{outputName}}', 'outputName', 'Name of the ZIP artifact produced by this step'),
    stepVar(base, '{{filesZipped}}', 'filesZipped', 'Number of files added to the ZIP'),
    stepVar(base, '{{zipSizeBytes}}', 'zipSizeBytes', 'Size of the ZIP file in bytes'),
  ];
}

function resolveBrowserDownload(
  config: Record<string, unknown>,
  base: NodeOutputBase,
  stepType: string,
): AvailableVariable[] {
  let name = String(config.artifactName ?? 'browser-artifact');
  if (stepType === 'browser.screenshot') {
    name = normalizeArtifactOutputName(name, '.png');
  }
  const vars: AvailableVariable[] = [
    artifact(
      base,
      name,
      stepType === 'browser.screenshot' ? 'Screenshot PNG artifact' : 'Downloaded file artifact',
    ),
  ];
  if (stepType === 'browser.wait-download' || stepType === 'browser.wait-for-download') {
    vars.push(
      stepVar(base, '{{artifactName}}', 'artifactName', 'Name of the downloaded artifact'),
      stepVar(base, '{{downloadedFileName}}', 'downloadedFileName', 'Downloaded file name (may include preserved extension)'),
      stepVar(base, '{{downloadedFileSizeBytes}}', 'downloadedFileSizeBytes', 'Downloaded file size in bytes'),
    );
  }
  return vars;
}

function resolveFolderWriteFile(
  _config: Record<string, unknown>,
  base: NodeOutputBase,
): AvailableVariable[] {
  return [
    stepVar(base, '{{writtenPath}}', 'writtenPath', 'Resolved destination file path written to disk'),
    stepVar(base, '{{writtenSizeBytes}}', 'writtenSizeBytes', 'Byte count written to disk'),
  ];
}

const NODE_OUTPUT_RESOLVERS: Record<string, NodeOutputResolver> = {
  'mail.read-attachments': resolveMailReadAttachments,
  'excel.transform': resolveExcelTransform,
  'excel.write': resolveExcelWrite,
  'excel.write-datatable': resolveExcelWriteDataTable,
  'csv.write': resolveCsvWrite,
  'json.write-file': resolveJsonWrite,
  'word.fill-template': resolveWordFillTemplate,
  'zip.create': resolveZipCreate,
  'folder.write-file': resolveFolderWriteFile,
  'browser.screenshot': (c, b) => resolveBrowserDownload(c, b, 'browser.screenshot'),
  'browser.download': (c, b) => resolveBrowserDownload(c, b, 'browser.download'),
  'browser.wait-for-download': (c, b) => resolveBrowserDownload(c, b, 'browser.wait-for-download'),
  'browser.wait-download': (c, b) => resolveBrowserDownload(c, b, 'browser.wait-download'),
};

export function resolveRegisteredNodeOutputs(
  stepType: string,
  config: Record<string, unknown>,
  base: NodeOutputBase,
): AvailableVariable[] | null {
  const resolver = NODE_OUTPUT_RESOLVERS[stepType];
  if (!resolver) return null;
  return resolver(config, base);
}

export type PickerCategory = 'variables' | 'artifacts' | 'step-outputs';

export function isSystemVariableSource(sourceNodeId: string): boolean {
  return sourceNodeId.startsWith('__');
}

export function categorizePickerVariable(v: AvailableVariable): PickerCategory {
  if (v.kind === 'artifact') return 'artifacts';
  if (v.sourceStepType === 'set.variable' || isSystemVariableSource(v.sourceNodeId)) {
    return 'variables';
  }
  return 'step-outputs';
}

export function groupPickerVariables(variables: AvailableVariable[]): Record<PickerCategory, AvailableVariable[]> {
  const groups: Record<PickerCategory, AvailableVariable[]> = {
    variables: [],
    artifacts: [],
    'step-outputs': [],
  };
  for (const v of variables) {
    groups[categorizePickerVariable(v)].push(v);
  }
  return groups;
}

export const PICKER_CATEGORY_LABELS: Record<PickerCategory, string> = {
  variables: 'Variables',
  artifacts: 'Artifacts',
  'step-outputs': 'Step Outputs',
};
