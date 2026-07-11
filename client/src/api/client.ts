import axios, { AxiosError, type AxiosRequestConfig } from 'axios';
import type { LoginResult, ProblemDetails } from './types';

/**
 * Central Axios instance (07 §9). Responsibilities:
 *  - base URL + credentials (refresh cookie is HttpOnly, sent automatically)
 *  - attach the in-memory JWT access token (06 §6 — access token kept in memory)
 *  - on 401, transparently refresh once via /auth/refresh and retry (07 §9, 08 §11)
 *  - normalise RFC 9457 problem-details errors for the UI (05 §6)
 */
export const apiClient = axios.create({
  baseURL: '/api/v1',
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true, // send/receive the HttpOnly refresh cookie
});

let accessToken: string | null = null;

// Called by the auth layer when a session ends or refresh fails, so the app can
// react (e.g. show the login screen) rather than loop on 401s.
let onSessionExpired: (() => void) | null = null;

export function setAccessToken(token: string | null): void {
  accessToken = token;
}

export function setOnSessionExpired(handler: (() => void) | null): void {
  onSessionExpired = handler;
}

apiClient.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }
  return config;
});

// --- Single-flight 401 → refresh → retry -----------------------------------
let refreshPromise: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
  // Only one refresh in flight at a time; concurrent 401s await the same call.
  refreshPromise ??= (async () => {
    try {
      const { data } = await axios.post<LoginResult>(
        '/api/v1/auth/refresh',
        {},
        { withCredentials: true },
      );
      setAccessToken(data.accessToken);
      return data.accessToken;
    } catch {
      return null;
    } finally {
      refreshPromise = null;
    }
  })();
  return refreshPromise;
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as (AxiosRequestConfig & { _retried?: boolean }) | undefined;
    const status = error.response?.status;
    const isAuthCall = original?.url?.includes('/auth/');

    if (status === 401 && original && !original._retried && !isAuthCall) {
      original._retried = true;
      const newToken = await refreshAccessToken();
      if (newToken) {
        original.headers = { ...original.headers, Authorization: `Bearer ${newToken}` };
        return apiClient(original);
      }
      // Refresh failed → the session is over.
      setAccessToken(null);
      onSessionExpired?.();
    }

    return Promise.reject(error);
  },
);

/** A normalised error the UI can render consistently. */
export class ApiError extends Error {
  readonly status: number;
  readonly correlationId?: string;
  readonly fieldErrors?: Record<string, string[]>;

  constructor(
    status: number,
    message: string,
    correlationId?: string,
    fieldErrors?: Record<string, string[]>,
  ) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.correlationId = correlationId;
    this.fieldErrors = fieldErrors;
  }
}

export function toApiError(error: unknown): ApiError {
  if (axios.isAxiosError(error)) {
    const axiosError = error as AxiosError<ProblemDetails>;
    const problem = axiosError.response?.data;
    const status = axiosError.response?.status ?? 0;
    return new ApiError(
      status,
      problem?.detail || problem?.title || axiosError.message || 'Request failed.',
      problem?.correlationId,
      problem?.errors,
    );
  }
  return new ApiError(0, 'An unexpected error occurred.');
}

/** HTTP status from either an ApiError (mutations) or a raw AxiosError (queries). */
export function errorStatus(error: unknown): number | undefined {
  if (error instanceof ApiError) return error.status;
  if (axios.isAxiosError(error)) return error.response?.status;
  return undefined;
}
