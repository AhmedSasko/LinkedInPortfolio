import asyncio
import structlog
from app.llm.base import BaseLLMProvider
from app.schemas.analysis import FullAnalysisResult
from app.services.profile_scorer import ProfileScorer
from app.services.skills_intelligence import SkillsIntelligenceAnalyzer
from app.services.career_analyzer import CareerAnalyzer
from app.services.ats_optimizer import AtsOptimizer
from app.services.seniority_estimator import SeniorityEstimator
from app.services.recommendation_engine import RecommendationEngine

log = structlog.get_logger()


class AnalysisOrchestrator:
    def __init__(self, llm: BaseLLMProvider):
        self.llm = llm
        self.profile_scorer = ProfileScorer(llm)
        self.skills_analyzer = SkillsIntelligenceAnalyzer(llm)
        self.career_analyzer = CareerAnalyzer(llm)
        self.ats_optimizer = AtsOptimizer(llm)
        self.seniority_estimator = SeniorityEstimator(llm)
        self.recommendation_engine = RecommendationEngine(llm)

    async def run_full_analysis(self, profile_data: dict) -> FullAnalysisResult:
        log.info("orchestrator.start", name=profile_data.get("name"))
        errors: dict[str, str] = {}

        # Phase 1: Run 5 analyses concurrently
        tasks = {
            "profile_scoring": self.profile_scorer.analyze(profile_data),
            "skills_intelligence": self.skills_analyzer.analyze(profile_data),
            "career_analysis": self.career_analyzer.analyze(profile_data),
            "ats_optimization": self.ats_optimizer.analyze(profile_data),
            "seniority_estimation": self.seniority_estimator.analyze(profile_data),
        }

        results = await asyncio.gather(*tasks.values(), return_exceptions=True)
        named_results: dict = {}
        for key, result in zip(tasks.keys(), results):
            if isinstance(result, Exception):
                log.error("orchestrator.phase1.error", analysis=key, error=str(result))
                errors[key] = str(result)
                named_results[key] = None
            else:
                named_results[key] = result

        # Phase 2: Recommendations use phase 1 context
        analysis_context: dict = {}
        for key, result in named_results.items():
            if result is not None and hasattr(result, "raw") and result.raw:
                analysis_context[key] = result.raw
            elif result is not None:
                analysis_context[key] = result.model_dump(exclude={"raw"})

        try:
            recs = await self.recommendation_engine.analyze(profile_data, analysis_context)
        except Exception as e:
            log.error("orchestrator.recommendations.error", error=str(e))
            errors["recommendations"] = str(e)
            recs = None

        # Derive overall score from profile scoring
        overall_score: int | None = None
        if named_results.get("profile_scoring") is not None:
            overall_score = named_results["profile_scoring"].overall_score

        result = FullAnalysisResult(
            profile_scoring=named_results.get("profile_scoring"),
            skills_intelligence=named_results.get("skills_intelligence"),
            career_analysis=named_results.get("career_analysis"),
            ats_optimization=named_results.get("ats_optimization"),
            seniority_estimation=named_results.get("seniority_estimation"),
            recommendations=recs,
            overall_score=overall_score,
            errors=errors,
        )

        log.info(
            "orchestrator.done",
            overall_score=overall_score,
            errors=list(errors.keys()),
        )
        return result
