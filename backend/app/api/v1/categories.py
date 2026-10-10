from fastapi import APIRouter, Depends
from sqlalchemy import select, text
from sqlalchemy.ext.asyncio import AsyncSession

from app.database import get_db
from app.models.product import Category
from app.schemas.category import CategoryResponse, CategoryListResponse
from app.schemas.product import ProductListItem

router = APIRouter()

@router.get("/", response_model=CategoryListResponse)
async def list_categories(db: AsyncSession = Depends(get_db)):
    result = await db.execute(select(Category).order_by(Category.display_order, Category.name))
    categories = result.scalars().all()
    return {"items": [CategoryResponse.model_validate(c) for c in categories]}

@router.get("/{slug}/products", response_model=list[ProductListItem])
async def list_category_products(slug: str, db: AsyncSession = Depends(get_db)):
    result = await db.execute(
        text("SELECT * FROM v_product_list WHERE category_slug = :slug ORDER BY created_at DESC"),
        {"slug": slug},
    )
    return [ProductListItem(**dict(row)) for row in result.mappings().all()]
