import { useSession } from '@/auth/session';
import type { SessionResponse } from './types';

// Every call is a same-origin relative path (Q-231). The access token lives only in memory (the
// session store); the refresh token is an httpOnly cookie the browser attaches to /api/v1/auth/*
// by itself (Q-232) — this module never sees it.

export class ApiError extends Error {
  readonly status: number;
  readonly code: string | undefined;
  readonly errors: { code: string; message: string }[];

  constructor(status: number, code: string | undefined, message: string, errors: { code: string; message: string }[] = []) {
    super(message);
    this.status = status;
    this.code = code;
    this.errors = errors;
  }
}

// NFR-003: explicit, non-technical feedback — used when the server gave no catalogue body (e.g. a bare 403).
const FALLBACK_MESSAGE: Record<number, string> = {
  401: 'Your session has expired. Please sign in again.',
  403: "You don't have permission to do this.",
  404: "We couldn't find what you're looking for.",
};
const GENERIC = 'Something went wrong on our end. Please try again, and contact support if it continues.';

let refreshInFlight: Promise<boolean> | null = null;

/** POST /auth/refresh using the cookie. Concurrent 401s share one refresh (the token rotates on use). */
export function refreshSession(): Promise<boolean> {
  refreshInFlight ??= (async () => {
    try {
      const res = await fetch('/api/v1/auth/refresh', { method: 'POST', credentials: 'same-origin' });
      if (!res.ok) {
        useSession.getState().clear();
        return false;
      }
      const body = (await res.json()) as SessionResponse;
      useSession.getState().setSession(body.accessToken, body.user);
      return true;
    } catch {
      return false;
    } finally {
      refreshInFlight = null;
    }
  })();
  return refreshInFlight;
}

async function toError(res: Response): Promise<ApiError> {
  let body: unknown = null;
  try {
    body = await res.json();
  } catch {
    /* empty body (framework 401/403) */
  }
  const b = body as { code?: string; message?: string; errors?: unknown } | null;
  if (b && Array.isArray(b.errors)) {
    const errors = b.errors as { code: string; message: string }[];
    return new ApiError(res.status, errors[0]?.code, errors.map((e) => e.message).join(' '), errors);
  }
  if (b && typeof b.code === 'string') return new ApiError(res.status, b.code, b.message ?? GENERIC);
  if (b && res.status === 400) return new ApiError(400, 'ERR-901', 'Something about this request wasn’t right — please check and try again.');
  return new ApiError(res.status, undefined, FALLBACK_MESSAGE[res.status] ?? GENERIC);
}

export interface RequestOptions {
  method?: string;
  body?: unknown;
  headers?: Record<string, string>;
  /** anonymous endpoints skip the bearer header and never trigger a refresh */
  anonymous?: boolean;
  raw?: boolean;
}

export async function request<T>(path: string, options: RequestOptions = {}, retried = false): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json', ...options.headers };
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';
  const token = useSession.getState().accessToken;
  if (!options.anonymous && token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(`/api/v1${path}`, {
    method: options.method ?? 'GET',
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    credentials: 'same-origin',
  });

  if (res.status === 401 && !options.anonymous && !retried && !path.startsWith('/auth/')) {
    if (await refreshSession()) return request<T>(path, options, true);
  }
  if (!res.ok) throw await toError(res);
  if (options.raw) return res as unknown as T;
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const api = {
  get: <T>(path: string, opts?: RequestOptions) => request<T>(path, opts),
  post: <T>(path: string, body?: unknown, opts?: RequestOptions) => request<T>(path, { ...opts, method: 'POST', body }),
  put: <T>(path: string, body?: unknown, opts?: RequestOptions) => request<T>(path, { ...opts, method: 'PUT', body }),
  del: <T>(path: string, opts?: RequestOptions) => request<T>(path, { ...opts, method: 'DELETE' }),
};

export const idempotencyKey = () => ({ 'Idempotency-Key': crypto.randomUUID() });

export function errorMessage(error: unknown): string {
  return error instanceof ApiError ? error.message : GENERIC;
}
