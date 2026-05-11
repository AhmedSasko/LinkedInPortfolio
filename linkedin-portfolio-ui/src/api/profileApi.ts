import axios from 'axios'
import type { ImportRequest, ProfileData, ProfileDto, ProfileStatusDto, ProfileSummaryDto, SyncResultDto } from '../types/profile'

const api = axios.create({ baseURL: '/api' })

function authHeader(): Record<string, string> {
  const token = localStorage.getItem('token')
  return token ? { Authorization: `Bearer ${token}` } : {}
}

// Attach JWT to every request automatically
api.interceptors.request.use((config) => {
  const headers = authHeader()
  if (headers.Authorization) {
    config.headers.Authorization = headers.Authorization
  }
  return config
})

export async function fetchProfile(): Promise<ProfileDto | null> {
  const res = await api.get<ProfileDto>('/profile')
  if (res.status === 204) return null
  return res.data
}

export async function fetchAllProfiles(): Promise<ProfileSummaryDto[]> {
  const res = await api.get<ProfileSummaryDto[]>('/profile/all')
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

export async function triggerSync(): Promise<SyncResultDto> {
  const res = await api.post<SyncResultDto>('/sync')
  return res.data
}

export async function importProfile(req: ImportRequest): Promise<ProfileData> {
  const res = await fetch('/api/profile/import', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...authHeader() },
    body: JSON.stringify(req),
  })
  if (!res.ok) {
    const data = await res.json().catch(() => ({}))
    throw new Error((data as { message?: string }).message ?? `HTTP ${res.status}`)
  }
  return res.json()
}
