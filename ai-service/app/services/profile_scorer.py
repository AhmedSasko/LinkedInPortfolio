import json
import structlog
from app.llm.base import BaseLLMProvider
from app.llm.prompts import profile_scoring
from app.schemas.analysis import ProfileScoringResult

log = structlog.get_logger()


class ProfileScorer:
    def __init__(self, llm: BaseLLMProvider):
        self.llm = llm

    async def analyze(self, profile_data: dict) -> ProfileScoringResult:
        system_prompt, user_prompt = profile_scoring.build_prompt(profile_data)
        try:
            raw = await self.llm.generate_structured(system_prompt, user_prompt)
            result = ProfileScoringResult(
                overall_score=raw.get("overall_score"),
                completeness_score=raw.get("completeness_score"),
                headline_score=raw.get("headline_score"),
                about_score=raw.get("about_score"),
                experience_score=raw.get("experience_score"),
                skills_score=raw.get("skills_score"),
                education_score=raw.get("education_score"),
                projects_score=raw.get("projects_score"),
                strengths=raw.get("strengths", []),
                improvements=raw.get("improvements", []),
                raw=raw,
            )
            log.info("profile_scorer.done", score=result.overall_score)
            return result
        except Exception as e:
            log.error("profile_scorer.failed", error=str(e))
            raise
