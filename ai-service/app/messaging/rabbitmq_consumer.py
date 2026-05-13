import asyncio
import json
import structlog
from aio_pika import connect_robust, IncomingMessage, ExchangeType
from app.config import Settings
from app.schemas.messages import AnalysisRequestMessage, ResumeParseRequestMessage
from app.schemas.messages import AnalysisResultMessage, ResumeParseResultMessage
from app.services.analysis_orchestrator import AnalysisOrchestrator
from app.services.resume_parser import ResumeParser
from app.messaging.rabbitmq_publisher import RabbitMqPublisher

log = structlog.get_logger()

EXCHANGE_NAME = "linkedin.analysis"
ANALYSIS_QUEUE = "linkedin.analysis.requests"
RESUME_QUEUE = "linkedin.resume.parse.requests"


class RabbitMqConsumer:
    def __init__(
        self,
        settings: Settings,
        orchestrator: AnalysisOrchestrator,
        resume_parser: ResumeParser,
        publisher: RabbitMqPublisher,
    ):
        self.settings = settings
        self.orchestrator = orchestrator
        self.resume_parser = resume_parser
        self.publisher = publisher

    async def start(self) -> None:
        connection = await connect_robust(self.settings.RABBITMQ_URL)
        channel = await connection.channel()
        await channel.set_qos(prefetch_count=1)

        exchange = await channel.declare_exchange(
            EXCHANGE_NAME, ExchangeType.TOPIC, durable=True
        )

        # Analysis queue
        analysis_queue = await channel.declare_queue(ANALYSIS_QUEUE, durable=True)
        await analysis_queue.bind(exchange, routing_key="analysis.request")

        # Resume queue
        resume_queue = await channel.declare_queue(RESUME_QUEUE, durable=True)
        await resume_queue.bind(exchange, routing_key="resume.parse.request")

        await analysis_queue.consume(self._handle_analysis)
        await resume_queue.consume(self._handle_resume_parse)

        log.info("rabbitmq_consumer.started")

    async def _handle_analysis(self, message: IncomingMessage) -> None:
        async with message.process(requeue=True):
            try:
                body = json.loads(message.body.decode())
                request = AnalysisRequestMessage(**body)
                log.info(
                    "consumer.analysis.received",
                    message_id=request.message_id,
                    user_id=request.user_id,
                )

                profile_dict = request.profile_data.model_dump()
                analysis = await self.orchestrator.run_full_analysis(profile_dict)

                result_msg = AnalysisResultMessage(
                    message_id=request.message_id,
                    user_id=request.user_id,
                    profile_id=request.profile_id,
                    status="completed",
                    result=analysis.model_dump(exclude={"errors"}),
                    overall_score=analysis.overall_score,
                )
                await self.publisher.publish_analysis_result(result_msg)
                log.info("consumer.analysis.done", message_id=request.message_id)

            except Exception as e:
                log.error("consumer.analysis.failed", error=str(e))
                try:
                    body = json.loads(message.body.decode())
                    result_msg = AnalysisResultMessage(
                        message_id=body.get("message_id", "unknown"),
                        user_id=body.get("user_id", 0),
                        profile_id=body.get("profile_id", 0),
                        status="failed",
                        error_message=str(e),
                    )
                    await self.publisher.publish_analysis_result(result_msg)
                except Exception:
                    pass

    async def _handle_resume_parse(self, message: IncomingMessage) -> None:
        async with message.process(requeue=True):
            try:
                body = json.loads(message.body.decode())
                request = ResumeParseRequestMessage(**body)
                log.info(
                    "consumer.resume.received",
                    message_id=request.message_id,
                    user_id=request.user_id,
                )

                parsed = self.resume_parser.parse(
                    request.file_content_base64, request.file_type
                )

                result_msg = ResumeParseResultMessage(
                    message_id=request.message_id,
                    user_id=request.user_id,
                    resume_id=request.resume_id,
                    status="completed",
                    parsed_data=parsed.model_dump(),
                )
                await self.publisher.publish_resume_result(result_msg)
                log.info("consumer.resume.done", message_id=request.message_id)

            except Exception as e:
                log.error("consumer.resume.failed", error=str(e))
                try:
                    body = json.loads(message.body.decode())
                    result_msg = ResumeParseResultMessage(
                        message_id=body.get("message_id", "unknown"),
                        user_id=body.get("user_id", 0),
                        resume_id=body.get("resume_id", 0),
                        status="failed",
                        error_message=str(e),
                    )
                    await self.publisher.publish_resume_result(result_msg)
                except Exception:
                    pass
