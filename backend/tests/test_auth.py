from uuid import uuid4

import pytest
from httpx import AsyncClient


def _unique_email() -> str:
    # fitora.app, not .test/.example - email-validator rejects RFC 2606
    # reserved TLDs (.test/.example/.invalid/.localhost) at the format
    # level, confirmed live against the real endpoint this session.
    return f"authtest_{uuid4().hex[:10]}@fitora.app"


@pytest.mark.asyncio
async def test_signup_returns_token_and_user(client: AsyncClient):
    email = _unique_email()
    response = await client.post(
        "/api/v1/auth/signup",
        json={"email": email, "password": "testpass123", "name": "Test User"},
    )
    assert response.status_code == 201
    body = response.json()
    assert body["token_type"] == "bearer"
    assert body["access_token"]
    assert body["user"]["email"] == email
    assert body["user"]["name"] == "Test User"


@pytest.mark.asyncio
async def test_signup_duplicate_email_rejected(client: AsyncClient):
    email = _unique_email()
    body = {"email": email, "password": "testpass123", "name": "First"}
    first = await client.post("/api/v1/auth/signup", json=body)
    assert first.status_code == 201

    second = await client.post("/api/v1/auth/signup", json={**body, "name": "Second"})
    assert second.status_code == 409


@pytest.mark.asyncio
async def test_signup_short_password_rejected(client: AsyncClient):
    response = await client.post(
        "/api/v1/auth/signup",
        json={"email": _unique_email(), "password": "short", "name": "Test"},
    )
    assert response.status_code == 422


@pytest.mark.asyncio
async def test_login_with_correct_password(client: AsyncClient):
    email = _unique_email()
    await client.post(
        "/api/v1/auth/signup",
        json={"email": email, "password": "testpass123", "name": "Login Test"},
    )
    response = await client.post("/api/v1/auth/login", json={"email": email, "password": "testpass123"})
    assert response.status_code == 200
    assert response.json()["access_token"]


@pytest.mark.asyncio
async def test_login_with_wrong_password_rejected(client: AsyncClient):
    email = _unique_email()
    await client.post(
        "/api/v1/auth/signup",
        json={"email": email, "password": "testpass123", "name": "Wrong Pass"},
    )
    response = await client.post("/api/v1/auth/login", json={"email": email, "password": "wrongpassword"})
    assert response.status_code == 401


@pytest.mark.asyncio
async def test_login_with_unknown_email_rejected(client: AsyncClient):
    response = await client.post(
        "/api/v1/auth/login", json={"email": _unique_email(), "password": "whatever123"}
    )
    assert response.status_code == 401


@pytest.mark.asyncio
async def test_me_requires_token(client: AsyncClient):
    response = await client.get("/api/v1/auth/me")
    assert response.status_code == 401


@pytest.mark.asyncio
async def test_me_with_valid_token_returns_user(client: AsyncClient):
    email = _unique_email()
    signup = await client.post(
        "/api/v1/auth/signup",
        json={"email": email, "password": "testpass123", "name": "Me Test"},
    )
    token = signup.json()["access_token"]

    response = await client.get("/api/v1/auth/me", headers={"Authorization": f"Bearer {token}"})
    assert response.status_code == 200
    assert response.json()["email"] == email


@pytest.mark.asyncio
async def test_me_with_garbage_token_rejected(client: AsyncClient):
    response = await client.get("/api/v1/auth/me", headers={"Authorization": "Bearer not-a-real-token"})
    assert response.status_code == 401
