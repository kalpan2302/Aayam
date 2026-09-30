from fastapi import APIRouter, Depends, Query, HTTPException, status
from typing import Optional
from decimal import Decimal
from uuid import UUID

from app.schemas.product import ProductListResponse, ProductDetail, ProductCreate, ProductUpdate
from app.api.deps import verify_admin_key

router = APIRouter()

@router.get("/", response_model=ProductListResponse)
async def list_products(
    page: int = Query(1, ge=1),
    page_size: int = Query(20, ge=1, le=100),
    category: Optional[str] = None,
    brand: Optional[str] = None,
    min_price: Optional[Decimal] = None,
    max_price: Optional[Decimal] = None,
    finish: Optional[str] = None,
    door_material: Optional[str] = None,
    door_thickness_mm: Optional[float] = None,
    sort: Optional[str] = None,
    search: Optional[str] = None
):
    return {"items": [], "meta": {"total": 0, "page": page, "page_size": page_size, "total_pages": 0}}

@router.get("/{product_id}", response_model=ProductDetail)
async def get_product(product_id: UUID):
    raise HTTPException(status_code=404, detail="Product not found")

@router.get("/sku/{sku}", response_model=ProductDetail)
async def get_product_by_sku(sku: str):
    raise HTTPException(status_code=404, detail="Product not found")

@router.post("/", response_model=ProductDetail, dependencies=[Depends(verify_admin_key)])
async def create_product(product: ProductCreate):
    raise HTTPException(status_code=501, detail="Not implemented")

@router.put("/{product_id}", response_model=ProductDetail, dependencies=[Depends(verify_admin_key)])
async def update_product(product_id: UUID, product: ProductUpdate):
    raise HTTPException(status_code=501, detail="Not implemented")

@router.delete("/{product_id}", status_code=status.HTTP_204_NO_CONTENT, dependencies=[Depends(verify_admin_key)])
async def delete_product(product_id: UUID):
    return None
