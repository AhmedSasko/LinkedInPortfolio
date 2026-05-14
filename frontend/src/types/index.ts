// ─── Auth ──────────────────────────────────────────────────────────────────────
export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  userId: number;
  email: string;
  isAdmin: boolean;
}

// ─── Profile ───────────────────────────────────────────────────────────────────
export interface ProfileExperience {
  id?: number;
  title: string;
  company: string;
  location?: string;
  startDate?: string;
  endDate?: string;
  isCurrent: boolean;
  description?: string;
}

export interface ProfileEducation {
  id?: number;
  school: string;
  degree?: string;
  fieldOfStudy?: string;
  startYear?: string;
  endYear?: string;
}

export interface ProfileSkill {
  id?: number;
  name: string;
  endorsementCount?: number;
}

export interface ProfileCertification {
  id?: number;
  name: string;
  issuingOrganization?: string;
  issueDate?: string;
  credentialUrl?: string;
}

export interface ProfileProject {
  id?: number;
  title: string;
  description?: string;
  url?: string;
  startDate?: string;
  endDate?: string;
}

export interface Profile {
  id: number;
  userId: number;
  name?: string;
  headline?: string;
  location?: string;
  about?: string;
  photoUrl?: string;
  fetchedAt: string;
  experiences: ProfileExperience[];
  educations: ProfileEducation[];
  skills: ProfileSkill[];
  certifications: ProfileCertification[];
  projects: ProfileProject[];
}

export interface ProfileStatus {
  hasProfile: boolean;
  lastSyncedAt?: string;
  completenessScore?: number;
  isLinkedInConnected: boolean;
}

// ─── Analysis ──────────────────────────────────────────────────────────────────
export interface ProfileScoringResult {
  overall_score?: number;
  completeness_score?: number;
  headline_score?: number;
  about_score?: number;
  experience_score?: number;
  skills_score?: number;
  education_score?: number;
  projects_score?: number;
  strengths?: string[];
  improvements?: string[];
}

export interface SkillsIntelligenceResult {
  current_skills_assessment?: string;
  skill_gaps?: string[];
  trending_skills?: string[];
  skill_clusters?: Array<{ name: string; skills: string[]; proficiency?: string }>;
  recommendations?: string[];
}

export interface CareerAnalysisResult {
  career_trajectory?: string;
  years_of_experience?: number;
  progression_score?: number;
  role_transitions?: Array<{ from: string; to: string; insight?: string }>;
  industry_focus?: string;
  career_velocity?: string;
  predicted_next_roles?: string[];
  career_risks?: string[];
  strategic_advice?: string[];
}

export interface AtsOptimizationResult {
  overall_ats_score?: number;
  keyword_density?: { score: number; top_keywords: string[] };
  headline_analysis?: { issues: string[]; suggestion: string };
  about_analysis?: { keyword_gaps: string[]; suggestions: string[] };
  experience_improvements?: Array<{ role: string; original: string; improved: string }>;
  missing_keywords?: string[];
  formatting_issues?: string[];
  quick_wins?: Array<{ action: string; impact: string }>;
}

export interface SeniorityEstimationResult {
  estimated_level?: string;
  confidence_score?: number;
  years_of_experience?: number;
  evidence?: string[];
  leadership_indicators?: string[];
  specialization_depth?: string;
  next_level?: string;
  next_level_gap?: string[];
  comparable_titles?: string[];
}

export interface RecommendationsResult {
  immediate_actions?: Array<{ title: string; description: string; impact: string }>;
  short_term_goals?: Array<{ goal: string; steps: string[]; success_metric: string }>;
  long_term_goals?: Array<{ goal: string; milestones: string[]; success_metric: string }>;
  learning_path?: Array<{ name: string; platform: string; reason: string; priority: string }>;
  networking_strategy?: string;
  profile_quick_fixes?: Array<{ fix: string; score_improvement: number }>;
  job_search_strategy?: { target_companies?: string[]; target_roles?: string[]; approach?: string };
  personal_brand_tips?: string[];
}

export interface AnalysisResult {
  id: number;
  userId: number;
  profileId: number;
  analysisType: string;
  status: 'pending' | 'processing' | 'completed' | 'failed';
  result?: {
    profile_scoring?: ProfileScoringResult;
    skills_intelligence?: SkillsIntelligenceResult;
    career_analysis?: CareerAnalysisResult;
    ats_optimization?: AtsOptimizationResult;
    seniority_estimation?: SeniorityEstimationResult;
    recommendations?: RecommendationsResult;
  };
  overallScore?: number;
  errorMessage?: string;
  requestedAt: string;
  completedAt?: string;
}

export interface AnalysisStatus {
  hasAnalysis: boolean;
  status?: string;
  requestedAt?: string;
  completedAt?: string;
}

// ─── Scrape Progress ───────────────────────────────────────────────────────────
export interface ScrapeStep {
  key: string;
  label: string;
  status: 'pending' | 'running' | 'done' | 'error';
}

export interface ScrapeProgress {
  isRunning: boolean;
  steps: ScrapeStep[];
  error: string | null;
  result: Profile | null;
}

// ─── Resume ────────────────────────────────────────────────────────────────────
export interface Resume {
  id: number;
  userId: number;
  fileName: string;
  fileType: string;
  fileSize: number;
  status: string;
  parsedData?: {
    name?: string;
    email?: string;
    skills?: string[];
    experiences?: ProfileExperience[];
    educations?: ProfileEducation[];
  };
  createdAt: string;
}
