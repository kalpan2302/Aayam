from typing import Optional
from uuid import UUID

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.material_variant import ProductMaterialVariant


class MaterialVariantService:
    @staticmethod
    async def create_variant(db: AsyncSession, product_id: UUID, data: dict) -> ProductMaterialVariant:
        variant = ProductMaterialVariant(product_id=product_id, **data)
        db.add(variant)
        await db.commit()
        await db.refresh(variant)
        return variant

    @staticmethod
    async def list_variants_for_product(db: AsyncSession, product_id: UUID) -> list[ProductMaterialVariant]:
        result = await db.execute(
            select(ProductMaterialVariant)
            .where(ProductMaterialVariant.product_id == product_id)
            .order_by(ProductMaterialVariant.display_order)
        )
        return list(result.scalars().all())

    @staticmethod
    async def get_default_variant(db: AsyncSession, product_id: UUID) -> Optional[ProductMaterialVariant]:
        result = await db.execute(
            select(ProductMaterialVariant)
            .where(ProductMaterialVariant.product_id == product_id, ProductMaterialVariant.is_default.is_(True))
        )
        return result.scalar_one_or_none()
