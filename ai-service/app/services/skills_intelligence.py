import structlog
from app.llm.base import BaseLLMProvider
from app.llm.prompts import skills_analysis
from app.schemas.analysis import SkillsIntelligenceResult

log = structlog.get_logger()


class SkillsIntelligenceAnalyzer:
    def __init__(self, llm: BaseLLMProvider):
        self.llm = llm

    async def analyze(self, profile_data: dict) -> SkillsIntelligenceResult:
        system_prompt, user_prompt = skills_analysis.build_prompt(profile_data)
        try:
            raw = await self.llm.generate_structured(system_prompt, user_prompt)
            result = SkillsIntelligenceResult(
                current_skills_assessment=raw.get("current_skills_assessment"),
                skill_gaps=raw.get("skill_gaps", []),
                trending_skills=raw.get("trending_skills", []),
                skill_clusters=raw.get("skill_clusters", []),
                recommendations=raw.get("recommendations", []),
                raw=raw,
            )
            log.info("skills_intelligence.done", gaps=len(result.skill_gaps))
            return result
        except Exception as e:
            log.error("skills_intelligence.failed", error=str(e))
            raise
