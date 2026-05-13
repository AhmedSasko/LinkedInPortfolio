import structlog
from app.llm.base import BaseLLMProvider
from app.llm.prompts import ats_optimization
from app.schemas.analysis import AtsOptimizationResult

log = structlog.get_logger()


class AtsOptimizer:
    def __init__(self, llm: BaseLLMProvider):
        self.llm = llm

    async def analyze(self, profile_data: dict) -> AtsOptimizationResult:
        system_prompt, user_prompt = ats_optimization.build_prompt(profile_data)
        try:
            raw = await self.llm.generate_structured(system_prompt, user_prompt)
            result = AtsOptimizationResult(
                overall_ats_score=raw.get("overall_ats_score"),
                keyword_density=raw.get("keyword_density"),
                headline_analysis=raw.get("headline_analysis"),
                about_analysis=raw.get("about_analysis"),
                experience_improvements=raw.get("experience_improvements", []),
                missing_keywords=raw.get("missing_keywords", []),
                formatting_issues=raw.get("formatting_issues", []),
                quick_wins=raw.get("quick_wins", []),
                raw=raw,
            )
            log.info("ats_optimizer.done", score=result.overall_ats_score)
            return result
        except Exception as e:
            log.error("ats_optimizer.failed", error=str(e))
            raise
