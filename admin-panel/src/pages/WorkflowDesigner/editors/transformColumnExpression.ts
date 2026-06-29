/**
 * Collapses multiline editor text into a single expression string for storage.
 */
export function normalizeExpressionText(raw: string): string {
  return raw
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .join(' ')
    .trim();
}
