from pydantic import BaseModel
from typing import Any, Optional

class ErrorDetail(BaseModel):
    code: str
    message: str
    details: Optional[Any] = None

class ErrorResponse(BaseModel):
    error: ErrorDetail
    status: int

class HealthResponse(BaseModel):
    status: str
    version: str
    environment: str
    timestamp: str

class PaginationMeta(BaseModel):
    total: int
    page: int
    page_size: int
    total_pages: int
