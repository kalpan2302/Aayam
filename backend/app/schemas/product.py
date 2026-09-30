from pydantic import BaseModel, ConfigDict, Field
from typing import Optional, List
from decimal import Decimal
from uuid import UUID
from app.schemas.common import PaginationMeta

class ProductDimensionResponse(BaseModel):
    width_mm: Optional[Decimal] = None
    height_mm: Optional[Decimal] = None
    depth_mm: Optional[Decimal] = None

    model_config = ConfigDict(from_attributes=True)

class Product3DAssetResponse(BaseModel):
    model_url: str
    model_format: str
    file_size_bytes: int
    polygon_count: int
    thumbnail_url: Optional[str] = None
    anchor_x_mm: Optional[Decimal] = None
    anchor_y_mm: Optional[Decimal] = None
    anchor_z_mm: Optional[Decimal] = None

    model_config = ConfigDict(from_attributes=True)

class ProductCompatibilityResponse(BaseModel):
    door_materials: List[str]
    door_types: List[str]
    opening_directions: List[str]
    min_door_thickness_mm: Optional[Decimal] = None
    max_door_thickness_mm: Optional[Decimal] = None

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
    finish: str
    thumbnail_url: Optional[str] = None
    is_featured: bool

    model_config = ConfigDict(from_attributes=True)

class ProductDetail(BaseModel):
    id: UUID
    sku: str
    name: str
    brand_id: UUID
    category_id: UUID
    price: Decimal
    mrp: Optional[Decimal] = None
    currency: str
    finish: str
    thumbnail_url: Optional[str] = None
    is_featured: bool
    dimensions: Optional[ProductDimensionResponse] = None
    asset_3d: Optional[Product3DAssetResponse] = None
    compatibility: Optional[ProductCompatibilityResponse] = None
    images: List[str] = []

    model_config = ConfigDict(from_attributes=True)

class ProductListResponse(BaseModel):
    items: List[ProductListItem]
    meta: PaginationMeta

    model_config = ConfigDict(from_attributes=True)

class ProductCreate(BaseModel):
    sku: str = Field(pattern='^[A-Z0-9_-]{3,100}$')
    name: str = Field(min_length=2)
    brand_id: UUID
    category_id: UUID
    price: Decimal = Field(gt=0)
    mrp: Optional[Decimal] = None
    currency: str = "INR"
    finish: str
    thumbnail_url: Optional[str] = None
    is_featured: bool = False

class ProductUpdate(BaseModel):
    name: Optional[str] = Field(None, min_length=2)
    brand_id: Optional[UUID] = None
    category_id: Optional[UUID] = None
    price: Optional[Decimal] = Field(None, gt=0)
    mrp: Optional[Decimal] = None
    currency: Optional[str] = None
    finish: Optional[str] = None
    thumbnail_url: Optional[str] = None
    is_featured: Optional[bool] = None
