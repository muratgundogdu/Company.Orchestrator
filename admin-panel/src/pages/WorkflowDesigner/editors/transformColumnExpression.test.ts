import { describe, expect, it } from 'vitest';
import { normalizeExpressionText } from './transformColumnExpression';

describe('normalizeExpressionText', () => {
  it('trims whitespace', () => {
    expect(normalizeExpressionText('  toNumber(value)  ')).toBe('toNumber(value)');
  });

  it('joins multiline input into a single expression string', () => {
    expect(
      normalizeExpressionText('toNumber(row["Fiyat"])\n* toNumber(variables.usdRateText)'),
    ).toBe('toNumber(row["Fiyat"]) * toNumber(variables.usdRateText)');
  });

  it('removes empty lines', () => {
    expect(normalizeExpressionText('trim(value)\n\n\n')).toBe('trim(value)');
  });
});
