import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { apiClient, setAccessToken, setOnSessionExpired, toApiError } from '../api/client';
import type { AuthUser, LoginResult } from '../api/types';

interface AuthState {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isInitialising: boolean;
  login: (userName: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  hasPermission: (permission: string) => boolean;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isInitialising, setIsInitialising] = useState(true);

  const clearSession = useCallback(() => {
    setAccessToken(null);
    setUser(null);
  }, []);

  // If the refresh interceptor gives up, drop back to the login screen cleanly.
  useEffect(() => {
    setOnSessionExpired(clearSession);
    return () => setOnSessionExpired(null);
  }, [clearSession]);

  // On mount, try a silent refresh: the access token is in memory only, but the
  // refresh token lives in an HttpOnly cookie, so a page reload can restore the
  // session without forcing re-login (08 §11).
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const { data } = await apiClient.post<LoginResult>('/auth/refresh', {});
        if (!cancelled) {
          setAccessToken(data.accessToken);
          setUser(data.user);
        }
      } catch {
        // No valid refresh cookie → remain logged out.
      } finally {
        if (!cancelled) setIsInitialising(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      user,
      isAuthenticated: user !== null,
      isInitialising,
      // Client permission list only hides/disables UI; the server still enforces
      // authorization on every call (06 §5). Convenience, not security.
      hasPermission: (permission) => user?.permissions.includes(permission) ?? false,
      login: async (userName, password) => {
        try {
          const { data } = await apiClient.post<LoginResult>('/auth/login', { userName, password });
          setAccessToken(data.accessToken);
          setUser(data.user);
        } catch (error) {
          throw toApiError(error);
        }
      },
      logout: async () => {
        try {
          await apiClient.post('/auth/logout', {});
        } catch {
          // Best-effort; clear local state regardless.
        }
        clearSession();
      },
    }),
    [user, isInitialising, clearSession],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthState {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider.');
  }
  return context;
}
