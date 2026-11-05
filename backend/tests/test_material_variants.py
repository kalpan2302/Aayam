from uuid import uuid4

import pytest
from httpx import AsyncClient

ADMIN_HEADERS = {"x-api-key": "dev-admin-key-change-me"}


async def _create_test_product(client: AsyncClient) -> str:
    brands = (await client.get("/api/v1/brands/")).json()["items"]
    categories = (await client.get("/api/v1/categories/")).json()["items"]
    sku = f"VARIANT_TEST_{uuid4().hex[:8].upper()}"
    response = await client.post(
        "/api/v1/products/",
        headers=ADMIN_HEADERS,
        json={
            "sku": sku,
            "name": "Material Variant Test Product",
            "brand_id": brands[0]["id"],
            "category_id": categories[0]["id"],
            "price": 100,
        },
    )
    assert response.status_code == 201
    return response.json()["id"]


@pytest.mark.asyncio
async def test_create_and_list_material_variants(client: AsyncClient):
    product_id = await _create_test_product(client)

    create_response = await client.post(
        f"/api/v1/products/{product_id}/material-variants",
        headers=ADMIN_HEADERS,
        json={
            "finish_name": "matte_black",
            "base_color_hex": "#1A1A1A",
            "metallic_factor": 0.6,
            "roughness_factor": 0.75,
            "is_default": True,
        },
    )
    assert create_response.status_code == 201
    created = create_response.json()
    assert created["finish_name"] == "matte_black"
    assert created["product_id"] == product_id

    list_response = await client.get(f"/api/v1/products/{product_id}/material-variants")
    assert list_response.status_code == 200
    variants = list_response.json()
    assert len(variants) == 1
    assert variants[0]["finish_name"] == "matte_black"


@pytest.mark.asyncio
async def test_duplicate_finish_name_rejected(client: AsyncClient):
    product_id = await _create_test_product(client)
    body = {
        "finish_name": "chrome",
        "base_color_hex": "#B8BAC0",
        "metallic_factor": 1.0,
        "roughness_factor": 0.08,
    }

    first = await client.post(
        f"/api/v1/products/{product_id}/material-variants", headers=ADMIN_HEADERS, json=body
    )
    assert first.status_code == 201

    second = await client.post(
        f"/api/v1/products/{product_id}/material-variants", headers=ADMIN_HEADERS, json=body
    )
    assert second.status_code == 409


@pytest.mark.asyncio
async def test_create_material_variant_requires_admin_key(client: AsyncClient):
    product_id = await _create_test_product(client)
    response = await client.post(
        f"/api/v1/products/{product_id}/material-variants",
        json={"finish_name": "silver", "base_color_hex": "#C0C0C0", "metallic_factor": 0.95, "roughness_factor": 0.2},
    )
    assert response.status_code in (401, 403, 422)


@pytest.mark.asyncio
async def test_product_detail_carries_material_variants(client: AsyncClient):
    product_id = await _create_test_product(client)
    await client.post(
        f"/api/v1/products/{product_id}/material-variants",
        headers=ADMIN_HEADERS,
        json={
            "finish_name": "brushed_nickel",
            "base_color_hex": "#9E9FA3",
            "metallic_factor": 0.9,
            "roughness_factor": 0.45,
            "is_default": True,
        },
    )

    detail_response = await client.get(f"/api/v1/products/{product_id}")
    assert detail_response.status_code == 200
    detail = detail_response.json()
    assert len(detail["material_variants"]) == 1
    assert detail["material_variants"][0]["finish_name"] == "brushed_nickel"


@pytest.mark.asyncio
async def test_no_default_variant_when_none_created(client: AsyncClient):
    product_id = await _create_test_product(client)

    list_response = await client.get(f"/api/v1/products/{product_id}/material-variants")
    assert list_response.status_code == 200
    assert list_response.json() == []
