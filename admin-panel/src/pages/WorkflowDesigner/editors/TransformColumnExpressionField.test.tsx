import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import TransformColumnExpressionField from './TransformColumnExpressionField';

describe('TransformColumnExpressionField', () => {
  it('keeps inline editing behavior', () => {
    const onChange = vi.fn();
    render(
      <TransformColumnExpressionField
        value="toNumber(value) / 100"
        onChange={onChange}
      />,
    );

    const input = screen.getByTestId('expression-inline-input') as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'trim(value)' } });
    expect(onChange).toHaveBeenCalledWith('trim(value)');
  });

  it('opens modal, saves normalized expression, and applies to operation', () => {
    const onChange = vi.fn();
    render(
      <TransformColumnExpressionField
        value="toNumber(value) / 100"
        onChange={onChange}
      />,
    );

    fireEvent.click(screen.getByTestId('expression-expand-button'));
    const textarea = screen.getByTestId('expression-editor-textarea') as HTMLTextAreaElement;
    fireEvent.change(textarea, {
      target: {
        value: 'toNumber(row["Fiyat"])\n* toNumber(variables.usdRateText)',
      },
    });
    fireEvent.click(screen.getByTestId('expression-editor-save'));

    expect(onChange).toHaveBeenCalledWith(
      'toNumber(row["Fiyat"]) * toNumber(variables.usdRateText)',
    );
    expect(screen.queryByTestId('expression-editor-textarea')).not.toBeInTheDocument();
  });

  it('closes modal on cancel without changing expression', () => {
    const onChange = vi.fn();
    render(
      <TransformColumnExpressionField
        value="toNumber(value) / 100"
        onChange={onChange}
      />,
    );

    fireEvent.click(screen.getByTestId('expression-expand-button'));
    const textarea = screen.getByTestId('expression-editor-textarea') as HTMLTextAreaElement;
    fireEvent.change(textarea, { target: { value: 'trim(value)' } });
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(onChange).not.toHaveBeenCalled();
    expect(screen.getByTestId('expression-inline-input')).toHaveValue('toNumber(value) / 100');
  });
});
