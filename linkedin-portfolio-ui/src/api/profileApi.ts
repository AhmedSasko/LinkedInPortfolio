import axios from 'axios'
import type { ProfileDto, ProfileStatusDto, ProfileSummaryDto, SyncResultDto } from '../types/profile'

const api = axios.create({ baseURL: '/api' })

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
