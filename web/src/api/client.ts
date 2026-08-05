/*
    One place that knows how to talk to the API. Every call sends credentials so the browser
    carries the refresh cookie; the access token is passed explicitly by the caller because it
    lives in memory only — never localStorage, where any XSS becomes permanent account
    handover (spec §7).
*/
const BASE = import.meta.env.VITE_API_BASE ?? 'https://localhost:7001/api/v1';

export type Problem = { title?: string; status?: number; code?: string; detail?: string };

export class ApiError extends Error {
  readonly status: number;
  readonly code?: string;

  constructor(status: number, problem: Problem | null) {
    super(problem?.title ?? `Request failed (${status})`);
    this.status = status;
    this.code = problem?.code;
  }
}

async function request<T>(path: string, init: RequestInit = {}, accessToken?: string): Promise<T> {
  const headers = new Headers(init.headers);
  if (init.body) headers.set('Content-Type', 'application/json');
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`);

  const res = await fetch(`${BASE}${path}`, { ...init, headers, credentials: 'include' });

  if (!res.ok) {
    // Every error is RFC 7807 ProblemDetails, so one shape covers the whole API.
    const problem = await res.json().catch(() => null);
    throw new ApiError(res.status, problem);
  }

  return res.status === 204 ? (undefined as T) : ((await res.json()) as T);
}

export type AuthResponse = { accessToken: string; userId: string; displayName: string };

export type Profile = {
  id: string;
  email: string;
  displayName: string;
  bio: string | null;
  avatarUrl: string | null;
  sex: number | null;
  experienceLevel: number | null;
  units: number;
  currentBodyweightKg: number | null;
  createdAt: string;
};

export const api = {
  register: (email: string, password: string, displayName: string) =>
    request<AuthResponse>('/auth/register', {
      method: 'POST',
      body: JSON.stringify({ email, password, displayName }),
    }),

  login: (email: string, password: string) =>
    request<AuthResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),

  /* No body and no bearer token: the httpOnly cookie is the entire credential. */
  refresh: () => request<AuthResponse>('/auth/refresh', { method: 'POST' }),

  logout: () => request<void>('/auth/logout', { method: 'POST' }),

  me: (token: string) => request<Profile>('/me', {}, token),

  updateProfile: (token: string, body: Record<string, unknown>) =>
    request<Profile>('/me', { method: 'PATCH', body: JSON.stringify(body) }, token),

  recordBodyweight: (token: string, weightKg: number) =>
    request<{ id: string; weightKg: number; recordedAt: string }>(
      '/me/bodyweight',
      { method: 'POST', body: JSON.stringify({ weightKg }) },
      token,
    ),
};
