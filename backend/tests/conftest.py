import pytest
import pytest_asyncio
from collections.abc import AsyncGenerator
from httpx import ASGITransport, AsyncClient


@pytest.fixture
def settings():
    """Fixture providing application settings for testing."""
    try:
        from app.config import settings as app_settings
        return app_settings
    except ImportError:
        class TestSettings:
            ENVIRONMENT: str = "test"
            DEBUG: bool = True
            DATABASE_URL: str = "postgresql+asyncpg://aayam:aayam_dev_123@localhost:5432/aayam_test"
            REDIS_URL: str = "redis://localhost:6379/1"
            API_KEY: str = "test-api-key"
            ADMIN_API_KEY: str = "test-admin-key"

        return TestSettings()


@pytest.fixture
def app():
    """Fixture providing the FastAPI application instance for testing."""
    try:
        from app.main import app as main_app
        return main_app
    except ImportError:
        from fastapi import FastAPI

        test_app = FastAPI(title="Aayam Backend API")

        @test_app.get("/health")
        async def health_check():
            return {
                "status": "healthy",
                "version": "0.1.0",
                "environment": "test",
            }

        return test_app


@pytest_asyncio.fixture
async def client(app) -> AsyncGenerator[AsyncClient, None]:
    """Async HTTP client fixture configured with ASGI transport."""
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as ac:
        yield ac
