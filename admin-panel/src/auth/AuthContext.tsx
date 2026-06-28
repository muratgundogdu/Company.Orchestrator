import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { authApi } from '../api/endpoints';
import type { LoginRequest, UserProfileDto } from '../api/types';
import { Permissions, type Permission } from './permissions';
import { clearAuth, getActiveToken, loadAuth, saveAuth } from './storage';

interface AuthContextValue {
  user: UserProfileDto | null;
  token: string | null;
  loading: boolean;
  login: (request: LoginRequest, remember?: boolean) => Promise<void>;
  logout: () => void;
  hasPermission: (permission: Permission | Permission[]) => boolean;
  refreshProfile: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function isExpired(expiresAtUtc: string): boolean {
  return new Date(expiresAtUtc).getTime() <= Date.now();
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserProfileDto | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const logout = useCallback(() => {
    clearAuth();
    setUser(null);
    setToken(null);
  }, []);

  const refreshProfile = useCallback(async () => {
    const activeToken = getActiveToken();
    if (!activeToken) {
      logout();
      return;
    }
    const res = await authApi.me();
    setUser(res.data);
  }, [logout]);

  useEffect(() => {
    const stored = loadAuth();
    if (!stored || isExpired(stored.expiresAtUtc)) {
      clearAuth();
      setLoading(false);
      return;
    }
    setToken(stored.token);
    setUser(stored.user);
    setLoading(false);
  }, []);

  useEffect(() => {
    if (!token || !user) return undefined;

    const stored = loadAuth();
    if (!stored) return undefined;

    const msUntilExpiry = new Date(stored.expiresAtUtc).getTime() - Date.now();
    if (msUntilExpiry <= 0) {
      logout();
      return undefined;
    }

    const timer = window.setTimeout(logout, msUntilExpiry);
    return () => window.clearTimeout(timer);
  }, [token, user, logout]);

  const login = useCallback(async (request: LoginRequest, remember = false) => {
    const res = await authApi.login(request);
    const auth = {
      token: res.data.token,
      expiresAtUtc: res.data.expiresAtUtc,
      user: res.data.user,
    };
    saveAuth(auth, remember);
    setToken(auth.token);
    setUser(auth.user);
  }, []);

  const hasPermission = useCallback(
    (permission: Permission | Permission[]) => {
      if (!user) return false;
      const required = Array.isArray(permission) ? permission : [permission];
      const perms = new Set(user.permissions.map((p) => p.toLowerCase()));
      const roles = new Set(user.roles.map((r) => r.toLowerCase()));
      if (roles.has('admin')) return true;
      return required.some((p) => perms.has(p.toLowerCase()));
    },
    [user],
  );

  const value = useMemo(
    () => ({ user, token, loading, login, logout, hasPermission, refreshProfile }),
    [user, token, loading, login, logout, hasPermission, refreshProfile],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}

export { Permissions };
