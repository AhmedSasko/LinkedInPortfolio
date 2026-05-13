from fastapi import APIRouter, Depends, HTTPException
from app.schemas.profile import ProfileData
from app.schemas.analysis import FullAnalysisResult
from app.services.analysis_orchestrator import AnalysisOrchestrator
import structlog

log = structlog.get_logger()
router = APIRouter(prefix="/analyze", tags=["Analysis"])


def get_orchestrator() -> AnalysisOrchestrator:
    # Injected via app state at startup
    from app.main import orchestrator as _orchestrator
    return _orchestrator


@router.post("/profile", response_model=FullAnalysisResult)
async def analyze_profile(
    profile: ProfileData,
    orch: AnalysisOrchestrator = Depends(get_orchestrator),
) -> FullAnalysisResult:
    """Run full AI analysis on a LinkedIn profile. Returns all 6 analysis types."""
    try:
        result = await orch.run_full_analysis(profile.model_dump())
        return result
    except Exception as e:
        log.error("api.analyze_profile.error", error=str(e))
        raise HTTPException(status_code=500, detail=f"Analysis failed: {e}")
