export interface ExperienceDto {
  title: string
  company: string
  startDate: string
  endDate: string
  description: string
  isCurrent: boolean
}

export interface EducationDto {
  school: string
  degree: string
  fieldOfStudy: string
  startYear: string
  endYear: string
}

export interface SkillDto {
  name: string
  endorsementCount: number
}

export interface ProjectDto {
  title: string
  description: string
  url: string
  startDate: string
  endDate: string
}

export interface CertificationDto {
  name: string
  issuingOrganization: string
  issueDate: string
  credentialUrl: string
}

export interface ProfileDto {
  id: number
  fetchedAt: string
  name: string
  headline: string
  location: string
  about: string
  photoBase64: string
  photoUrl?: string
  experiences: ExperienceDto[]
  educations: EducationDto[]
  skills: SkillDto[]
  projects: ProjectDto[]
  certifications: CertificationDto[]
}

export interface UpdateProfileRequest {
  name: string
  headline: string
  location: string
  about: string
  photoUrl?: string
  experiences: ExperienceDto[]
  educations: EducationDto[]
  skills: SkillDto[]
  projects: ProjectDto[]
  certifications: CertificationDto[]
}

export interface ProfileStatusDto {
  hasProfile: boolean
  lastSyncedAt: string | null
}

export interface ProfileSummaryDto {
  id: number
  fetchedAt: string
  name: string
  headline: string
  userEmail: string
}

// Auth
export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
}

export interface AuthResponse {
  token: string
}

// Auth JWT payload (decoded from token)
export interface AuthUser {
  userId: number
  email: string
  isAdmin: boolean
}

