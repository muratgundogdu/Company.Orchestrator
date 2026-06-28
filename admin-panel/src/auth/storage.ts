import type { UserProfileDto } from '../api/types';

const TOKEN_KEY = 'alterone_token';
const EXPIRES_KEY = 'alterone_token_expires';
const USER_KEY = 'alterone_user';

export interface StoredAuth {
  token: string;
  expiresAtUtc: string;
  user: UserProfileDto;
}

export function loadAuth(): StoredAuth | null {
  const token = localStorage.getItem(TOKEN_KEY) ?? sessionStorage.getItem(TOKEN_KEY);
  const expiresAtUtc = localStorage.getItem(EXPIRES_KEY) ?? sessionStorage.getItem(EXPIRES_KEY);
  const userJson = localStorage.getItem(USER_KEY) ?? sessionStorage.getItem(USER_KEY);
  if (!token || !expiresAtUtc || !userJson) return null;

  try {
    const user = JSON.parse(userJson) as UserProfileDto;
    return { token, expiresAtUtc, user };
  } catch {
    return null;
  }
}

export function saveAuth(auth: StoredAuth, remember: boolean): void {
  const storage = remember ? localStorage : sessionStorage;
  storage.setItem(TOKEN_KEY, auth.token);
  storage.setItem(EXPIRES_KEY, auth.expiresAtUtc);
  storage.setItem(USER_KEY, JSON.stringify(auth.user));

  const other = remember ? sessionStorage : localStorage;
  other.removeItem(TOKEN_KEY);
  other.removeItem(EXPIRES_KEY);
  other.removeItem(USER_KEY);
}

export function clearAuth(): void {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(EXPIRES_KEY);
  localStorage.removeItem(USER_KEY);
  sessionStorage.removeItem(TOKEN_KEY);
  sessionStorage.removeItem(EXPIRES_KEY);
  sessionStorage.removeItem(USER_KEY);
}

export function getActiveToken(): string | null {
  return localStorage.getItem(TOKEN_KEY) ?? sessionStorage.getItem(TOKEN_KEY);
}
