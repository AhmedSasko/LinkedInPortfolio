from fastapi import APIRouter
from pydantic import BaseModel

router = APIRouter()


class HealthResponse(BaseModel):
    status: str
    service: str


class ReadinessResponse(BaseModel):
    status: str
    llm_provider: str


@router.get("/health", response_model=HealthResponse, tags=["Health"])
async def health() -> HealthResponse:
    return HealthResponse(status="ok", service="ai-service")


@router.get("/readiness", response_model=ReadinessResponse, tags=["Health"])
async def readiness(llm_provider: str = "unknown") -> ReadinessResponse:
    return ReadinessResponse(status="ready", llm_provider=llm_provider)
