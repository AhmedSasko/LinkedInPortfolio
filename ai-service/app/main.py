import asyncio
import structlog
from contextlib import asynccontextmanager
from fastapi import FastAPI
from app.config import Settings
from app.llm.factory import create_llm_provider
from app.services.analysis_orchestrator import AnalysisOrchestrator
from app.services.resume_parser import ResumeParser
from app.messaging.rabbitmq_publisher import RabbitMqPublisher
from app.messaging.rabbitmq_consumer import RabbitMqConsumer
from app.api import health, analysis

log = structlog.get_logger()

settings = Settings()
orchestrator: AnalysisOrchestrator | None = None
_consumer_task: asyncio.Task | None = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    global orchestrator, _consumer_task

    log.info("startup.begin", llm_provider=settings.LLM_PROVIDER)

    # Build LLM + orchestrator
    llm = create_llm_provider(settings)
    orchestrator = AnalysisOrchestrator(llm)

    # Build messaging
    publisher = RabbitMqPublisher(settings)
    await publisher.connect()

    resume_parser = ResumeParser(spacy_model=settings.SPACY_MODEL)
    consumer = RabbitMqConsumer(settings, orchestrator, resume_parser, publisher)

    # Start consumer in background
    _consumer_task = asyncio.create_task(consumer.start())
    log.info("startup.done")

    yield

    # Shutdown
    if _consumer_task:
        _consumer_task.cancel()
        try:
            await _consumer_task
        except asyncio.CancelledError:
            pass
    await publisher.close()
    log.info("shutdown.done")


app = FastAPI(
    title="LinkedIn AI Service",
    version="1.0.0",
    description="AI analysis sidecar for LinkedIn profile intelligence",
    lifespan=lifespan,
)

app.include_router(health.router)
app.include_router(analysis.router)
