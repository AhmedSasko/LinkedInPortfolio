import json
import structlog
from aio_pika import connect_robust, Message, DeliveryMode, ExchangeType
from app.config import Settings
from app.schemas.messages import AnalysisResultMessage, ResumeParseResultMessage

log = structlog.get_logger()

EXCHANGE_NAME = "linkedin.analysis"


class RabbitMqPublisher:
    def __init__(self, settings: Settings):
        self.settings = settings
        self._connection = None
        self._channel = None
        self._exchange = None

    async def connect(self) -> None:
        self._connection = await connect_robust(self.settings.RABBITMQ_URL)
        self._channel = await self._connection.channel()
        self._exchange = await self._channel.declare_exchange(
            EXCHANGE_NAME, ExchangeType.TOPIC, durable=True
        )
        log.info("rabbitmq_publisher.connected")

    async def _publish(self, routing_key: str, payload: dict) -> None:
        if self._exchange is None:
            await self.connect()
        body = json.dumps(payload).encode()
        message = Message(
            body=body,
            delivery_mode=DeliveryMode.PERSISTENT,
            content_type="application/json",
        )
        await self._exchange.publish(message, routing_key=routing_key)

    async def publish_analysis_result(self, result: AnalysisResultMessage) -> None:
        await self._publish("analysis.result.completed", result.model_dump())
        log.info("publisher.analysis_result.sent", message_id=result.message_id)

    async def publish_resume_result(self, result: ResumeParseResultMessage) -> None:
        await self._publish("resume.parse.result", result.model_dump())
        log.info("publisher.resume_result.sent", message_id=result.message_id)

    async def close(self) -> None:
        if self._connection:
            await self._connection.close()
