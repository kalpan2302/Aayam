from pydantic import BaseModel, ConfigDict
from typing import Optional, List
from uuid import UUID

class BrandResponse(BaseModel):
    id: UUID
    name: str
    slug: str
    description: Optional[str] = None
    logo_url: Optional[str] = None
    country: Optional[str] = None
    is_active: bool

    model_config = ConfigDict(from_attributes=True)

class BrandListResponse(BaseModel):
    items: List[BrandResponse]

    model_config = ConfigDict(from_attributes=True)
