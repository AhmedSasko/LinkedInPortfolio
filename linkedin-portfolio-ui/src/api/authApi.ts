import type { AuthResponse, LoginRequest, RegisterRequest } from '../types/profile';

const API_BASE = '/api';

async function request<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const data = await res.json().catch(() => ({}));
    throw new Error((data as { message?: string }).message ?? `HTTP ${res.status}`);
  }
  return res.json();
}

export async function register(req: RegisterRequest): Promise<AuthResponse> {
  return request<AuthResponse>('/auth/register', req);
}

export async function login(req: LoginRequest): Promise<AuthResponse> {
  return request<AuthResponse>('/auth/login', req);
}
