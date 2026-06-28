export interface EditorProps {
  config: Record<string, unknown>;
  onChange: (config: Record<string, unknown>) => void;
  /** Called when any text/number input inside the editor gains focus. */
  onFocusField: (el: HTMLInputElement | HTMLTextAreaElement, key: string) => void;
  /**
   * Map from field key (matching ValidationError.field) to error message.
   * Editors render these under the corresponding inputs.
   */
  fieldErrors?: Record<string, string>;
}
