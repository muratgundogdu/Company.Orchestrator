import { useMemo } from 'react';
import { credentialApi } from '../api/endpoints';
import type { CredentialDto } from '../api/types';
import { useApi } from './useApi';

export function useCredentials(type?: string) {
  const { data, loading, error, refetch } = useApi(
    () => credentialApi.list(1, 500),
    [],
  );

  const credentials = useMemo(() => {
    const items = data?.items ?? [];
    if (!type) return items;
    return items.filter((c) => c.type === type && c.isActive);
  }, [data, type]);

  return { credentials, loading, error, refetch };
}

export function credentialOptions(credentials: CredentialDto[]) {
  return credentials.map((c) => ({ value: c.name, label: `${c.name} (${c.type})` }));
}
