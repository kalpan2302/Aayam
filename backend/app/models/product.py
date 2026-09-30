"""
SQLAlchemy 2.0 ORM models for Aayam product database.

Follows the schema defined in docs/phases/phase-2-database-design.md:
- Brands/Categories use SERIAL (Integer) PKs (internal-only IDs)
- Products use UUID PKs (API-exposed, merge-safe)
- Soft deletes via deleted_at timestamp
- All temporal fields use TIMESTAMPTZ
"""

from datetime import datetime
from decimal import Decimal as PyDecimal

from sqlalchemy import (
    Boolean, DateTime, ForeignKey, Integer, Numeric, String, Text,
    UniqueConstraint, CheckConstraint, Index, text,
)
from sqlalchemy.dialects.postgresql import JSONB, UUID
from sqlalchemy.orm import Mapped, mapped_column, relationship
from sqlalchemy.sql import func

from app.database import Base


# ═══════════════════════════════════════════════════════════
# BRANDS
# ═══════════════════════════════════════════════════════════

class Brand(Base):
    __tablename__ = "brands"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    name: Mapped[str] = mapped_column(String(255), nullable=False, unique=True)
    slug: Mapped[str] = mapped_column(String(255), nullable=False, unique=True, index=True)
    description: Mapped[str | None] = mapped_column(Text)
    logo_url: Mapped[str | None] = mapped_column(String(500))
    website: Mapped[str | None] = mapped_column(String(500))
    country: Mapped[str] = mapped_column(String(100), nullable=False, server_default="India")
    is_active: Mapped[bool] = mapped_column(Boolean, nullable=False, server_default=text("true"))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False, server_default=func.now())
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False, server_default=func.now(), onupdate=func.now())

    # Relationships
    products: Mapped[list["Product"]] = relationship(back_populates="brand")

    __table_args__ = (
        CheckConstraint("LENGTH(TRIM(name)) > 0", name="chk_brands_name_not_empty"),
        CheckConstraint("slug ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'", name="chk_brands_slug_format"),
    )

    def __repr__(self) -> str:
        return f"<Brand {self.slug}>"


# ═══════════════════════════════════════════════════════════
# CATEGORIES
# ═══════════════════════════════════════════════════════════

class Category(Base):
    __tablename__ = "categories"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    name: Mapped[str] = mapped_column(String(255), nullable=False)
    slug: Mapped[str] = mapped_column(String(255), nullable=False, unique=True, index=True)
    parent_id: Mapped[int | None] = mapped_column(Integer, ForeignKey("categories.id", ondelete="RESTRICT", onupdate="CASCADE"))
    icon: Mapped[str | None] = mapped_column(String(100))
    display_order: Mapped[int] = mapped_column(Integer, nullable=False, server_default=text("0"))
    depth: Mapped[int] = mapped_column(Integer, nullable=False, server_default=text("0"))
    is_active: Mapped[bool] = mapped_column(Boolean, nullable=False, server_default=text("true"))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False, server_default=func.now())

    # Relationships
    parent: Mapped["Category | None"] = relationship(remote_side=[id])
    products: Mapped[list["Product"]] = relationship(back_populates="category")

    __table_args__ = (
        CheckConstraint("LENGTH(TRIM(name)) > 0", name="chk_categories_name_not_empty"),
        CheckConstraint("slug ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'", name="chk_categories_slug_format"),
        CheckConstraint("parent_id IS NULL OR parent_id != id", name="chk_categories_no_self_parent"),
        CheckConstraint("depth >= 0", name="chk_categories_depth_non_negative"),
        CheckConstraint("depth <= 5", name="chk_categories_max_depth"),
    )

    def __repr__(self) -> str:
        return f"<Category {self.slug}>"


# ═══════════════════════════════════════════════════════════
# PRODUCTS
# ═══════════════════════════════════════════════════════════

class Product(Base):
    __tablename__ = "products"

    id: Mapped[str] = mapped_column(UUID(as_uuid=False), primary_key=True, server_default=text("gen_random_uuid()"))
    sku: Mapped[str] = mapped_column(String(100), nullable=False, unique=True, index=True)
    name: Mapped[str] = mapped_column(String(255), nullable=False)
    description: Mapped[str | None] = mapped_column(Text)

    # References
    brand_id: Mapped[int] = mapped_column(Integer, ForeignKey("brands.id", ondelete="RESTRICT", onupdate="CASCADE"), nullable=False)
    category_id: Mapped[int] = mapped_column(Integer, ForeignKey("categories.id", ondelete="RESTRICT", onupdate="CASCADE"), nullable=False)

    # Pricing
    price: Mapped[PyDecimal] = mapped_column(Numeric(10, 2), nullable=False)
    mrp: Mapped[PyDecimal | None] = mapped_column(Numeric(10, 2))
    currency: Mapped[str] = mapped_column(String(3), nullable=False, server_default="INR")

    # Physical properties
    finish: Mapped[str | None] = mapped_column(String(100))
    weight_grams: Mapped[int | None] = mapped_column(Integer)
    material_primary: Mapped[str | None] = mapped_column(String(100))

    # Mounting
    mounting_type: Mapped[str | None] = mapped_column(String(50))
    bore_diameter_mm: Mapped[PyDecimal | None] = mapped_column(Numeric(6, 2))
    backset_mm: Mapped[PyDecimal | None] = mapped_column(Numeric(6, 2))

    # Status
    is_active: Mapped[bool] = mapped_column(Boolean, nullable=False, server_default=text("true"))
    is_featured: Mapped[bool] = mapped_column(Boolean, nullable=False, server_default=text("false"))

    # Temporal
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False, server_default=func.now())
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False, server_default=func.now(), onupdate=func.now())
    deleted_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))

    # Relationships
    brand: Mapped["Brand"] = relationship(back_populates="products")
    category: Mapped["Category"] = relationship(back_populates="products")
    dimensions: Mapped["ProductDimension | None"] = relationship(back_populates="product", uselist=False, cascade="all, delete-orphan")
    compatibility: Mapped["ProductCompatibility | None"] = relationship(back_populates="product", uselist=False, cascade="all, delete-orphan")
    asset_3d: Mapped["Product3DAsset | None"] = relationship(back_populates="product", uselist=False, cascade="all, delete-orphan")
    images: Mapped[list["ProductImage"]] = relationship(back_populates="product", cascade="all, delete-orphan", order_by="ProductImage.display_order")
    materials: Mapped[list["ProductMaterial"]] = relationship(back_populates="product", cascade="all, delete-orphan")
    price_history: Mapped[list["ProductPriceHistory"]] = relationship(back_populates="product", cascade="all, delete-orphan")
    attributes: Mapped[list["ProductAttribute"]] = relationship(back_populates="product", cascade="all, delete-orphan")
    tags: Mapped[list["ProductTag"]] = relationship(back_populates="product", cascade="all, delete-orphan")

    __table_args__ = (
        CheckConstraint("sku ~ '^[A-Z0-9_-]{3,100}$'", name="chk_products_sku_format"),
        CheckConstraint("LENGTH(TRIM(name)) >= 2", name="chk_products_name_not_empty"),
        CheckConstraint("price > 0", name="chk_products_price_positive"),
        CheckConstraint("mrp IS NULL OR mrp >= price", name="chk_products_mrp_gte_price"),
        CheckConstraint("weight_grams IS NULL OR weight_grams >= 0", name="chk_products_weight_non_negative"),
        CheckConstraint("deleted_at IS NULL OR is_active = false", name="chk_products_soft_delete_consistency"),
        Index("idx_products_brand_id", "brand_id", postgresql_where=text("deleted_at IS NULL")),
        Index("idx_products_category_id", "category_id", postgresql_where=text("deleted_at IS NULL")),
        Index("idx_products_category_price", "category_id", "price", postgresql_where=text("deleted_at IS NULL")),
        Index("idx_products_active", "id", postgresql_where=text("deleted_at IS NULL AND is_active = true")),
    )

    def __repr__(self) -> str:
        return f"<Product {self.sku}>"


# ═══════════════════════════════════════════════════════════
# PRODUCT DIMENSIONS (1:1)
# ═══════════════════════════════════════════════════════════

class ProductDimension(Base):
    __tablename__ = "product_dimensions"

    product_id: Mapped[str] = mapped_column(UUID(as_uuid=False), ForeignKey("products.id", ondelete="CASCADE", onupdate="CASCADE"), primary_key=True)
    width_mm: Mapped[PyDecimal | None] = mapped_column(Numeric(8, 2))
    height_mm: Mapped[PyDecimal | None] = mapped_column(Numeric(8, 2))
    depth_mm: Mapped[PyDecimal | None] = mapped_column(Numeric(8, 2))
    lever_length_mm: Mapped[PyDecimal | None] = mapped_column(Numeric(8, 2))
    rose_diameter_mm: Mapped[PyDecimal | None] = mapped_column(Numeric(8, 2))
    face_plate_width_mm: Mapped[PyDecimal | None] = mapped_column(Numeric(8, 2))
    face_plate_height_mm: Mapped[PyDecimal | None] = mapped_column(Numeric(8, 2))

    product: Mapped["Product"] = relationship(back_populates="dimensions")

    __table_args__ = (
        CheckConstraint("width_mm IS NULL OR width_mm > 0", name="chk_dimensions_width_positive"),
        CheckConstraint("height_mm IS NULL OR height_mm > 0", name="chk_dimensions_height_positive"),
        CheckConstraint("depth_mm IS NULL OR depth_mm > 0", name="chk_dimensions_depth_positive"),
        CheckConstraint(
            "COALESCE(width_mm, 0) <= 2000 AND COALESCE(height_mm, 0) <= 2000 AND COALESCE(depth_mm, 0) <= 2000",
            name="chk_dimensions_max_size",
        ),
    )


# ═══════════════════════════════════════════════════════════
# PRODUCT COMPATIBILITY (1:1)
# ═══════════════════════════════════════════════════════════

class ProductCompatibility(Base):
    __tablename__ = "product_compatibility"

    product_id: Mapped[str] = mapped_column(UUID(as_uuid=False), ForeignKey("products.id", ondelete="CASCADE", onupdate="CASCADE"), primary_key=True)
    door_materials: Mapped[list] = mapped_column(JSONB, nullable=False, server_default=text("'[]'::jsonb"))
    door_types: Mapped[list] = mapped_column(JSONB, nullable=False, server_default=text("'[]'::jsonb"))
    opening_directions: Mapped[list] = mapped_column(JSONB, nullable=False, server_default=text("'[]'::jsonb"))
    min_door_thickness_mm: Mapped[PyDecimal | None] = mapped_column(Numeric(6, 2))
    max_door_thickness_mm: Mapped[PyDecimal | None] = mapped_column(Numeric(6, 2))

    product: Mapped["Product"] = relationship(back_populates="compatibility")

    __table_args__ = (
        CheckConstraint(
            "min_door_thickness_mm IS NULL OR max_door_thickness_mm IS NULL OR min_door_thickness_mm <= max_door_thickness_mm",
            name="chk_compat_thickness_range",
        ),
        CheckConstraint("min_door_thickness_mm IS NULL OR min_door_thickness_mm > 0", name="chk_compat_min_thickness_positive"),
        Index("idx_compat_door_materials", "door_materials", postgresql_using="gin"),
        Index("idx_compat_door_types", "door_types", postgresql_using="gin"),
    )


# ═══════════════════════════════════════════════════════════
# PRODUCT 3D ASSETS (1:1)
# ═══════════════════════════════════════════════════════════

class Product3DAsset(Base):
    __tablename__ = "product_3d_assets"

    product_id: Mapped[str] = mapped_column(UUID(as_uuid=False), ForeignKey("products.id", ondelete="CASCADE", onupdate="CASCADE"), primary_key=True)
    model_url: Mapped[str] = mapped_column(String(500), nullable=False)
    model_format: Mapped[str] = mapped_column(String(10), nullable=False, server_default="glb")
    file_size_bytes: Mapped[int | None] = mapped_column(Integer)
    polygon_count: Mapped[int | None] = mapped_column(Integer)
    thumbnail_url: Mapped[str | None] = mapped_column(String(500))

    # Anchor point (door-relative in mm)
    anchor_x_mm: Mapped[PyDecimal] = mapped_column(Numeric(8, 2), nullable=False, server_default=text("0"))
    anchor_y_mm: Mapped[PyDecimal] = mapped_column(Numeric(8, 2), nullable=False, server_default=text("1000"))
    anchor_z_mm: Mapped[PyDecimal] = mapped_column(Numeric(8, 2), nullable=False, server_default=text("0"))

    # LOD variants
    lod_high_url: Mapped[str | None] = mapped_column(String(500))
    lod_medium_url: Mapped[str | None] = mapped_column(String(500))
    lod_low_url: Mapped[str | None] = mapped_column(String(500))

    # Quality metadata
    has_pbr_materials: Mapped[bool] = mapped_column(Boolean, nullable=False, server_default=text("true"))
    texture_resolution: Mapped[int] = mapped_column(Integer, nullable=False, server_default=text("2048"))

    # Temporal
    last_validated_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    uploaded_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False, server_default=func.now())

    product: Mapped["Product"] = relationship(back_populates="asset_3d")

    __table_args__ = (
        CheckConstraint("model_url ~ '^https?://'", name="chk_asset_model_url_format"),
        CheckConstraint("file_size_bytes IS NULL OR file_size_bytes > 0", name="chk_asset_file_size_positive"),
        CheckConstraint("file_size_bytes IS NULL OR file_size_bytes <= 52428800", name="chk_asset_file_size_max"),  # 50MB
        CheckConstraint("polygon_count IS NULL OR (polygon_count > 0 AND polygon_count <= 500000)", name="chk_asset_polygon_count_range"),
        CheckConstraint("texture_resolution IN (256, 512, 1024, 2048, 4096)", name="chk_asset_texture_resolution"),
        CheckConstraint("anchor_y_mm >= 0", name="chk_asset_anchor_y_non_negative"),
    )


# ═══════════════════════════════════════════════════════════
# PRODUCT IMAGES (1:N)
# ═══════════════════════════════════════════════════════════

class ProductImage(Base):
    __tablename__ = "product_images"

    id: Mapped[str] = mapped_column(UUID(as_uuid=False), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id: Mapped[str] = mapped_column(UUID(as_uuid=False), ForeignKey("products.id", ondelete="CASCADE", onupdate="CASCADE"), nullable=False)
    image_url: Mapped[str] = mapped_column(String(500), nullable=False)
    alt_text: Mapped[str | None] = mapped_column(String(255))
    is_primary: Mapped[bool] = mapped_column(Boolean, nullable=False, server_default=text("false"))
    display_order: Mapped[int] = mapped_column(Integer, nullable=False, server_default=text("0"))
    width_px: Mapped[int | None] = mapped_column(Integer)
    height_px: Mapped[int | None] = mapped_column(Integer)
    file_size_bytes: Mapped[int | None] = mapped_column(Integer)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False, server_default=func.now())

    product: Mapped["Product"] = relationship(back_populates="images")

    __table_args__ = (
        CheckConstraint("image_url ~ '^https?://'", name="chk_images_url_format"),
        CheckConstraint("display_order >= 0", name="chk_images_display_order_non_negative"),
        Index("idx_images_product_order", "product_id", "display_order"),
    )


# ═══════════════════════════════════════════════════════════
# PRODUCT MATERIALS (M:N junction)
# ═══════════════════════════════════════════════════════════

class ProductMaterial(Base):
    __tablename__ = "product_materials"

    product_id: Mapped[str] = mapped_column(UUID(as_uuid=False), ForeignKey("products.id", ondelete="CASCADE", onupdate="CASCADE"), primary_key=True)
    material_name: Mapped[str] = mapped_column(String(100), primary_key=True)
    percentage: Mapped[PyDecimal | None] = mapped_column(Numeric(5, 2))

    product: Mapped["Product"] = relationship(back_populates="materials")

    __table_args__ = (
        CheckConstraint("LENGTH(TRIM(material_name)) > 0", name="chk_materials_name_not_empty"),
        CheckConstraint("percentage IS NULL OR (percentage > 0 AND percentage <= 100)", name="chk_materials_percentage_range"),
    )


# ═══════════════════════════════════════════════════════════
# PRODUCT PRICE HISTORY (immutable append-only)
# ═══════════════════════════════════════════════════════════

class ProductPriceHistory(Base):
    __tablename__ = "product_price_history"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    product_id: Mapped[str] = mapped_column(UUID(as_uuid=False), ForeignKey("products.id", ondelete="CASCADE", onupdate="CASCADE"), nullable=False)
    price: Mapped[PyDecimal] = mapped_column(Numeric(10, 2), nullable=False)
    mrp: Mapped[PyDecimal | None] = mapped_column(Numeric(10, 2))
    reason: Mapped[str | None] = mapped_column(String(255))
    effective_from: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False, server_default=func.now())
    effective_until: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False, server_default=func.now())

    product: Mapped["Product"] = relationship(back_populates="price_history")

    __table_args__ = (
        CheckConstraint("price > 0", name="chk_price_history_price_positive"),
        CheckConstraint("mrp IS NULL OR mrp >= price", name="chk_price_history_mrp_gte_price"),
        CheckConstraint("effective_until IS NULL OR effective_until > effective_from", name="chk_price_history_date_range"),
        Index("idx_price_history_product_active", "product_id", "effective_from", postgresql_where=text("effective_until IS NULL")),
    )


# ═══════════════════════════════════════════════════════════
# PRODUCT ATTRIBUTES (EAV extension)
# ═══════════════════════════════════════════════════════════

class ProductAttribute(Base):
    __tablename__ = "product_attributes"

    product_id: Mapped[str] = mapped_column(UUID(as_uuid=False), ForeignKey("products.id", ondelete="CASCADE", onupdate="CASCADE"), primary_key=True)
    attribute_key: Mapped[str] = mapped_column(String(100), primary_key=True)
    attribute_value: Mapped[str] = mapped_column(Text, nullable=False)
    attribute_type: Mapped[str] = mapped_column(String(20), nullable=False, server_default="string")

    product: Mapped["Product"] = relationship(back_populates="attributes")

    __table_args__ = (
        CheckConstraint("attribute_key ~ '^[a-z_][a-z0-9_]*$'", name="chk_attributes_key_format"),
        CheckConstraint("LENGTH(TRIM(attribute_value)) > 0", name="chk_attributes_value_not_empty"),
        CheckConstraint("attribute_type IN ('string', 'number', 'boolean', 'json')", name="chk_attributes_type_valid"),
        Index("idx_attributes_key", "attribute_key"),
    )


# ═══════════════════════════════════════════════════════════
# TAGS (M:N via junction)
# ═══════════════════════════════════════════════════════════

class Tag(Base):
    __tablename__ = "tags"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    name: Mapped[str] = mapped_column(String(100), nullable=False, unique=True)
    slug: Mapped[str] = mapped_column(String(100), nullable=False, unique=True)

    __table_args__ = (
        CheckConstraint("slug ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'", name="chk_tags_slug_format"),
    )


class ProductTag(Base):
    __tablename__ = "product_tags"

    product_id: Mapped[str] = mapped_column(UUID(as_uuid=False), ForeignKey("products.id", ondelete="CASCADE"), primary_key=True)
    tag_id: Mapped[int] = mapped_column(Integer, ForeignKey("tags.id", ondelete="CASCADE"), primary_key=True)

    product: Mapped["Product"] = relationship(back_populates="tags")
    tag: Mapped["Tag"] = relationship()
