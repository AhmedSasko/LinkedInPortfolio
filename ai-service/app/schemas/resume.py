from pydantic import BaseModel
from typing import Optional


class ResumeParseResult(BaseModel):
    name: Optional[str] = None
    email: Optional[str] = None
    phone: Optional[str] = None
    location: Optional[str] = None
    summary: Optional[str] = None
    skills: list[str] = []
    experiences: list[dict] = []
    educations: list[dict] = []
    certifications: list[dict] = []
    projects: list[dict] = []
    languages: list[str] = []
    raw_text: Optional[str] = None
