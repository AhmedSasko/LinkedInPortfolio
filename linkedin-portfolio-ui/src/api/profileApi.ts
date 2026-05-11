import axios from 'axios'
import type { ImportRequest, ProfileData, ProfileDto, ProfileStatusDto, ProfileSummaryDto } from '../types/profile'

const api = axios.create({ baseURL: '/api' })

// Attach JWT to every request automatically
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

export async function fetchProfile(): Promise<ProfileDto | null> {
  try {
    const res = await api.get<ProfileDto>('/profile/latest')
    return res.data
  } catch (err: unknown) {
    // 404 means no profile imported yet — treat as empty, not error
    if (axios.isAxiosError(err) && err.response?.status === 404) return null
    throw err
  }
}

export async function fetchAllProfiles(): Promise<ProfileSummaryDto[]> {
  const res = await api.get<ProfileSummaryDto[]>('/profile')
  return res.data
}

export async function fetchProfileById(id: number): Promise<ProfileDto> {
  const res = await api.get<ProfileDto>(`/profile/${id}`)
  return res.data
}

export async function fetchStatus(): Promise<ProfileStatusDto> {
  const res = await api.get<ProfileStatusDto>('/profile/status')
  return res.data
}

export async function importProfile(req: ImportRequest): Promise<ProfileData> {
  const res = await api.post<ProfileData>('/profile/import', req)
  return res.data
}
