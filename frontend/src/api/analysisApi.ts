import axiosInstance from './axiosInstance';
import type { AnalysisResult, AnalysisStatus } from '../types';

export const analysisApi = {
  requestAnalysis: () =>
    axiosInstance.post<import('../types').AnalysisResult>('/analysis/request'),

  getLatest: () =>
    axiosInstance.get<AnalysisResult>('/analysis/latest').catch((e) => {
      if (e.response?.status === 404) return { data: null as unknown as AnalysisResult };
      throw e;
    }),

  getById: (id: number) =>
    axiosInstance.get<AnalysisResult>(`/analysis/${id}`),

  getStatus: () =>
    axiosInstance.get<AnalysisStatus>('/analysis/status'),
};
