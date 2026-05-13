import json
import asyncio
import structlog
from typing import Any, Optional, Type
from pydantic import BaseModel
from openai import AsyncOpenAI, RateLimitError
from .base import BaseLLMProvider, LLMResponse

log = structlog.get_logger()

# Retry settings for rate-limit errors (Groq free tier: 12k TPM)
_MAX_RETRIES = 5
_RETRY_BASE_DELAY = 5.0  # seconds


async def _with_retry(coro_fn, *args, **kwargs):
    """Call an async function with exponential backoff on RateLimitError."""
    delay = _RETRY_BASE_DELAY
    for attempt in range(_MAX_RETRIES):
        try:
            return await coro_fn(*args, **kwargs)
        except RateLimitError as e:
            if attempt == _MAX_RETRIES - 1:
                raise
            log.warning("openai_provider.rate_limit", attempt=attempt + 1, delay=delay)
            await asyncio.sleep(delay)
            delay *= 2  # exponential backoff


class OpenAIProvider(BaseLLMProvider):
    def __init__(self, api_key: str, model: str = "gpt-4o", base_url: Optional[str] = None):
        self.client = AsyncOpenAI(api_key=api_key, base_url=base_url)
        self.model = model

    async def generate(
        self,
        system_prompt: str,
        user_prompt: str,
        temperature: float = 0.3,
        max_tokens: int = 4096,
    ) -> LLMResponse:
        response = await _with_retry(
            self.client.chat.completions.create,
            model=self.model,
            messages=[
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_prompt},
            ],
            temperature=temperature,
            max_tokens=max_tokens,
        )
        content = response.choices[0].message.content or ""
        return LLMResponse(
            content=content,
            model=self.model,
            usage={
                "prompt_tokens": response.usage.prompt_tokens if response.usage else 0,
                "completion_tokens": response.usage.completion_tokens if response.usage else 0,
                "total_tokens": response.usage.total_tokens if response.usage else 0,
            },
        )

    async def generate_structured(
        self,
        system_prompt: str,
        user_prompt: str,
        response_model: Optional[Type[BaseModel]] = None,
        temperature: float = 0.3,
        max_tokens: int = 4096,
    ) -> Any:
        enhanced_system = system_prompt
        if response_model is not None:
            schema = response_model.model_json_schema()
            enhanced_system = (
                f"{system_prompt}\n\n"
                f"You MUST respond with valid JSON matching this exact schema:\n"
                f"{json.dumps(schema, indent=2)}\n"
                f"Return ONLY the JSON object, no markdown, no explanation."
            )

        # json_object response_format is only supported by OpenAI — skip for other providers
        kwargs: dict = dict(
            model=self.model,
            messages=[
                {"role": "system", "content": enhanced_system},
                {"role": "user", "content": user_prompt},
            ],
            temperature=temperature,
            max_tokens=max_tokens,
        )
        if self.client.base_url is None or "openai.com" in str(self.client.base_url):
            kwargs["response_format"] = {"type": "json_object"}

        response = await _with_retry(self.client.chat.completions.create, **kwargs)

        content = response.choices[0].message.content or "{}"
        # Strip markdown code fences if present (some models wrap output)
        content = content.strip()
        if content.startswith("```json"):
            content = content[7:]
        if content.startswith("```"):
            content = content[3:]
        if content.endswith("```"):
            content = content[:-3]
        content = content.strip()

        if response_model is not None:
            return response_model.model_validate_json(content)
        return json.loads(content)
