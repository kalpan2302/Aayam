from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import select, text
from sqlalchemy.ext.asyncio import AsyncSession

from app.database import get_db
from app.models.product import Brand
from app.schemas.brand import BrandResponse, BrandListResponse
from app.schemas.product import ProductListItem

router = APIRouter()

@router.get("/", response_model=BrandListResponse)
async def list_brands(db: AsyncSession = Depends(get_db)):
    result = await db.execute(select(Brand).where(Brand.is_active.is_(True)).order_by(Brand.name))
    brands = result.scalars().all()
    return {"items": [BrandResponse.model_validate(b) for b in brands]}

@router.get("/{slug}", response_model=BrandResponse)
async def get_brand(slug: str, db: AsyncSession = Depends(get_db)):
    result = await db.execute(select(Brand).where(Brand.slug == slug))
    brand = result.scalar_one_or_none()
    if brand is None:
        raise HTTPException(status_code=404, detail="Brand not found")
    return BrandResponse.model_validate(brand)

@router.get("/{slug}/products", response_model=list[ProductListItem])
async def list_brand_products(slug: str, db: AsyncSession = Depends(get_db)):
    result = await db.execute(
        text("SELECT * FROM v_product_list WHERE brand_slug = :slug ORDER BY created_at DESC"),
        {"slug": slug},
    )
    return [ProductListItem(**dict(row)) for row in result.mappings().all()]
