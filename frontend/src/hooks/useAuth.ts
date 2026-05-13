import { useState, useCallback } from 'react';
import { authApi } from '../api/authApi';
import type { AuthResponse } from '../types';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  userId: number | null;
  email: string | null;
  isAdmin: boolean;
}

function loadFromStorage(): AuthState {
  return {
    accessToken: localStorage.getItem('accessToken'),
    refreshToken: localStorage.getItem('refreshToken'),
    userId: Number(localStorage.getItem('userId')) || null,
    email: localStorage.getItem('userEmail'),
    isAdmin: localStorage.getItem('isAdmin') === 'true',
  };
}

function saveToStorage(res: AuthResponse) {
  localStorage.setItem('accessToken', res.accessToken);
  localStorage.setItem('refreshToken', res.refreshToken);
  localStorage.setItem('userId', String(res.userId));
  localStorage.setItem('userEmail', res.email);
  localStorage.setItem('isAdmin', String(res.isAdmin));
}

function clearStorage() {
  ['accessToken', 'refreshToken', 'userId', 'userEmail', 'isAdmin'].forEach((k) =>
    localStorage.removeItem(k)
  );
}

export function useAuth() {
  const [auth, setAuth] = useState<AuthState>(loadFromStorage);

  const setFromResponse = useCallback((res: AuthResponse) => {
    saveToStorage(res);
    setAuth({
      accessToken: res.accessToken,
      refreshToken: res.refreshToken,
      userId: res.userId,
      email: res.email,
      isAdmin: res.isAdmin,
    });
  }, []);

  const logout = useCallback(async () => {
    const token = localStorage.getItem('refreshToken');
    if (token) {
      try {
        await authApi.revoke(token);
      } catch {
        // ignore
      }
    }
    clearStorage();
    setAuth({ accessToken: null, refreshToken: null, userId: null, email: null, isAdmin: false });
  }, []);

  const isAuthenticated = !!auth.accessToken;

  return { ...auth, isAuthenticated, setFromResponse, logout };
}
