from sqlalchemy import String, Integer, Boolean, DECIMAL, DateTime, ForeignKey, UniqueConstraint, CheckConstraint, text
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import mapped_column, relationship

from app.database import Base


class ProductMaterialVariant(Base):
    """Phase 6 Step 6: a selectable finish for a product, swappable at
    runtime in AR without leaving the scene - distinct from ProductMaterial
    (material composition, e.g. "80% brass" for sourcing info, unrelated)
    and from Product.finish (the single finish a SKU ships with today)."""

    __tablename__ = "product_material_variants"
    __table_args__ = (
        UniqueConstraint("product_id", "finish_name", name="uq_material_variants_product_finish"),
        CheckConstraint("metallic_factor >= 0 AND metallic_factor <= 1", name="ck_material_variants_metallic_range"),
        CheckConstraint("roughness_factor >= 0 AND roughness_factor <= 1", name="ck_material_variants_roughness_range"),
    )

    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    product_id = mapped_column(UUID(as_uuid=True), ForeignKey("products.id", ondelete="CASCADE"), nullable=False)
    finish_name = mapped_column(String(100), nullable=False)
    display_order = mapped_column(Integer, server_default=text("0"), nullable=False)
    base_color_hex = mapped_column(String(9), nullable=False)
    metallic_factor = mapped_column(DECIMAL(3, 2), nullable=False)
    roughness_factor = mapped_column(DECIMAL(3, 2), nullable=False)
    model_url = mapped_column(String, nullable=True)
    thumbnail_url = mapped_column(String, nullable=True)
    is_default = mapped_column(Boolean, server_default=text("false"), nullable=False)
    created_at = mapped_column(DateTime(timezone=True), server_default=text("now()"), nullable=False)

    product = relationship("Product", back_populates="material_variants")
