from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    # App
    APP_NAME: str = "linkedin-ai-service"
    APP_ENV: str = "development"
    APP_PORT: int = 8000
    LOG_LEVEL: str = "INFO"

    # LLM Provider — "openai" | "anthropic" | "groq"
    LLM_PROVIDER: str = "openai"
    OPENAI_API_KEY: str = ""
    OPENAI_MODEL: str = "gpt-4o"
    ANTHROPIC_API_KEY: str = ""
    ANTHROPIC_MODEL: str = "claude-sonnet-4-6"
    GROQ_API_KEY: str = ""
    GROQ_MODEL: str = "llama-3.3-70b-versatile"
    LLM_MAX_TOKENS: int = 4096
    LLM_TEMPERATURE: float = 0.3

    # RabbitMQ
    RABBITMQ_URL: str = "amqp://linkedin:LinkedIn@2024@localhost:5672/linkedin"
    RABBITMQ_EXCHANGE: str = "linkedin.analysis"
    RABBITMQ_QUEUE_REQUESTS: str = "linkedin.analysis.requests"
    RABBITMQ_QUEUE_RESULTS: str = "linkedin.analysis.results"
    RABBITMQ_QUEUE_RESUME_REQUESTS: str = "linkedin.resume.requests"
    RABBITMQ_QUEUE_RESUME_RESULTS: str = "linkedin.resume.results"

    # Redis
    REDIS_URL: str = "redis://localhost:6379/1"

    # spaCy model
    SPACY_MODEL: str = "en_core_web_sm"

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")


settings = Settings()
