import pytest
from httpx import AsyncClient

from app.storage import get_storage_dir

ADMIN_HEADERS = {"x-api-key": "dev-admin-key-change-me"}


@pytest.mark.asyncio
async def test_upload_model_requires_admin_key(client: AsyncClient):
    response = await client.post(
        "/api/v1/assets/upload",
        params={"asset_type": "model"},
        files={"file": ("test.glb", b"glb-bytes", "application/octet-stream")},
    )
    assert response.status_code in (401, 403, 422)


@pytest.mark.asyncio
async def test_upload_model_writes_file_and_returns_working_url(client: AsyncClient):
    response = await client.post(
        "/api/v1/assets/upload",
        params={"asset_type": "model", "category": "test_category"},
        headers=ADMIN_HEADERS,
        files={"file": ("UPLOADTEST_001.glb", b"glb-bytes-for-test", "application/octet-stream")},
    )
    assert response.status_code == 200
    data = response.json()
    assert data["url"] == "/static/models/test_category/UPLOADTEST_001.glb"
    assert data["size_bytes"] == len(b"glb-bytes-for-test")

    on_disk = get_storage_dir() / "models" / "test_category" / "UPLOADTEST_001.glb"
    assert on_disk.read_bytes() == b"glb-bytes-for-test"

    static_response = await client.get(data["url"])
    assert static_response.status_code == 200
    assert static_response.content == b"glb-bytes-for-test"

    on_disk.unlink()
    on_disk.parent.rmdir()


@pytest.mark.asyncio
async def test_upload_rejects_wrong_extension_for_asset_type(client: AsyncClient):
    response = await client.post(
        "/api/v1/assets/upload",
        params={"asset_type": "thumbnail"},
        headers=ADMIN_HEADERS,
        files={"file": ("not_an_image.glb", b"data", "application/octet-stream")},
    )
    assert response.status_code == 400


@pytest.mark.asyncio
async def test_upload_rejects_path_traversal_filename(client: AsyncClient):
    response = await client.post(
        "/api/v1/assets/upload",
        params={"asset_type": "model"},
        headers=ADMIN_HEADERS,
        files={"file": ("../../etc/passwd.glb", b"data", "application/octet-stream")},
    )
    # Path(...).name strips the directory components, leaving "passwd.glb" -
    # a boring, valid filename. Confirm it lands inside storage, not outside it.
    assert response.status_code == 200
    data = response.json()
    assert data["url"] == "/static/models/passwd.glb"
    on_disk = get_storage_dir() / "models" / "passwd.glb"
    assert on_disk.exists()
    on_disk.unlink()


@pytest.mark.asyncio
async def test_upload_rejects_unsafe_category(client: AsyncClient):
    response = await client.post(
        "/api/v1/assets/upload",
        params={"asset_type": "model", "category": "../../escape"},
        headers=ADMIN_HEADERS,
        files={"file": ("ok.glb", b"data", "application/octet-stream")},
    )
    assert response.status_code == 400


@pytest.mark.asyncio
async def test_generate_qr_creates_working_png(client: AsyncClient):
    response = await client.get("/api/v1/assets/HANDLE_001/qr")
    assert response.status_code == 200
    data = response.json()
    assert data["qr_url"] == "/static/qrcodes/HANDLE_001.png"
    assert data["deep_link"] == "aayam://product/HANDLE_001"

    on_disk = get_storage_dir() / "qrcodes" / "HANDLE_001.png"
    assert on_disk.exists()
    assert on_disk.read_bytes()[:8] == b"\x89PNG\r\n\x1a\n"  # PNG magic bytes

    png_response = await client.get(data["qr_url"])
    assert png_response.status_code == 200
    assert png_response.content == on_disk.read_bytes()

    on_disk.unlink()


@pytest.mark.asyncio
async def test_generate_qr_rejects_unsafe_product_id(client: AsyncClient):
    # A single path segment that starts with "." - fails _safe_segment, and
    # can't be used to escape the qrcodes/ directory either way.
    response = await client.get("/api/v1/assets/.hidden/qr")
    assert response.status_code == 400
