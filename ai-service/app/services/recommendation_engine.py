import structlog
from app.llm.base import BaseLLMProvider
from app.llm.prompts import recommendations
from app.schemas.analysis import RecommendationsResult

log = structlog.get_logger()


class RecommendationEngine:
    def __init__(self, llm: BaseLLMProvider):
        self.llm = llm

    async def analyze(self, profile_data: dict, analysis_context: dict) -> RecommendationsResult:
        system_prompt, user_prompt = recommendations.build_prompt(profile_data, analysis_context)
        try:
            raw = await self.llm.generate_structured(system_prompt, user_prompt)
            result = RecommendationsResult(
                immediate_actions=raw.get("immediate_actions", []),
                short_term_goals=raw.get("short_term_goals", []),
                long_term_goals=raw.get("long_term_goals", []),
                learning_path=raw.get("learning_path", []),
                networking_strategy=raw.get("networking_strategy"),
                profile_quick_fixes=raw.get("profile_quick_fixes", []),
                job_search_strategy=raw.get("job_search_strategy"),
                personal_brand_tips=raw.get("personal_brand_tips", []),
                raw=raw,
            )
            log.info(
                "recommendation_engine.done",
                actions=len(result.immediate_actions),
                learning_items=len(result.learning_path),
            )
            return result
        except Exception as e:
            log.error("recommendation_engine.failed", error=str(e))
            raise
