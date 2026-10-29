"""
Model package - re-exports all ORM classes for convenient imports.

Usage:
    from app.models import Product, Brand, Category
"""
from app.database import Base  # noqa: F401 - needed by Alembic env.py

from app.models.product import (
    Brand,
    Category,
    Product,
    ProductDimension,
    ProductCompatibility,
    Product3DAsset,
    ProductImage,
    ProductMaterial,
    ProductPriceHistory,
    ProductAttribute,
    Tag,
    ProductTag,
)
from app.models.measurement import (
    Device,
    Door,
    DoorMeasurement,
)

__all__ = [
    "Base",
    "Brand",
    "Category",
    "Product",
    "ProductDimension",
    "ProductCompatibility",
    "Product3DAsset",
    "ProductImage",
    "ProductMaterial",
    "ProductPriceHistory",
    "ProductAttribute",
    "Tag",
    "ProductTag",
    "Device",
    "Door",
    "DoorMeasurement",
]