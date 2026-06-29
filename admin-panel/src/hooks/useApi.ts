import { useState, useEffect, useCallback } from 'react';

interface ApiState<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
}

export function useApi<T>(
  fetcher: () => Promise<{ data: T }>,
  deps: unknown[] = [],
): ApiState<T> & { refetch: (options?: { silent?: boolean }) => void } {
  const [state, setState] = useState<ApiState<T>>({
    data: null,
    loading: true,
    error: null,
  });

  const fetch = useCallback(async (silent = false) => {
    if (!silent) setState((s) => ({ ...s, loading: true, error: null }));
    try {
      const res = await fetcher();
      setState({ data: res.data, loading: false, error: null });
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : 'An error occurred';
      setState((s) => (
        silent
          ? { ...s, loading: false }
          : { data: null, loading: false, error: msg }
      ));
    }
    // deps intentionally excludes `fetcher` to avoid infinite loops when an
    // inline arrow function is passed; callers control refetch via `deps`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps]);

  useEffect(() => { void fetch(); }, [fetch]);

  return {
    ...state,
    refetch: (options) => { void fetch(options?.silent ?? false); },
  };
}
