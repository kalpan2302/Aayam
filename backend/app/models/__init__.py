"""
Import all models so Alembic can auto-detect them.
"""
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
from app.models.audit import AuditLog

__all__ = [
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
    "AuditLog",
]
