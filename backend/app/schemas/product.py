"""
Pydantic schemas for Product API - request/response models.
"""
from datetime import datetime
from decimal import Decimal
from typing import Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field

from app.schemas.common import PaginationMeta


class ProductDimensionResponse(BaseModel):
    width_mm: Optional[Decimal] = None
    height_mm: Optional[Decimal] = None
    depth_mm: Optional[Decimal] = None
    weight_grams: Optional[Decimal] = None
    model_config = ConfigDict(from_attributes=True)


class Product3DAssetResponse(BaseModel):
    model_url: str
    model_format: str
    file_size_bytes: int
    polygon_count: int
    thumbnail_url: Optional[str] = None
    anchor_x_mm: Decimal
    anchor_y_mm: Decimal
    anchor_z_mm: Decimal
    lod_level: int = 0
    model_config = ConfigDict(from_attributes=True)


class ProductCompatibilityResponse(BaseModel):
    door_materials: list[str]
    door_types: list[str]
    opening_directions: list[str]
    min_door_thickness_mm: Optional[Decimal] = None
    max_door_thickness_mm: Optional[Decimal] = None
    bore_diameter_mm: Optional[Decimal] = None
    backset_mm: Optional[Decimal] = None
    model_config = ConfigDict(from_attributes=True)


class ProductImageResponse(BaseModel):
    id: UUID
    image_url: str
    alt_text: Optional[str] = None
    display_order: int
    is_primary: bool
    model_config = ConfigDict(from_attributes=True)


class ProductMaterialResponse(BaseModel):
    material_name: str
    percentage: Optional[Decimal] = None
    model_config = ConfigDict(from_attributes=True)


class ProductListItem(BaseModel):
    id: UUID
    sku: str
    name: str
    brand_name: str
    category_name: str
    price: Decimal
    mrp: Optional[Decimal] = None
    currency: str
    finish: Optional[str] = None
    mounting_type: Optional[str] = None
    thumbnail_url: Optional[str] = None
    is_featured: bool
    has_3d_model: bool = False
    model_config = ConfigDict(from_attributes=True)


class ProductListResponse(BaseModel):
    items: list[ProductListItem]
    meta: PaginationMeta
    model_config = ConfigDict(from_attributes=True)


class ProductDetail(BaseModel):
    id: UUID
    sku: str
    name: str
    description: Optional[str] = None
    brand_id: UUID
    brand_name: str = ""
    category_id: UUID
    category_name: str = ""
    price: Decimal
    mrp: Optional[Decimal] = None
    currency: str
    finish: Optional[str] = None
    mounting_type: Optional[str] = None
    thumbnail_url: Optional[str] = None
    is_featured: bool
    is_active: bool
    created_at: datetime
    updated_at: datetime
    dimensions: Optional[ProductDimensionResponse] = None
    asset_3d: Optional[Product3DAssetResponse] = None
    compatibility: Optional[ProductCompatibilityResponse] = None
    images: list[ProductImageResponse] = []
    materials: list[ProductMaterialResponse] = []
    model_config = ConfigDict(from_attributes=True)


class ProductCreate(BaseModel):
    sku: str = Field(pattern=r'^[A-Z0-9_-]{3,100}$')
    name: str = Field(min_length=2, max_length=255)
    description: Optional[str] = None
    brand_id: UUID
    category_id: UUID
    price: Decimal = Field(gt=0)
    mrp: Optional[Decimal] = Field(None, gt=0)
    currency: str = "INR"
    finish: Optional[str] = None
    mounting_type: Optional[str] = None
    thumbnail_url: Optional[str] = None
    is_featured: bool = False


class ProductUpdate(BaseModel):
    name: Optional[str] = Field(None, min_length=2, max_length=255)
    description: Optional[str] = None
    brand_id: Optional[UUID] = None
    category_id: Optional[UUID] = None
    price: Optional[Decimal] = Field(None, gt=0)
    mrp: Optional[Decimal] = Field(None, gt=0)
    currency: Optional[str] = None
    finish: Optional[str] = None
    mounting_type: Optional[str] = None
    thumbnail_url: Optional[str] = None
    is_featured: Optional[bool] = None
    is_active: Optional[bool] = None