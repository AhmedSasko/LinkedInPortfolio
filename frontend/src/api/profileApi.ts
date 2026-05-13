import axiosInstance from './axiosInstance';
import type { Profile, ProfileStatus, ScrapeProgress } from '../types';

export const profileApi = {
  getLatest: () =>
    axiosInstance.get<Profile>('/profile/latest').catch((e) => {
      if (e.response?.status === 404) return { data: null as unknown as Profile };
      throw e;
    }),

  getById: (id: number) =>
    axiosInstance.get<Profile>(`/profile/${id}`),

  update: (data: Partial<Profile>) =>
    axiosInstance.put<Profile>('/profile', data),

  getStatus: () =>
    axiosInstance.get<ProfileStatus>('/profile/status'),

  scrapeStart: (linkedInUrl: string, cookie: string) =>
    axiosInstance.post('/profile/scrape', { linkedInUrl, liAtCookie: cookie }),

  getScrapeProgress: () =>
    axiosInstance.get<ScrapeProgress>('/profile/scrape-progress'),

  getLinkedInSyncUrl: () =>
    axiosInstance.get<{ url: string }>('/auth/linkedin/sync-url'),
};
