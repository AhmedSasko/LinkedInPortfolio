from pydantic import BaseModel
from typing import Optional


class ExperienceSchema(BaseModel):
    title: Optional[str] = None
    company: Optional[str] = None
    start_date: Optional[str] = None
    end_date: Optional[str] = None
    is_current: bool = False
    description: Optional[str] = None
    location: Optional[str] = None


class EducationSchema(BaseModel):
    school: Optional[str] = None
    degree: Optional[str] = None
    field_of_study: Optional[str] = None
    start_year: Optional[str] = None
    end_year: Optional[str] = None


class SkillSchema(BaseModel):
    name: Optional[str] = None
    endorsement_count: int = 0


class CertificationSchema(BaseModel):
    name: Optional[str] = None
    issuing_organization: Optional[str] = None
    issue_date: Optional[str] = None
    credential_url: Optional[str] = None


class ProjectSchema(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None
    url: Optional[str] = None
    start_date: Optional[str] = None
    end_date: Optional[str] = None


class ProfileData(BaseModel):
    name: Optional[str] = None
    headline: Optional[str] = None
    location: Optional[str] = None
    about: Optional[str] = None
    photo_url: Optional[str] = None
    photo_base64: Optional[str] = None
    experiences: list[ExperienceSchema] = []
    educations: list[EducationSchema] = []
    skills: list[SkillSchema] = []
    certifications: list[CertificationSchema] = []
    projects: list[ProjectSchema] = []
