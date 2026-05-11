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
  fetchedAt: string
  name: string
  headline: string
  location: string
  about: string
  photoBase64: string
  experience: ExperienceDto[]
  education: EducationDto[]
  skills: SkillDto[]
  projects: ProjectDto[]
  certifications: CertificationDto[]
}

export interface ProfileStatusDto {
  lastSyncedAt: string | null
  experienceCount: number
  educationCount: number
  skillCount: number
  projectCount: number
  certificationCount: number
}

export interface ProfileSummaryDto {
  id: number
  fetchedAt: string
  name: string
  headline: string
  experienceCount: number
  educationCount: number
  skillCount: number
  projectCount: number
  certificationCount: number
}

export interface SyncResultDto {
  success: boolean
  syncedAt: string | null
  message: string
}

// Auth
export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  token: string;
}

// Auth JWT payload (decoded from token)
export interface AuthUser {
  userId: number;
  email: string;
  isAdmin: boolean;
}

// Import
export interface ImportRequest {
  linkedInUrl: string;
}

// Alias used by importProfile
export type ProfileData = ProfileDto;
