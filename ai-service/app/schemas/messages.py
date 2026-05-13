from pydantic import BaseModel
from typing import Optional
from .profile import ProfileData


class AnalysisRequestMessage(BaseModel):
    message_id: str
    user_id: int
    profile_id: int
    analysis_types: list[str] = ["full"]
    profile_data: ProfileData


class AnalysisResultMessage(BaseModel):
    message_id: str
    user_id: int
    profile_id: int
    status: str  # completed | failed
    result: Optional[dict] = None
    overall_score: Optional[int] = None
    error_message: Optional[str] = None


class ResumeParseRequestMessage(BaseModel):
    message_id: str
    user_id: int
    resume_id: int
    file_name: str
    file_content_base64: str
    file_type: str  # pdf | docx


class ResumeParseResultMessage(BaseModel):
    message_id: str
    user_id: int
    resume_id: int
    status: str  # completed | failed
    parsed_data: Optional[dict] = None
    error_message: Optional[str] = None
