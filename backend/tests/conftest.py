import pytest
import pytest_asyncio
from collections.abc import AsyncGenerator
from httpx import ASGITransport, AsyncClient


@pytest.fixture
def settings():
    """Fixture providing application settings for testing."""
    try:
        from app.config import get_settings
        return get_settings()
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
async def client(app, settings) -> AsyncGenerator[AsyncClient, None]:
    """Async HTTP client fixture configured with ASGI transport.

    Overrides the app's `get_db` dependency with an engine created inside this
    fixture (i.e. inside the test's own event loop) using NullPool, so pooled
    asyncpg connections never cross an event-loop boundary - the module-level
    `app.database.engine` is created once at import time and is bound to
    whichever loop exists then, which breaks under pytest-asyncio's per-test
    loops ("cannot perform operation: another operation is in progress").
    """
    from sqlalchemy.ext.asyncio import create_async_engine, async_sessionmaker, AsyncSession
    from sqlalchemy.pool import NullPool
    from app.database import get_db

    test_engine = create_async_engine(settings.DATABASE_URL, poolclass=NullPool)
    test_session_maker = async_sessionmaker(test_engine, class_=AsyncSession, expire_on_commit=False)

    async def override_get_db() -> AsyncGenerator[AsyncSession, None]:
        async with test_session_maker() as session:
            yield session

    app.dependency_overrides[get_db] = override_get_db
    transport = ASGITransport(app=app)
    try:
        async with AsyncClient(transport=transport, base_url="http://test") as ac:
            yield ac
    finally:
        app.dependency_overrides.pop(get_db, None)
        await test_engine.dispose()
