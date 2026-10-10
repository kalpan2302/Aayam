"""
ORM Models - matches migration 001_initial_schema exactly.

Design decisions:
  - UUID PKs on all tables (API-safe, merge-safe across environments)
  - product_materials and product_tags use composite PKs (no surrogate)
  - Relationships use cascade="all, delete-orphan" for child tables
  - JSONB columns on compatibility for flexible door_materials/door_types
  - DECIMAL with precision for all measurements (mm, grams, currency)
"""
from sqlalchemy import (
    String, Integer, Text, ForeignKey, Boolean,
    DECIMAL, DateTime, UniqueConstraint, CheckConstraint,
    text,
)
from sqlalchemy.dialects.postgresql import UUID, JSONB
from sqlalchemy.orm import mapped_column, relationship

from app.database import Base


class Brand(Base):
    __tablename__ = "brands"

    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    name = mapped_column(String(255), unique=True, nullable=False)
    slug = mapped_column(String(255), unique=True, nullable=False)
    description = mapped_column(String, nullable=True)
    logo_url = mapped_column(String, nullable=True)
    country = mapped_column(String(100), nullable=True)
    is_active = mapped_column(Boolean, server_default=text("true"), nullable=False)
    created_at = mapped_column(DateTime(timezone=True), server_default=text("now()"), nullable=False)
    updated_at = mapped_column(DateTime(timezone=True), server_default=text("now()"), nullable=False)

    products = relationship("Product", back_populates="brand")


class Category(Base):
    __tablename__ = "categories"

    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    name = mapped_column(String(255), nullable=False)
    slug = mapped_column(String(255), unique=True, nullable=False)
    icon = mapped_column(String, nullable=True)
    parent_id = mapped_column(UUID(as_uuid=True), ForeignKey("categories.id", ondelete="SET NULL"), nullable=True)
    display_order = mapped_column(Integer, server_default=text("0"), nullable=False)
    depth = mapped_column(Integer, server_default=text("0"), nullable=False)

    parent = relationship("Category", remote_side="Category.id", backref="children")
    products = relationship("Product", back_populates="category")


class Product(Base):
    __tablename__ = "products"
    __table_args__ = (
        CheckConstraint("price > 0", name="ck_products_price_positive"),
        CheckConstraint("mrp IS NULL OR mrp >= price", name="ck_products_mrp_gte_price"),
        CheckConstraint("deleted_at IS NULL OR is_active = false", name="ck_products_deleted_inactive"),
    )

    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    sku = mapped_column(String(100), unique=True, nullable=False)
    name = mapped_column(String(255), nullable=False)
    description = mapped_column(Text, nullable=True)
    brand_id = mapped_column(UUID(as_uuid=True), ForeignKey("brands.id", ondelete="RESTRICT"), nullable=False)
    category_id = mapped_column(UUID(as_uuid=True), ForeignKey("categories.id", ondelete="RESTRICT"), nullable=False)
    price = mapped_column(DECIMAL(10, 2), nullable=False)
    mrp = mapped_column(DECIMAL(10, 2), nullable=True)
    currency = mapped_column(String(3), server_default=text("'INR'"), nullable=False)
    finish = mapped_column(String(100), nullable=True)
    mounting_type = mapped_column(String(50), nullable=True)
    thumbnail_url = mapped_column(String, nullable=True)
    is_featured = mapped_column(Boolean, server_default=text("false"), nullable=False)
    is_active = mapped_column(Boolean, server_default=text("true"), nullable=False)
    is_deleted = mapped_column(Boolean, server_default=text("false"), nullable=False)
    created_at = mapped_column(DateTime(timezone=True), server_default=text("now()"), nullable=False)
    updated_at = mapped_column(DateTime(timezone=True), server_default=text("now()"), nullable=False)
    deleted_at = mapped_column(DateTime(timezone=True), nullable=True)

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
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id", ondelete="CASCADE"), unique=True, nullable=False)
    width_mm = mapped_column(DECIMAL(8, 2), nullable=True)
    height_mm = mapped_column(DECIMAL(8, 2), nullable=True)
    depth_mm = mapped_column(DECIMAL(8, 2), nullable=True)
    weight_grams = mapped_column(DECIMAL(8, 2), nullable=True)

    product = relationship("Product", back_populates="dimension")


class ProductCompatibility(Base):
    __tablename__ = "product_compatibilities"
    __table_args__ = (
        CheckConstraint(
            "min_door_thickness_mm IS NULL OR max_door_thickness_mm IS NULL OR min_door_thickness_mm <= max_door_thickness_mm",
            name="ck_compat_thickness_range",
        ),
    )

    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id", ondelete="CASCADE"), unique=True, nullable=False)
    door_materials = mapped_column(JSONB, server_default=text("'[]'::jsonb"), nullable=False)
    door_types = mapped_column(JSONB, server_default=text("'[]'::jsonb"), nullable=False)
    opening_directions = mapped_column(JSONB, server_default=text("'[]'::jsonb"), nullable=False)
    min_door_thickness_mm = mapped_column(DECIMAL(6, 2), nullable=True)
    max_door_thickness_mm = mapped_column(DECIMAL(6, 2), nullable=True)
    bore_diameter_mm = mapped_column(DECIMAL(6, 2), nullable=True)
    backset_mm = mapped_column(DECIMAL(6, 2), nullable=True)

    product = relationship("Product", back_populates="compatibility")


class Product3DAsset(Base):
    __tablename__ = "product_3d_assets"
    __table_args__ = (
        UniqueConstraint("product_id", "lod_level", name="uq_product_3d_assets_product_lod"),
        CheckConstraint("file_size_bytes > 0", name="ck_3d_assets_filesize"),
        CheckConstraint("polygon_count > 0", name="ck_3d_assets_polygons"),
        CheckConstraint("model_format IN ('glb', 'gltf', 'usdz', 'fbx', 'obj')", name="ck_3d_assets_format"),
    )

    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id", ondelete="CASCADE"), nullable=False)
    model_url = mapped_column(String, nullable=False)
    model_format = mapped_column(String(20), nullable=False)
    file_size_bytes = mapped_column(Integer, nullable=False)
    polygon_count = mapped_column(Integer, nullable=False)
    thumbnail_url = mapped_column(String, nullable=True)
    anchor_x_mm = mapped_column(DECIMAL(8, 2), server_default=text("0"), nullable=False)
    anchor_y_mm = mapped_column(DECIMAL(8, 2), server_default=text("0"), nullable=False)
    anchor_z_mm = mapped_column(DECIMAL(8, 2), server_default=text("0"), nullable=False)
    lod_level = mapped_column(Integer, server_default=text("0"), nullable=False)
    created_at = mapped_column(DateTime(timezone=True), server_default=text("now()"), nullable=False)

    product = relationship("Product", back_populates="asset_3d")


class ProductImage(Base):
    __tablename__ = "product_images"

    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id", ondelete="CASCADE"), nullable=False)
    image_url = mapped_column(String, nullable=False)
    alt_text = mapped_column(String(255), nullable=True)
    display_order = mapped_column(Integer, server_default=text("0"), nullable=False)
    is_primary = mapped_column(Boolean, server_default=text("false"), nullable=False)

    product = relationship("Product", back_populates="images")


class ProductMaterial(Base):
    __tablename__ = "product_materials"
    __table_args__ = (
        CheckConstraint("percentage IS NULL OR (percentage > 0 AND percentage <= 100)", name="ck_materials_percentage"),
    )

    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id", ondelete="CASCADE"), primary_key=True)
    material_name = mapped_column(String(100), primary_key=True)
    percentage = mapped_column(DECIMAL(5, 2), nullable=True)

    product = relationship("Product", back_populates="materials")


class ProductPriceHistory(Base):
    __tablename__ = "product_price_history"

    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id", ondelete="CASCADE"), nullable=False)
    old_price = mapped_column(DECIMAL(10, 2), nullable=True)
    new_price = mapped_column(DECIMAL(10, 2), nullable=False)
    changed_at = mapped_column(DateTime(timezone=True), server_default=text("now()"), nullable=False)

    product = relationship("Product", back_populates="price_history")


class ProductAttribute(Base):
    __tablename__ = "product_attributes"
    __table_args__ = (
        UniqueConstraint("product_id", "key", name="uq_product_attributes_product_key"),
    )

    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id", ondelete="CASCADE"), nullable=False)
    key = mapped_column(String(100), nullable=False)
    value = mapped_column(Text, nullable=False)

    product = relationship("Product", back_populates="attributes")


class Tag(Base):
    __tablename__ = "tags"

    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    name = mapped_column(String(100), unique=True, nullable=False)


class ProductTag(Base):
    __tablename__ = "product_tags"

    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id", ondelete="CASCADE"), primary_key=True)
    tag_id = mapped_column(UUID(as_uuid=True), ForeignKey("tags.id", ondelete="CASCADE"), primary_key=True)

    product = relationship("Product", back_populates="tags")
    tag = relationship("Tag")