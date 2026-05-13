import axiosInstance from './axiosInstance';
import type { Resume } from '../types';

export const resumeApi = {
  upload: (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return axiosInstance.post<Resume>('/resume/upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },

  list: () =>
    axiosInstance.get<Resume[]>('/resume/uploads'),

  getById: (id: number) =>
    axiosInstance.get<Resume>(`/resume/${id}`),

  importToProfile: (id: number) =>
    axiosInstance.post(`/resume/${id}/import`),
};
