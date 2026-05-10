import axios from 'axios'
import type { ProfileDto, ProfileStatusDto, SyncResultDto } from '../types/profile'

const api = axios.create({ baseURL: '/api' })

export async function fetchProfile(): Promise<ProfileDto | null> {
  const res = await api.get<ProfileDto>('/profile')
  if (res.status === 204) return null
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
