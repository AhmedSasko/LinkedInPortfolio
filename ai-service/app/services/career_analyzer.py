import structlog
from app.llm.base import BaseLLMProvider
from app.llm.prompts import career_analysis
from app.schemas.analysis import CareerAnalysisResult

log = structlog.get_logger()


class CareerAnalyzer:
    def __init__(self, llm: BaseLLMProvider):
        self.llm = llm

    async def analyze(self, profile_data: dict) -> CareerAnalysisResult:
        system_prompt, user_prompt = career_analysis.build_prompt(profile_data)
        try:
            raw = await self.llm.generate_structured(system_prompt, user_prompt)
            result = CareerAnalysisResult(
                career_trajectory=raw.get("career_trajectory"),
                years_of_experience=raw.get("years_of_experience"),
                progression_score=raw.get("progression_score"),
                role_transitions=raw.get("role_transitions", []),
                industry_focus=raw.get("industry_focus"),
                career_velocity=raw.get("career_velocity"),
                predicted_next_roles=raw.get("predicted_next_roles", []),
                career_risks=raw.get("career_risks", []),
                strategic_advice=raw.get("strategic_advice", []),
                raw=raw,
            )
            log.info("career_analyzer.done", trajectory=result.career_trajectory)
            return result
        except Exception as e:
            log.error("career_analyzer.failed", error=str(e))
            raise
