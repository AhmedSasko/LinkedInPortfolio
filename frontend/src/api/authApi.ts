import axiosInstance from './axiosInstance';
import type { AuthResponse } from '../types';

export const authApi = {
  register: (email: string, password: string) =>
    axiosInstance.post<AuthResponse>('/auth/register', { email, password }),

  login: (email: string, password: string) =>
    axiosInstance.post<AuthResponse>('/auth/login', { email, password }),

  refresh: (refreshToken: string) =>
    axiosInstance.post<AuthResponse>('/auth/refresh', { refreshToken }),

  revoke: (refreshToken: string) =>
    axiosInstance.post('/auth/revoke', { refreshToken }),

  getGoogleUrl: () =>
    axiosInstance.get<{ url: string }>('/auth/google'),

  getLinkedInUrl: () =>
    axiosInstance.get<{ url: string }>('/auth/linkedin'),
};
