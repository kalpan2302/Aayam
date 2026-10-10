from functools import lru_cache
from pydantic_settings import BaseSettings, SettingsConfigDict
from pydantic import Field

class Settings(BaseSettings):
    DATABASE_URL: str = "postgresql+asyncpg://aayam:aayam_dev_123@localhost:5432/aayam"
    REDIS_URL: str = "redis://localhost:6379/0"
    AWS_S3_BUCKET: str = "aayam-assets-dev"
    AWS_REGION: str = "ap-south-1"
    CLOUDFRONT_DOMAIN: str = ""
    # Dev-only local asset serving - no cloud account needed. Served at /static,
    # swap for AWS_S3_BUCKET/CLOUDFRONT_DOMAIN later without changing callers,
    # since model_url/thumbnail_url are stored as paths, not full domains.
    LOCAL_STORAGE_DIR: str = "storage"
    API_KEY: str = "dev-api-key-change-me"
    ADMIN_API_KEY: str = "dev-admin-key-change-me"
    ENVIRONMENT: str = "development"
    DEBUG: bool = True
    APP_NAME: str = 'Aayam Product API'
    APP_VERSION: str = '0.1.0'
    CORS_ORIGINS: list[str] = ['*']
    PAGE_SIZE_DEFAULT: int = 20
    PAGE_SIZE_MAX: int = 100

    model_config = SettingsConfigDict(env_file='.env', env_file_encoding='utf-8')

    @property
    def DATABASE_URL_SYNC(self) -> str:
        return self.DATABASE_URL.replace("postgresql+asyncpg", "postgresql+psycopg2")

@lru_cache
def get_settings() -> Settings:
    return Settings()
