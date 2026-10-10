from uuid import uuid4

import pytest
from httpx import AsyncClient

ADMIN_HEADERS = {"x-api-key": "dev-admin-key-change-me"}


@pytest.mark.asyncio
async def test_list_products_returns_seeded_products(client: AsyncClient):
    response = await client.get("/api/v1/products/")
    assert response.status_code == 200
    data = response.json()
    assert data["meta"]["total"] >= 20
    assert len(data["items"]) == data["meta"]["page_size"] or len(data["items"]) == data["meta"]["total"]
    item = data["items"][0]
    assert {"id", "sku", "name", "brand_name", "category_name", "price", "has_3d_model"} <= item.keys()


@pytest.mark.asyncio
async def test_list_products_filters_by_brand_and_category(client: AsyncClient):
    response = await client.get("/api/v1/products/", params={"brand": "godrej", "category": "lever-handle"})
    assert response.status_code == 200
    data = response.json()
    assert data["meta"]["total"] >= 1
    for item in data["items"]:
        assert item["brand_name"] == "Godrej"


@pytest.mark.asyncio
async def test_list_products_pagination_bounds(client: AsyncClient):
    response = await client.get("/api/v1/products/", params={"page": 1, "page_size": 5})
    assert response.status_code == 200
    data = response.json()
    assert len(data["items"]) <= 5
    assert data["meta"]["page_size"] == 5


@pytest.mark.asyncio
async def test_get_product_by_sku_returns_full_detail(client: AsyncClient):
    response = await client.get("/api/v1/products/sku/HANDLE_001")
    assert response.status_code == 200
    data = response.json()
    assert data["sku"] == "HANDLE_001"
    assert data["brand_name"]
    assert data["dimensions"] is not None
    assert data["asset_3d"] is not None
    assert data["compatibility"] is not None
    assert isinstance(data["images"], list) and len(data["images"]) >= 1
    assert isinstance(data["materials"], list) and len(data["materials"]) >= 1


@pytest.mark.asyncio
async def test_get_product_by_sku_not_found(client: AsyncClient):
    response = await client.get("/api/v1/products/sku/DOES_NOT_EXIST")
    assert response.status_code == 404


@pytest.mark.asyncio
async def test_get_product_by_id_not_found(client: AsyncClient):
    response = await client.get("/api/v1/products/00000000-0000-0000-0000-000000000000")
    assert response.status_code == 404


@pytest.mark.asyncio
async def test_create_product_requires_admin_key(client: AsyncClient):
    response = await client.post("/api/v1/products/", json={
        "sku": "NOAUTH_001", "name": "No Auth", "brand_id": "00000000-0000-0000-0000-000000000000",
        "category_id": "00000000-0000-0000-0000-000000000000", "price": 100,
    })
    assert response.status_code in (401, 403, 422)


@pytest.mark.asyncio
async def test_create_update_soft_delete_lifecycle(client: AsyncClient):
    brands = (await client.get("/api/v1/brands/")).json()["items"]
    categories = (await client.get("/api/v1/categories/")).json()["items"]
    brand_id = brands[0]["id"]
    category_id = categories[0]["id"]

    sku = f"LIFECYCLE_{uuid4().hex[:8].upper()}"
    create_response = await client.post(
        "/api/v1/products/",
        headers=ADMIN_HEADERS,
        json={
            "sku": sku,
            "name": "Lifecycle Test Product",
            "brand_id": brand_id,
            "category_id": category_id,
            "price": 100,
        },
    )
    assert create_response.status_code == 201
    created = create_response.json()
    assert created["sku"] == sku
    assert created["price"] == "100.00"
    product_id = created["id"]

    update_response = await client.put(
        f"/api/v1/products/{product_id}", headers=ADMIN_HEADERS, json={"price": 150}
    )
    assert update_response.status_code == 200
    assert update_response.json()["price"] == "150.00"

    delete_response = await client.delete(f"/api/v1/products/{product_id}", headers=ADMIN_HEADERS)
    assert delete_response.status_code == 204

    get_after_delete = await client.get(f"/api/v1/products/{product_id}")
    assert get_after_delete.status_code == 404

    second_delete = await client.delete(f"/api/v1/products/{product_id}", headers=ADMIN_HEADERS)
    assert second_delete.status_code == 404


@pytest.mark.asyncio
async def test_update_nonexistent_product_returns_404(client: AsyncClient):
    response = await client.put(
        "/api/v1/products/00000000-0000-0000-0000-000000000000",
        headers=ADMIN_HEADERS,
        json={"price": 50},
    )
    assert response.status_code == 404


@pytest.mark.asyncio
async def test_list_brands_and_categories(client: AsyncClient):
    brands = await client.get("/api/v1/brands/")
    assert brands.status_code == 200
    assert len(brands.json()["items"]) >= 1

    categories = await client.get("/api/v1/categories/")
    assert categories.status_code == 200
    assert len(categories.json()["items"]) >= 1


@pytest.mark.asyncio
async def test_brand_products_filters_correctly(client: AsyncClient):
    response = await client.get("/api/v1/brands/godrej/products")
    assert response.status_code == 200
    items = response.json()
    assert len(items) >= 1
    for item in items:
        assert item["brand_name"] == "Godrej"
