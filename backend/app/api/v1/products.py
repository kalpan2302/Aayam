from fastapi import APIRouter, Depends, Query, HTTPException, status
from typing import Optional
from decimal import Decimal
from uuid import UUID

from sqlalchemy.ext.asyncio import AsyncSession

from app.database import get_db
from app.schemas.common import PaginationMeta
from app.schemas.product import ProductListResponse, ProductListItem, ProductDetail, ProductCreate, ProductUpdate
from app.api.deps import verify_admin_key
from app.services.product_service import ProductService

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
    search: Optional[str] = None,
    db: AsyncSession = Depends(get_db),
):
    filters = {
        "category": category,
        "brand": brand,
        "min_price": min_price,
        "max_price": max_price,
        "finish": finish,
        "door_material": door_material,
        "door_thickness_mm": door_thickness_mm,
        "sort": sort,
        "search": search,
    }
    items, total = await ProductService.list_products(db, filters, page, page_size)
    total_pages = (total + page_size - 1) // page_size if page_size else 0
    return {
        "items": [ProductListItem(**item) for item in items],
        "meta": PaginationMeta(total=total, page=page, page_size=page_size, total_pages=total_pages),
    }

@router.get("/{product_id}", response_model=ProductDetail)
async def get_product(product_id: UUID, db: AsyncSession = Depends(get_db)):
    data = await ProductService.get_product(db, product_id)
    if data is None:
        raise HTTPException(status_code=404, detail="Product not found")
    return ProductDetail(**data)

@router.get("/sku/{sku}", response_model=ProductDetail)
async def get_product_by_sku(sku: str, db: AsyncSession = Depends(get_db)):
    data = await ProductService.get_product_by_sku(db, sku)
    if data is None:
        raise HTTPException(status_code=404, detail="Product not found")
    return ProductDetail(**data)

@router.post("/", response_model=ProductDetail, status_code=status.HTTP_201_CREATED, dependencies=[Depends(verify_admin_key)])
async def create_product(product: ProductCreate, db: AsyncSession = Depends(get_db)):
    data = await ProductService.create_product(db, product.model_dump())
    return ProductDetail(**data)

@router.put("/{product_id}", response_model=ProductDetail, dependencies=[Depends(verify_admin_key)])
async def update_product(product_id: UUID, product: ProductUpdate, db: AsyncSession = Depends(get_db)):
    data = await ProductService.update_product(db, product_id, product.model_dump(exclude_unset=True))
    if data is None:
        raise HTTPException(status_code=404, detail="Product not found")
    return ProductDetail(**data)

@router.delete("/{product_id}", status_code=status.HTTP_204_NO_CONTENT, dependencies=[Depends(verify_admin_key)])
async def delete_product(product_id: UUID, db: AsyncSession = Depends(get_db)):
    deleted = await ProductService.soft_delete_product(db, product_id)
    if not deleted:
        raise HTTPException(status_code=404, detail="Product not found")
    return None
