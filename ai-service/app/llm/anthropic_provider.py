import json
from typing import Any, Optional, Type
from pydantic import BaseModel
import anthropic
from .base import BaseLLMProvider, LLMResponse


class AnthropicProvider(BaseLLMProvider):
    def __init__(self, api_key: str, model: str = "claude-sonnet-4-6"):
        self.client = anthropic.AsyncAnthropic(api_key=api_key)
        self.model = model

    async def generate(
        self,
        system_prompt: str,
        user_prompt: str,
        temperature: float = 0.3,
        max_tokens: int = 4096,
    ) -> LLMResponse:
        response = await self.client.messages.create(
            model=self.model,
            system=system_prompt,
            messages=[{"role": "user", "content": user_prompt}],
            temperature=temperature,
            max_tokens=max_tokens,
        )
        content = response.content[0].text if response.content else ""
        return LLMResponse(
            content=content,
            model=self.model,
            usage={
                "prompt_tokens": response.usage.input_tokens,
                "completion_tokens": response.usage.output_tokens,
                "total_tokens": response.usage.input_tokens + response.usage.output_tokens,
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

        response = await self.client.messages.create(
            model=self.model,
            system=enhanced_system,
            messages=[{"role": "user", "content": user_prompt}],
            temperature=temperature,
            max_tokens=max_tokens,
        )

        content = response.content[0].text if response.content else "{}"

        # Strip markdown code blocks if present
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
