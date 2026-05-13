from pydantic import BaseModel, field_validator
from typing import Optional, Any, Union


def _to_int(v: Any) -> Optional[int]:
    """Coerce float/string to int."""
    if v is None:
        return None
    try:
        return int(round(float(v)))
    except (TypeError, ValueError):
        return None


def _to_str(v: Any) -> Optional[str]:
    """Coerce list/dict/number to string."""
    if v is None:
        return None
    if isinstance(v, str):
        return v
    if isinstance(v, list):
        return ", ".join(str(i) for i in v)
    return str(v)


def _to_list_str(v: Any) -> list[str]:
    """Coerce dict/str/None to list[str]."""
    if v is None:
        return []
    if isinstance(v, list):
        return [str(i) for i in v]
    if isinstance(v, dict):
        return [f"{k}: {val}" for k, val in v.items()]
    if isinstance(v, str):
        return [v]
    return []


def _to_list_dict(v: Any) -> list[dict]:
    """Coerce dict/list/None to list[dict]."""
    if v is None:
        return []
    if isinstance(v, list):
        return [i if isinstance(i, dict) else {"value": i} for i in v]
    if isinstance(v, dict):
        return [{"key": k, "value": val} for k, val in v.items()]
    return []


class ProfileScoringResult(BaseModel):
    overall_score: Optional[int] = None
    completeness_score: Optional[int] = None
    headline_score: Optional[int] = None
    about_score: Optional[int] = None
    experience_score: Optional[int] = None
    skills_score: Optional[int] = None
    education_score: Optional[int] = None
    projects_score: Optional[int] = None
    strengths: list[str] = []
    improvements: list[str] = []
    raw: Optional[dict] = None

    @field_validator("overall_score", "completeness_score", "headline_score",
                     "about_score", "experience_score", "skills_score",
                     "education_score", "projects_score", mode="before")
    @classmethod
    def coerce_int(cls, v): return _to_int(v)

    @field_validator("strengths", "improvements", mode="before")
    @classmethod
    def coerce_list_str(cls, v): return _to_list_str(v)


class SkillsIntelligenceResult(BaseModel):
    current_skills_assessment: Optional[str] = None
    skill_gaps: list[str] = []
    trending_skills: list[str] = []
    skill_clusters: list[Any] = []
    recommendations: list[Any] = []
    raw: Optional[dict] = None

    @field_validator("current_skills_assessment", mode="before")
    @classmethod
    def coerce_str(cls, v): return _to_str(v)

    @field_validator("skill_gaps", "trending_skills", mode="before")
    @classmethod
    def coerce_list_str(cls, v): return _to_list_str(v)

    @field_validator("skill_clusters", "recommendations", mode="before")
    @classmethod
    def coerce_list_any(cls, v):
        if v is None: return []
        if isinstance(v, list): return v
        if isinstance(v, dict): return list(v.values())
        return [v]


class CareerAnalysisResult(BaseModel):
    career_trajectory: Optional[str] = None
    years_of_experience: Optional[float] = None
    progression_score: Optional[int] = None
    role_transitions: list[Any] = []
    industry_focus: Optional[str] = None
    career_velocity: Optional[str] = None
    predicted_next_roles: list[str] = []
    career_risks: list[str] = []
    strategic_advice: list[str] = []
    raw: Optional[dict] = None

    @field_validator("progression_score", mode="before")
    @classmethod
    def coerce_int(cls, v): return _to_int(v)

    @field_validator("years_of_experience", mode="before")
    @classmethod
    def coerce_years(cls, v):
        if v is None:
            return None
        if isinstance(v, (int, float)):
            return float(v)
        # Handle strings like "8-12 years", "10+ years", "~8 years"
        import re
        nums = re.findall(r"[\d.]+", str(v))
        return float(nums[0]) if nums else None

    @field_validator("industry_focus", "career_velocity", "career_trajectory", mode="before")
    @classmethod
    def coerce_str(cls, v): return _to_str(v)

    @field_validator("predicted_next_roles", "career_risks", "strategic_advice", mode="before")
    @classmethod
    def coerce_list_str(cls, v): return _to_list_str(v)

    @field_validator("role_transitions", mode="before")
    @classmethod
    def coerce_list_any(cls, v):
        if v is None: return []
        if isinstance(v, list): return v
        if isinstance(v, dict): return list(v.values())
        return [v]


class AtsOptimizationResult(BaseModel):
    overall_ats_score: Optional[int] = None
    keyword_density: Optional[Any] = None
    headline_analysis: Optional[Any] = None
    about_analysis: Optional[Any] = None
    experience_improvements: list[Any] = []
    missing_keywords: list[str] = []
    formatting_issues: list[str] = []
    quick_wins: list[Any] = []
    raw: Optional[dict] = None

    @field_validator("overall_ats_score", mode="before")
    @classmethod
    def coerce_int(cls, v): return _to_int(v)

    @field_validator("missing_keywords", "formatting_issues", mode="before")
    @classmethod
    def coerce_list_str(cls, v): return _to_list_str(v)

    @field_validator("experience_improvements", "quick_wins", mode="before")
    @classmethod
    def coerce_list_any(cls, v):
        if v is None: return []
        if isinstance(v, list): return v
        if isinstance(v, dict): return list(v.values())
        return [v]


class SeniorityEstimationResult(BaseModel):
    estimated_level: Optional[str] = None
    confidence_score: Optional[int] = None
    years_of_experience: Optional[float] = None
    evidence: list[str] = []
    leadership_indicators: list[str] = []
    specialization_depth: Optional[str] = None
    next_level: Optional[str] = None
    next_level_gap: list[str] = []
    comparable_titles: list[str] = []
    raw: Optional[dict] = None

    @field_validator("confidence_score", mode="before")
    @classmethod
    def coerce_int(cls, v): return _to_int(v)

    @field_validator("years_of_experience", mode="before")
    @classmethod
    def coerce_years(cls, v):
        if v is None:
            return None
        if isinstance(v, (int, float)):
            return float(v)
        import re
        nums = re.findall(r"[\d.]+", str(v))
        return float(nums[0]) if nums else None

    @field_validator("specialization_depth", "next_level", "estimated_level", mode="before")
    @classmethod
    def coerce_str(cls, v): return _to_str(v)

    @field_validator("evidence", "leadership_indicators", "next_level_gap",
                     "comparable_titles", mode="before")
    @classmethod
    def coerce_list_str(cls, v): return _to_list_str(v)


class RecommendationsResult(BaseModel):
    immediate_actions: list[Any] = []
    short_term_goals: list[Any] = []
    long_term_goals: list[Any] = []
    learning_path: list[Any] = []
    networking_strategy: Optional[str] = None
    profile_quick_fixes: list[Any] = []
    job_search_strategy: Optional[Any] = None
    personal_brand_tips: list[str] = []
    raw: Optional[dict] = None

    @field_validator("networking_strategy", mode="before")
    @classmethod
    def coerce_str(cls, v): return _to_str(v)

    @field_validator("personal_brand_tips", mode="before")
    @classmethod
    def coerce_list_str(cls, v): return _to_list_str(v)

    @field_validator("immediate_actions", "short_term_goals", "long_term_goals",
                     "learning_path", "profile_quick_fixes", mode="before")
    @classmethod
    def coerce_list_any(cls, v):
        if v is None: return []
        if isinstance(v, list): return v
        if isinstance(v, dict): return list(v.values())
        return [v]


class FullAnalysisResult(BaseModel):
    profile_scoring: Optional[ProfileScoringResult] = None
    skills_intelligence: Optional[SkillsIntelligenceResult] = None
    career_analysis: Optional[CareerAnalysisResult] = None
    ats_optimization: Optional[AtsOptimizationResult] = None
    seniority_estimation: Optional[SeniorityEstimationResult] = None
    recommendations: Optional[RecommendationsResult] = None
    overall_score: Optional[int] = None
    errors: dict[str, str] = {}
