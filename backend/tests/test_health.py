import pytest
from httpx import AsyncClient


@pytest.mark.asyncio
async def test_health_returns_200(client: AsyncClient):
    """Test that health check endpoint returns HTTP 200."""
    response = await client.get("/health")
    assert response.status_code == 200


@pytest.mark.asyncio
async def test_health_contains_version(client: AsyncClient):
    """Test that health check endpoint payload contains version field."""
    response = await client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert "version" in data
    assert data["version"] is not None


@pytest.mark.asyncio
async def test_health_contains_environment(client: AsyncClient):
    """Test that health check endpoint payload contains environment field."""
    response = await client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert "environment" in data
    assert data["environment"] in ["development", "staging", "production", "test"]
