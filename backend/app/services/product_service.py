from typing import Optional, List, Tuple
from uuid import UUID
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.product import Product

class ProductService:
    @staticmethod
    async def list_products(db: AsyncSession, filters: dict, page: int, page_size: int) -> Tuple[List[Product], int]:
        return [], 0

    @staticmethod
    async def get_product(db: AsyncSession, product_id: UUID) -> Optional[Product]:
        return None

    @staticmethod
    async def get_product_by_sku(db: AsyncSession, sku: str) -> Optional[Product]:
        return None

    @staticmethod
    async def create_product(db: AsyncSession, data: dict) -> Product:
        return Product()

    @staticmethod
    async def update_product(db: AsyncSession, product_id: UUID, data: dict) -> Optional[Product]:
        return None

    @staticmethod
    async def soft_delete_product(db: AsyncSession, product_id: UUID) -> bool:
        return False
