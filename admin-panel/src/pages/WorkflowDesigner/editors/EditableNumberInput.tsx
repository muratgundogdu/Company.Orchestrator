import { useState } from 'react';

type EditableNumberInputProps = Omit<
  React.InputHTMLAttributes<HTMLInputElement>,
  'value' | 'onChange' | 'onBlur'
> & {
  value: unknown;
  /** Value applied when the field is empty or invalid on blur. */
  fallback: number;
  /** Minimum allowed value after blur normalization. Omit to skip minimum clamping. */
  min?: number;
  parseAs?: 'int' | 'float';
  onValueChange: (value: number | '') => void;
};

function displayValue(value: unknown, fallback: number): string {
  if (value === '') return '';
  if (value === null || value === undefined) return String(fallback);
  return String(value);
}

function parseRaw(raw: string, parseAs: 'int' | 'float'): number {
  return parseAs === 'float' ? parseFloat(raw) : parseInt(raw, 10);
}

function normalizeOnBlur(
  raw: string,
  fallback: number,
  min: number | undefined,
  parseAs: 'int' | 'float',
): number {
  if (raw === '') return fallback;
  const n = parseRaw(raw, parseAs);
  if (Number.isNaN(n)) return fallback;
  if (min !== undefined && n < min) return min;
  return n;
}

export default function EditableNumberInput({
  value,
  fallback,
  min,
  parseAs = 'int',
  onValueChange,
  ...inputProps
}: EditableNumberInputProps) {
  const [draft, setDraft] = useState<string | null>(null);

  const display = draft ?? displayValue(value, fallback);

  function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
    const raw = e.target.value;
    setDraft(raw);
    if (raw === '') {
      onValueChange('');
      return;
    }
    const n = parseRaw(raw, parseAs);
    if (!Number.isNaN(n)) onValueChange(n);
  }

  function handleBlur() {
    const raw = draft ?? displayValue(value, fallback);
    setDraft(null);
    const normalized = normalizeOnBlur(raw, fallback, min, parseAs);
    onValueChange(normalized);
  }

  return (
    <input
      {...inputProps}
      type="number"
      value={display}
      onChange={handleChange}
      onBlur={handleBlur}
    />
  );
}
