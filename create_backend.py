import os

base_dir = r"D:\Projects\Personal\Aayam\backend\app"
os.makedirs(base_dir, exist_ok=True)

files = {
    "__init__.py": "",
    "config.py": """\
from functools import lru_cache
from pydantic_settings import BaseSettings, SettingsConfigDict
from pydantic import Field

class Settings(BaseSettings):
    DATABASE_URL: str = "postgresql+asyncpg://aayam:aayam_dev_123@localhost:5432/aayam"
    REDIS_URL: str = "redis://localhost:6379/0"
    AWS_S3_BUCKET: str = "aayam-assets-dev"
    AWS_REGION: str = "ap-south-1"
    CLOUDFRONT_DOMAIN: str = ""
    API_KEY: str = "dev-api-key-change-me"
    ADMIN_API_KEY: str = "dev-admin-key-change-me"
    ENVIRONMENT: str = "development"
    DEBUG: bool = True
    APP_NAME: str = 'Aayam Product API'
    APP_VERSION: str = '0.1.0'
    CORS_ORIGINS: list[str] = ['*']
    PAGE_SIZE_DEFAULT: int = 20
    PAGE_SIZE_MAX: int = 100

    model_config = SettingsConfigDict(env_file='.env', env_file_encoding='utf-8')

    @property
    def DATABASE_URL_SYNC(self) -> str:
        return self.DATABASE_URL.replace("postgresql+asyncpg", "postgresql+psycopg2")

@lru_cache
def get_settings() -> Settings:
    return Settings()
""",
    "database.py": """\
from typing import AsyncGenerator
from sqlalchemy.ext.asyncio import create_async_engine, async_sessionmaker, AsyncSession
from sqlalchemy.orm import declarative_base
from app.config import get_settings

settings = get_settings()

engine = create_async_engine(
    settings.DATABASE_URL,
    pool_size=20,
    max_overflow=10,
    echo=settings.DEBUG,
)

async_session_maker = async_sessionmaker(
    engine, class_=AsyncSession, expire_on_commit=False
)

Base = declarative_base()

async def get_db() -> AsyncGenerator[AsyncSession, None]:
    async with async_session_maker() as session:
        yield session

async def init_db():
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)
""",
    "main.py": """\
from contextlib import asynccontextmanager
from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
import time
from datetime import datetime

from app.config import get_settings
from app.database import init_db, engine
from app.api.v1.router import api_router

settings = get_settings()

@asynccontextmanager
async def lifespan(app: FastAPI):
    # Connect to DB on startup
    await init_db()
    yield
    # Dispose on shutdown
    await engine.dispose()

app = FastAPI(
    title=settings.APP_NAME,
    version=settings.APP_VERSION,
    docs_url='/docs',
    redoc_url='/redoc',
    lifespan=lifespan
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.CORS_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.middleware("http")
async def log_requests(request: Request, call_next):
    start_time = time.time()
    response = await call_next(request)
    process_time = time.time() - start_time
    print(f"Path: {request.url.path} - Time: {process_time:.4f}s")
    return response

app.include_router(api_router, prefix="/api/v1")

@app.get("/health")
async def health_check():
    return {
        "status": "healthy",
        "version": settings.APP_VERSION,
        "environment": settings.ENVIRONMENT,
        "timestamp": datetime.utcnow().isoformat()
    }

@app.exception_handler(404)
async def custom_404_handler(request, exc):
    return JSONResponse(
        status_code=404,
        content={"error": {"code": "NOT_FOUND", "message": "The requested resource was not found"}}
    )

@app.exception_handler(422)
async def custom_422_handler(request, exc):
    return JSONResponse(
        status_code=422,
        content={"error": {"code": "UNPROCESSABLE_ENTITY", "message": "Validation error", "details": exc.errors()}}
    )

@app.exception_handler(500)
async def custom_500_handler(request, exc):
    return JSONResponse(
        status_code=500,
        content={"error": {"code": "INTERNAL_SERVER_ERROR", "message": "An unexpected error occurred"}}
    )
""",
    "api/__init__.py": "",
    "api/deps.py": """\
from fastapi import Header, HTTPException, status
from app.database import get_db
from app.config import get_settings

settings = get_settings()

def verify_api_key(x_api_key: str = Header(...)):
    if x_api_key != settings.API_KEY:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid API Key"
        )
    return x_api_key

def verify_admin_key(x_api_key: str = Header(...)):
    if x_api_key != settings.ADMIN_API_KEY:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Invalid Admin API Key"
        )
    return x_api_key
""",
    "api/v1/__init__.py": "",
    "api/v1/router.py": """\
from fastapi import APIRouter
from app.api.v1 import products, brands, categories, assets

api_router = APIRouter()
api_router.include_router(products.router, prefix="/products", tags=["Products"])
api_router.include_router(brands.router, prefix="/brands", tags=["Brands"])
api_router.include_router(categories.router, prefix="/categories", tags=["Categories"])
api_router.include_router(assets.router, prefix="/assets", tags=["Assets"])
""",
    "api/v1/products.py": """\
from fastapi import APIRouter, Depends, Query, HTTPException, status
from typing import Optional
from decimal import Decimal
from uuid import UUID

from app.schemas.product import ProductListResponse, ProductDetail, ProductCreate, ProductUpdate
from app.api.deps import verify_admin_key

router = APIRouter()

@router.get("/", response_model=ProductListResponse)
async def list_products(
    page: int = Query(1, ge=1),
    page_size: int = Query(20, ge=1, le=100),
    category: Optional[str] = None,
    brand: Optional[str] = None,
    min_price: Optional[Decimal] = None,
    max_price: Optional[Decimal] = None,
    finish: Optional[str] = None,
    door_material: Optional[str] = None,
    door_thickness_mm: Optional[float] = None,
    sort: Optional[str] = None,
    search: Optional[str] = None
):
    return {"items": [], "meta": {"total": 0, "page": page, "page_size": page_size, "total_pages": 0}}

@router.get("/{product_id}", response_model=ProductDetail)
async def get_product(product_id: UUID):
    raise HTTPException(status_code=404, detail="Product not found")

@router.get("/sku/{sku}", response_model=ProductDetail)
async def get_product_by_sku(sku: str):
    raise HTTPException(status_code=404, detail="Product not found")

@router.post("/", response_model=ProductDetail, dependencies=[Depends(verify_admin_key)])
async def create_product(product: ProductCreate):
    raise HTTPException(status_code=501, detail="Not implemented")

@router.put("/{product_id}", response_model=ProductDetail, dependencies=[Depends(verify_admin_key)])
async def update_product(product_id: UUID, product: ProductUpdate):
    raise HTTPException(status_code=501, detail="Not implemented")

@router.delete("/{product_id}", status_code=status.HTTP_204_NO_CONTENT, dependencies=[Depends(verify_admin_key)])
async def delete_product(product_id: UUID):
    return None
""",
    "api/v1/brands.py": """\
from fastapi import APIRouter

router = APIRouter()

@router.get("/")
async def list_brands():
    return []

@router.get("/{slug}")
async def get_brand(slug: str):
    return {}

@router.get("/{slug}/products")
async def list_brand_products(slug: str):
    return []
""",
    "api/v1/categories.py": """\
from fastapi import APIRouter

router = APIRouter()

@router.get("/")
async def list_categories():
    return []

@router.get("/{slug}/products")
async def list_category_products(slug: str):
    return []
""",
    "api/v1/assets.py": """\
from fastapi import APIRouter, Depends, UploadFile, File
from app.api.deps import verify_admin_key

router = APIRouter()

@router.post("/upload", dependencies=[Depends(verify_admin_key)])
async def upload_asset(file: UploadFile = File(...)):
    return {"filename": file.filename}

@router.get("/{product_id}/qr")
async def generate_qr(product_id: str):
    return {"qr_url": f"http://example.com/qr/{product_id}"}
""",
    "schemas/__init__.py": "",
    "schemas/common.py": """\
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
""",
    "schemas/product.py": """\
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
""",
    "schemas/brand.py": """\
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
""",
    "schemas/category.py": """\
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
""",
    "models/__init__.py": """\
from app.models.product import Brand, Category, Product, ProductDimension, ProductCompatibility, Product3DAsset, ProductImage, ProductMaterial, ProductPriceHistory, ProductAttribute, Tag, ProductTag
from app.models.audit import AuditLog
""",
    "models/product.py": """\
from sqlalchemy import Column, String, Integer, ForeignKey, Boolean, DECIMAL, text
from sqlalchemy.dialects.postgresql import UUID, JSONB
from sqlalchemy.orm import mapped_column, relationship

from app.database import Base

class Brand(Base):
    __tablename__ = "brands"
    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    name = mapped_column(String(255), nullable=False)
    slug = mapped_column(String(255), unique=True, nullable=False)
    description = mapped_column(String)
    logo_url = mapped_column(String)
    country = mapped_column(String(100))
    is_active = mapped_column(Boolean, default=True)

    products = relationship("Product", back_populates="brand")

class Category(Base):
    __tablename__ = "categories"
    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    name = mapped_column(String(255), nullable=False)
    slug = mapped_column(String(255), unique=True, nullable=False)
    icon = mapped_column(String)
    parent_id = mapped_column(UUID(as_uuid=True), ForeignKey("categories.id"))
    display_order = mapped_column(Integer, default=0)
    depth = mapped_column(Integer, default=0)

    products = relationship("Product", back_populates="category")

class Product(Base):
    __tablename__ = "products"
    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    sku = mapped_column(String(100), unique=True, nullable=False)
    name = mapped_column(String(255), nullable=False)
    brand_id = mapped_column(UUID(as_uuid=True), ForeignKey("brands.id"), nullable=False)
    category_id = mapped_column(UUID(as_uuid=True), ForeignKey("categories.id"), nullable=False)
    price = mapped_column(DECIMAL, nullable=False)
    mrp = mapped_column(DECIMAL)
    currency = mapped_column(String(3), default="INR")
    finish = mapped_column(String(100))
    thumbnail_url = mapped_column(String)
    is_featured = mapped_column(Boolean, default=False)
    is_deleted = mapped_column(Boolean, default=False)

    brand = relationship("Brand", back_populates="products")
    category = relationship("Category", back_populates="products")
    dimension = relationship("ProductDimension", back_populates="product", uselist=False, cascade="all, delete-orphan")
    compatibility = relationship("ProductCompatibility", back_populates="product", uselist=False, cascade="all, delete-orphan")
    asset_3d = relationship("Product3DAsset", back_populates="product", uselist=False, cascade="all, delete-orphan")
    images = relationship("ProductImage", back_populates="product", cascade="all, delete-orphan")
    materials = relationship("ProductMaterial", back_populates="product", cascade="all, delete-orphan")
    price_history = relationship("ProductPriceHistory", back_populates="product", cascade="all, delete-orphan")
    attributes = relationship("ProductAttribute", back_populates="product", cascade="all, delete-orphan")
    tags = relationship("ProductTag", back_populates="product", cascade="all, delete-orphan")

class ProductDimension(Base):
    __tablename__ = "product_dimensions"
    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id"), unique=True)
    width_mm = mapped_column(DECIMAL)
    height_mm = mapped_column(DECIMAL)
    depth_mm = mapped_column(DECIMAL)

    product = relationship("Product", back_populates="dimension")

class ProductCompatibility(Base):
    __tablename__ = "product_compatibilities"
    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id"), unique=True)
    door_materials = mapped_column(JSONB) # list of str
    door_types = mapped_column(JSONB)
    opening_directions = mapped_column(JSONB)
    min_door_thickness_mm = mapped_column(DECIMAL)
    max_door_thickness_mm = mapped_column(DECIMAL)

    product = relationship("Product", back_populates="compatibility")

class Product3DAsset(Base):
    __tablename__ = "product_3d_assets"
    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id"), unique=True)
    model_url = mapped_column(String, nullable=False)
    model_format = mapped_column(String, nullable=False)
    file_size_bytes = mapped_column(Integer, nullable=False)
    polygon_count = mapped_column(Integer, nullable=False)
    thumbnail_url = mapped_column(String)
    anchor_x_mm = mapped_column(DECIMAL)
    anchor_y_mm = mapped_column(DECIMAL)
    anchor_z_mm = mapped_column(DECIMAL)

    product = relationship("Product", back_populates="asset_3d")

class ProductImage(Base):
    __tablename__ = "product_images"
    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id"))
    image_url = mapped_column(String, nullable=False)
    display_order = mapped_column(Integer, default=0)

    product = relationship("Product", back_populates="images")

class ProductMaterial(Base):
    __tablename__ = "product_materials"
    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id"))
    material_name = mapped_column(String, nullable=False)
    percentage = mapped_column(DECIMAL)

    product = relationship("Product", back_populates="materials")

class ProductPriceHistory(Base):
    __tablename__ = "product_price_history"
    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id"))
    price = mapped_column(DECIMAL, nullable=False)
    effective_date = mapped_column(String, nullable=False) # Simplified for stub

    product = relationship("Product", back_populates="price_history")

class ProductAttribute(Base):
    __tablename__ = "product_attributes"
    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id"))
    key = mapped_column(String, nullable=False)
    value = mapped_column(String, nullable=False)

    product = relationship("Product", back_populates="attributes")

class Tag(Base):
    __tablename__ = "tags"
    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    name = mapped_column(String(100), unique=True, nullable=False)

class ProductTag(Base):
    __tablename__ = "product_tags"
    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id"))
    tag_id = mapped_column(UUID(as_uuid=True), ForeignKey("tags.id"))

    product = relationship("Product", back_populates="tags")
""",
    "models/audit.py": """\
from sqlalchemy import Column, String, Integer, text
from sqlalchemy.dialects.postgresql import UUID, JSONB
from sqlalchemy.orm import mapped_column

from app.database import Base

class AuditLog(Base):
    __tablename__ = "audit_logs"
    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    entity_name = mapped_column(String, nullable=False)
    entity_id = mapped_column(UUID(as_uuid=True), nullable=False)
    action = mapped_column(String, nullable=False)
    changes = mapped_column(JSONB)
    user_id = mapped_column(UUID(as_uuid=True))
""",
    "services/__init__.py": "",
    "services/product_service.py": """\
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
""",
    "utils/__init__.py": "",
    "utils/validators.py": """\
import re

def validate_sku(sku: str) -> bool:
    return bool(re.match(r'^[A-Z0-9_-]{3,100}$', sku))

def validate_slug(slug: str) -> bool:
    return bool(re.match(r'^[a-z0-9]+(?:-[a-z0-9]+)*$', slug))

def sanitize_search_query(query: str) -> str:
    if not query:
        return ""
    return query.replace("%", "\\\\%").replace("_", "\\\\_")

def clamp_page_size(page_size: int, max_size: int = 100) -> int:
    if page_size < 1:
        return 1
    if page_size > max_size:
        return max_size
    return page_size
"""
}

for filepath, content in files.items():
    full_path = os.path.join(base_dir, filepath)
    os.makedirs(os.path.dirname(full_path), exist_ok=True)
    with open(full_path, "w", encoding="utf-8") as f:
        f.write(content)
print("Files created successfully.")
