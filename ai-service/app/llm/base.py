from abc import ABC, abstractmethod
from typing import Any, Optional, Type
from pydantic import BaseModel


class LLMResponse(BaseModel):
    content: str
    model: str
    usage: dict[str, int]


class BaseLLMProvider(ABC):
    @abstractmethod
    async def generate(
        self,
        system_prompt: str,
        user_prompt: str,
        temperature: float = 0.3,
        max_tokens: int = 4096,
    ) -> LLMResponse:
        """Send a prompt to the LLM and get a raw text response."""
        ...

    @abstractmethod
    async def generate_structured(
        self,
        system_prompt: str,
        user_prompt: str,
        response_model: Optional[Type[BaseModel]] = None,
        temperature: float = 0.3,
        max_tokens: int = 4096,
    ) -> Any:
        """Send a prompt and return either a Pydantic model instance (if
        response_model is given) or a plain dict parsed from the JSON response."""
        ...
