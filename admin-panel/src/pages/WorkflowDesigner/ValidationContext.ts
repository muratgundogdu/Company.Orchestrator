import { createContext, useContext } from 'react';

export interface ValidationContextValue {
  errorNodeIds:   Set<string>;
  warningNodeIds: Set<string>;
}

const DEFAULT: ValidationContextValue = {
  errorNodeIds:   new Set(),
  warningNodeIds: new Set(),
};

export const ValidationContext = createContext<ValidationContextValue>(DEFAULT);

export function useValidation(): ValidationContextValue {
  return useContext(ValidationContext);
}
