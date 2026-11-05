"""
Pydantic schemas for product material variants (Phase 6 Step 6) - request/
response models.
"""
from decimal import Decimal
from typing import Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field


class MaterialVariantResponse(BaseModel):
    id: UUID
    product_id: UUID
    finish_name: str
    display_order: int
    base_color_hex: str
    metallic_factor: Decimal
    roughness_factor: Decimal
    model_url: Optional[str] = None
    thumbnail_url: Optional[str] = None
    is_default: bool
    model_config = ConfigDict(from_attributes=True)


class MaterialVariantCreate(BaseModel):
    finish_name: str = Field(min_length=1, max_length=100)
    display_order: int = 0
    base_color_hex: str = Field(pattern=r'^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$')
    metallic_factor: Decimal = Field(ge=0, le=1)
    roughness_factor: Decimal = Field(ge=0, le=1)
    model_url: Optional[str] = None
    thumbnail_url: Optional[str] = None
    is_default: bool = False
