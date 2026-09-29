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
