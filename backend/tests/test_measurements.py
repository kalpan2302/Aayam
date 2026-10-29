import uuid

import pytest
from httpx import AsyncClient

API_HEADERS = {"x-api-key": "dev-api-key-change-me"}


@pytest.mark.asyncio
async def test_register_device_creates_new_device(client: AsyncClient):
    device_uuid = str(uuid.uuid4())
    response = await client.post(
        "/api/v1/devices/register", json={"device_uuid": device_uuid}, headers=API_HEADERS
    )
    assert response.status_code == 200
    data = response.json()
    assert data["device_uuid"] == device_uuid
    assert "id" in data


@pytest.mark.asyncio
async def test_register_device_is_idempotent(client: AsyncClient):
    device_uuid = str(uuid.uuid4())
    first = await client.post(
        "/api/v1/devices/register", json={"device_uuid": device_uuid}, headers=API_HEADERS
    )
    second = await client.post(
        "/api/v1/devices/register", json={"device_uuid": device_uuid}, headers=API_HEADERS
    )
    assert first.json()["id"] == second.json()["id"]


@pytest.mark.asyncio
async def test_register_device_requires_api_key(client: AsyncClient):
    response = await client.post("/api/v1/devices/register", json={"device_uuid": str(uuid.uuid4())})
    assert response.status_code == 422  # missing required header


@pytest.mark.asyncio
async def test_register_device_rejects_wrong_api_key(client: AsyncClient):
    response = await client.post(
        "/api/v1/devices/register",
        json={"device_uuid": str(uuid.uuid4())},
        headers={"x-api-key": "wrong-key"},
    )
    assert response.status_code == 401


async def _register_device(client: AsyncClient) -> str:
    response = await client.post(
        "/api/v1/devices/register", json={"device_uuid": str(uuid.uuid4())}, headers=API_HEADERS
    )
    return response.json()["id"]


@pytest.mark.asyncio
async def test_create_and_list_doors_for_device(client: AsyncClient):
    device_id = await _register_device(client)

    create_response = await client.post(
        "/api/v1/doors/",
        params={"device_id": device_id},
        json={"room_label": "Front Entrance", "project_label": "Home Renovation"},
        headers=API_HEADERS,
    )
    assert create_response.status_code == 201
    door = create_response.json()
    assert door["room_label"] == "Front Entrance"
    assert door["device_id"] == device_id

    list_response = await client.get("/api/v1/doors/", params={"device_id": device_id}, headers=API_HEADERS)
    assert list_response.status_code == 200
    doors = list_response.json()
    assert len(doors) == 1
    assert doors[0]["id"] == door["id"]


@pytest.mark.asyncio
async def test_save_and_list_measurement_history(client: AsyncClient):
    device_id = await _register_device(client)
    door_response = await client.post(
        "/api/v1/doors/", params={"device_id": device_id}, json={}, headers=API_HEADERS
    )
    door_id = door_response.json()["id"]

    measurement_body = {
        "width_mm": 749.3,
        "height_mm": 2159.0,
        "width_confidence": 0.9,
        "height_confidence": 0.9,
        "source": "user_assisted",
    }
    save_response = await client.post(
        f"/api/v1/doors/{door_id}/measurements", json=measurement_body, headers=API_HEADERS
    )
    assert save_response.status_code == 201
    saved = save_response.json()
    assert float(saved["width_mm"]) == pytest.approx(749.3)
    assert saved["source"] == "user_assisted"

    # A second measurement for the same door - history, not an overwrite.
    second_body = dict(measurement_body, width_mm=750.0, source="lidar")
    await client.post(f"/api/v1/doors/{door_id}/measurements", json=second_body, headers=API_HEADERS)

    history_response = await client.get(f"/api/v1/doors/{door_id}/measurements", headers=API_HEADERS)
    assert history_response.status_code == 200
    history = history_response.json()
    assert len(history) == 2


@pytest.mark.asyncio
async def test_save_measurement_rejects_invalid_source(client: AsyncClient):
    device_id = await _register_device(client)
    door_response = await client.post(
        "/api/v1/doors/", params={"device_id": device_id}, json={}, headers=API_HEADERS
    )
    door_id = door_response.json()["id"]

    response = await client.post(
        f"/api/v1/doors/{door_id}/measurements",
        json={"source": "guess"},
        headers=API_HEADERS,
    )
    assert response.status_code == 422


@pytest.mark.asyncio
async def test_save_measurement_for_missing_door_404s(client: AsyncClient):
    missing_door_id = str(uuid.uuid4())
    response = await client.post(
        f"/api/v1/doors/{missing_door_id}/measurements",
        json={"source": "manual_input"},
        headers=API_HEADERS,
    )
    assert response.status_code == 404
