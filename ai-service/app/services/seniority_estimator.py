import structlog
from app.llm.base import BaseLLMProvider
from app.llm.prompts import seniority_estimation
from app.schemas.analysis import SeniorityEstimationResult

log = structlog.get_logger()


class SeniorityEstimator:
    def __init__(self, llm: BaseLLMProvider):
        self.llm = llm

    async def analyze(self, profile_data: dict) -> SeniorityEstimationResult:
        system_prompt, user_prompt = seniority_estimation.build_prompt(profile_data)
        try:
            raw = await self.llm.generate_structured(system_prompt, user_prompt)
            result = SeniorityEstimationResult(
                estimated_level=raw.get("estimated_level"),
                confidence_score=raw.get("confidence_score"),
                years_of_experience=raw.get("years_of_experience"),
                evidence=raw.get("evidence", []),
                leadership_indicators=raw.get("leadership_indicators", []),
                specialization_depth=raw.get("specialization_depth"),
                next_level=raw.get("next_level"),
                next_level_gap=raw.get("next_level_gap", []),
                comparable_titles=raw.get("comparable_titles", []),
                raw=raw,
            )
            log.info("seniority_estimator.done", level=result.estimated_level)
            return result
        except Exception as e:
            log.error("seniority_estimator.failed", error=str(e))
            raise
