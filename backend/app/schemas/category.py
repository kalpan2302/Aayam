from pydantic import BaseModel, ConfigDict
from typing import Optional, List
from uuid import UUID

class CategoryResponse(BaseModel):
    id: UUID
    name: str
    slug: str
    icon: Optional[str] = None
    parent_id: Optional[UUID] = None
    display_order: int
    depth: int

    model_config = ConfigDict(from_attributes=True)

class CategoryListResponse(BaseModel):
    items: List[CategoryResponse]

    model_config = ConfigDict(from_attributes=True)
