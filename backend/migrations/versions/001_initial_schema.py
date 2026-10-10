"""Initial schema - all tables, constraints, and indexes

Revision ID: 001
Revises:
Create Date: 2026-10-06
"""
from typing import Sequence, Union
from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql

revision: str = '001'
down_revision: Union[str, None] = None
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

def upgrade() -> None:
    op.execute('CREATE EXTENSION IF NOT EXISTS "uuid-ossp"')
    op.execute('CREATE EXTENSION IF NOT EXISTS "pgcrypto"')
    op.execute('CREATE EXTENSION IF NOT EXISTS "pg_trgm"')
    op.execute('CREATE EXTENSION IF NOT EXISTS "btree_gin"')

    op.create_table('brands',
        sa.Column('id', postgresql.UUID(as_uuid=True), server_default=sa.text('gen_random_uuid()'), nullable=False),
        sa.Column('name', sa.String(255), nullable=False),
        sa.Column('slug', sa.String(255), nullable=False),
        sa.Column('description', sa.String(), nullable=True),
        sa.Column('logo_url', sa.String(), nullable=True),
        sa.Column('country', sa.String(100), nullable=True),
        sa.Column('is_active', sa.Boolean(), server_default=sa.text('true'), nullable=False),
        sa.Column('created_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
        sa.Column('updated_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
        sa.PrimaryKeyConstraint('id'),
        sa.UniqueConstraint('slug', name='uq_brands_slug'),
        sa.UniqueConstraint('name', name='uq_brands_name'),
    )

    op.create_table('categories',
        sa.Column('id', postgresql.UUID(as_uuid=True), server_default=sa.text('gen_random_uuid()'), nullable=False),
        sa.Column('name', sa.String(255), nullable=False),
        sa.Column('slug', sa.String(255), nullable=False),
        sa.Column('icon', sa.String(), nullable=True),
        sa.Column('parent_id', postgresql.UUID(as_uuid=True), nullable=True),
        sa.Column('display_order', sa.Integer(), server_default=sa.text('0'), nullable=False),
        sa.Column('depth', sa.Integer(), server_default=sa.text('0'), nullable=False),
        sa.ForeignKeyConstraint(['parent_id'], ['categories.id'], name='fk_categories_parent', ondelete='SET NULL'),
        sa.PrimaryKeyConstraint('id'),
        sa.UniqueConstraint('slug', name='uq_categories_slug'),
        sa.CheckConstraint('depth >= 0 AND depth <= 5', name='ck_categories_depth'),
    )

    op.create_table('products',
        sa.Column('id', postgresql.UUID(as_uuid=True), server_default=sa.text('gen_random_uuid()'), nullable=False),
        sa.Column('sku', sa.String(100), nullable=False),
        sa.Column('name', sa.String(255), nullable=False),
        sa.Column('description', sa.Text(), nullable=True),
        sa.Column('brand_id', postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column('category_id', postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column('price', sa.DECIMAL(10, 2), nullable=False),
        sa.Column('mrp', sa.DECIMAL(10, 2), nullable=True),
        sa.Column('currency', sa.String(3), server_default=sa.text("'INR'"), nullable=False),
        sa.Column('finish', sa.String(100), nullable=True),
        sa.Column('mounting_type', sa.String(50), nullable=True),
        sa.Column('thumbnail_url', sa.String(), nullable=True),
        sa.Column('is_featured', sa.Boolean(), server_default=sa.text('false'), nullable=False),
        sa.Column('is_active', sa.Boolean(), server_default=sa.text('true'), nullable=False),
        sa.Column('is_deleted', sa.Boolean(), server_default=sa.text('false'), nullable=False),
        sa.Column('created_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
        sa.Column('updated_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
        sa.Column('deleted_at', sa.DateTime(timezone=True), nullable=True),
        sa.ForeignKeyConstraint(['brand_id'], ['brands.id'], name='fk_products_brand', ondelete='RESTRICT'),
        sa.ForeignKeyConstraint(['category_id'], ['categories.id'], name='fk_products_category', ondelete='RESTRICT'),
        sa.PrimaryKeyConstraint('id'),
        sa.UniqueConstraint('sku', name='uq_products_sku'),
        sa.CheckConstraint('price > 0', name='ck_products_price_positive'),
        sa.CheckConstraint('mrp IS NULL OR mrp >= price', name='ck_products_mrp_gte_price'),
        sa.CheckConstraint("deleted_at IS NULL OR is_active = false", name='ck_products_deleted_inactive'),
    )

    op.create_table('product_dimensions',
        sa.Column('id', postgresql.UUID(as_uuid=True), server_default=sa.text('gen_random_uuid()'), nullable=False),
        sa.Column('product_id', postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column('width_mm', sa.DECIMAL(8, 2), nullable=True),
        sa.Column('height_mm', sa.DECIMAL(8, 2), nullable=True),
        sa.Column('depth_mm', sa.DECIMAL(8, 2), nullable=True),
        sa.Column('weight_grams', sa.DECIMAL(8, 2), nullable=True),
        sa.ForeignKeyConstraint(['product_id'], ['products.id'], name='fk_product_dimensions_product', ondelete='CASCADE'),
        sa.PrimaryKeyConstraint('id'),
        sa.UniqueConstraint('product_id', name='uq_product_dimensions_product'),
        sa.CheckConstraint('width_mm IS NULL OR width_mm > 0', name='ck_dimensions_width_positive'),
        sa.CheckConstraint('height_mm IS NULL OR height_mm > 0', name='ck_dimensions_height_positive'),
        sa.CheckConstraint('depth_mm IS NULL OR depth_mm > 0', name='ck_dimensions_depth_positive'),
    )

    op.create_table('product_compatibilities',
        sa.Column('id', postgresql.UUID(as_uuid=True), server_default=sa.text('gen_random_uuid()'), nullable=False),
        sa.Column('product_id', postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column('door_materials', postgresql.JSONB(), server_default=sa.text("'[]'::jsonb"), nullable=False),
        sa.Column('door_types', postgresql.JSONB(), server_default=sa.text("'[]'::jsonb"), nullable=False),
        sa.Column('opening_directions', postgresql.JSONB(), server_default=sa.text("'[]'::jsonb"), nullable=False),
        sa.Column('min_door_thickness_mm', sa.DECIMAL(6, 2), nullable=True),
        sa.Column('max_door_thickness_mm', sa.DECIMAL(6, 2), nullable=True),
        sa.Column('bore_diameter_mm', sa.DECIMAL(6, 2), nullable=True),
        sa.Column('backset_mm', sa.DECIMAL(6, 2), nullable=True),
        sa.ForeignKeyConstraint(['product_id'], ['products.id'], name='fk_product_compatibilities_product', ondelete='CASCADE'),
        sa.PrimaryKeyConstraint('id'),
        sa.UniqueConstraint('product_id', name='uq_product_compatibilities_product'),
        sa.CheckConstraint('min_door_thickness_mm IS NULL OR max_door_thickness_mm IS NULL OR min_door_thickness_mm <= max_door_thickness_mm', name='ck_compat_thickness_range'),
    )

    op.create_table('product_3d_assets',
        sa.Column('id', postgresql.UUID(as_uuid=True), server_default=sa.text('gen_random_uuid()'), nullable=False),
        sa.Column('product_id', postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column('model_url', sa.String(), nullable=False),
        sa.Column('model_format', sa.String(20), nullable=False),
        sa.Column('file_size_bytes', sa.Integer(), nullable=False),
        sa.Column('polygon_count', sa.Integer(), nullable=False),
        sa.Column('thumbnail_url', sa.String(), nullable=True),
        sa.Column('anchor_x_mm', sa.DECIMAL(8, 2), server_default=sa.text('0'), nullable=False),
        sa.Column('anchor_y_mm', sa.DECIMAL(8, 2), server_default=sa.text('0'), nullable=False),
        sa.Column('anchor_z_mm', sa.DECIMAL(8, 2), server_default=sa.text('0'), nullable=False),
        sa.Column('lod_level', sa.Integer(), server_default=sa.text('0'), nullable=False),
        sa.Column('created_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
        sa.ForeignKeyConstraint(['product_id'], ['products.id'], name='fk_product_3d_assets_product', ondelete='CASCADE'),
        sa.PrimaryKeyConstraint('id'),
        sa.UniqueConstraint('product_id', 'lod_level', name='uq_product_3d_assets_product_lod'),
        sa.CheckConstraint('file_size_bytes > 0', name='ck_3d_assets_filesize'),
        sa.CheckConstraint('polygon_count > 0', name='ck_3d_assets_polygons'),
        sa.CheckConstraint("model_format IN ('glb', 'gltf', 'usdz', 'fbx', 'obj')", name='ck_3d_assets_format'),
    )

    op.create_table('product_images',
        sa.Column('id', postgresql.UUID(as_uuid=True), server_default=sa.text('gen_random_uuid()'), nullable=False),
        sa.Column('product_id', postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column('image_url', sa.String(), nullable=False),
        sa.Column('alt_text', sa.String(255), nullable=True),
        sa.Column('display_order', sa.Integer(), server_default=sa.text('0'), nullable=False),
        sa.Column('is_primary', sa.Boolean(), server_default=sa.text('false'), nullable=False),
        sa.ForeignKeyConstraint(['product_id'], ['products.id'], name='fk_product_images_product', ondelete='CASCADE'),
        sa.PrimaryKeyConstraint('id'),
    )

    op.create_table('product_materials',
        sa.Column('product_id', postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column('material_name', sa.String(100), nullable=False),
        sa.Column('percentage', sa.DECIMAL(5, 2), nullable=True),
        sa.ForeignKeyConstraint(['product_id'], ['products.id'], name='fk_product_materials_product', ondelete='CASCADE'),
        sa.PrimaryKeyConstraint('product_id', 'material_name', name='pk_product_materials'),
        sa.CheckConstraint('percentage IS NULL OR (percentage > 0 AND percentage <= 100)', name='ck_materials_percentage'),
    )

    op.create_table('product_price_history',
        sa.Column('id', postgresql.UUID(as_uuid=True), server_default=sa.text('gen_random_uuid()'), nullable=False),
        sa.Column('product_id', postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column('old_price', sa.DECIMAL(10, 2), nullable=True),
        sa.Column('new_price', sa.DECIMAL(10, 2), nullable=False),
        sa.Column('changed_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
        sa.ForeignKeyConstraint(['product_id'], ['products.id'], name='fk_product_price_history_product', ondelete='CASCADE'),
        sa.PrimaryKeyConstraint('id'),
        sa.CheckConstraint('new_price > 0', name='ck_price_history_positive'),
    )

    op.create_table('product_attributes',
        sa.Column('id', postgresql.UUID(as_uuid=True), server_default=sa.text('gen_random_uuid()'), nullable=False),
        sa.Column('product_id', postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column('key', sa.String(100), nullable=False),
        sa.Column('value', sa.Text(), nullable=False),
        sa.ForeignKeyConstraint(['product_id'], ['products.id'], name='fk_product_attributes_product', ondelete='CASCADE'),
        sa.PrimaryKeyConstraint('id'),
        sa.UniqueConstraint('product_id', 'key', name='uq_product_attributes_product_key'),
    )

    op.create_table('tags',
        sa.Column('id', postgresql.UUID(as_uuid=True), server_default=sa.text('gen_random_uuid()'), nullable=False),
        sa.Column('name', sa.String(100), nullable=False),
        sa.PrimaryKeyConstraint('id'),
        sa.UniqueConstraint('name', name='uq_tags_name'),
    )

    op.create_table('product_tags',
        sa.Column('product_id', postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column('tag_id', postgresql.UUID(as_uuid=True), nullable=False),
        sa.ForeignKeyConstraint(['product_id'], ['products.id'], name='fk_product_tags_product', ondelete='CASCADE'),
        sa.ForeignKeyConstraint(['tag_id'], ['tags.id'], name='fk_product_tags_tag', ondelete='CASCADE'),
        sa.PrimaryKeyConstraint('product_id', 'tag_id', name='pk_product_tags'),
    )

    # Indexes
    op.create_index('ix_brands_name_trgm', 'brands', ['name'], postgresql_using='gin', postgresql_ops={'name': 'gin_trgm_ops'})
    op.create_index('ix_products_name_trgm', 'products', ['name'], postgresql_using='gin', postgresql_ops={'name': 'gin_trgm_ops'})
    op.create_index('ix_compat_door_materials_gin', 'product_compatibilities', ['door_materials'], postgresql_using='gin')
    op.create_index('ix_compat_door_types_gin', 'product_compatibilities', ['door_types'], postgresql_using='gin')
    op.execute("CREATE INDEX ix_products_active ON products (brand_id, category_id, price) WHERE is_deleted = false AND is_active = true")
    op.execute("CREATE INDEX ix_products_list_covering ON products (category_id, price) INCLUDE (id, sku, name, brand_id, finish, is_featured) WHERE is_deleted = false")
    op.execute("CREATE INDEX ix_products_featured ON products (created_at DESC) WHERE is_featured = true AND is_deleted = false")
    op.create_index('ix_products_brand_id', 'products', ['brand_id'])
    op.create_index('ix_products_category_id', 'products', ['category_id'])
    op.create_index('ix_products_sku', 'products', ['sku'])
    op.create_index('ix_product_images_product_id', 'product_images', ['product_id'])
    op.create_index('ix_product_attributes_product_key', 'product_attributes', ['product_id', 'key'])
    op.create_index('ix_price_history_product_date', 'product_price_history', ['product_id', 'changed_at'])
    op.create_index('ix_categories_parent_id', 'categories', ['parent_id'])

def downgrade() -> None:
    op.drop_index('ix_categories_parent_id', table_name='categories')
    op.drop_index('ix_price_history_product_date', table_name='product_price_history')
    op.drop_index('ix_product_attributes_product_key', table_name='product_attributes')
    op.drop_index('ix_product_images_product_id', table_name='product_images')
    op.drop_index('ix_products_sku', table_name='products')
    op.drop_index('ix_products_category_id', table_name='products')
    op.drop_index('ix_products_brand_id', table_name='products')
    op.execute('DROP INDEX IF EXISTS ix_products_featured')
    op.execute('DROP INDEX IF EXISTS ix_products_list_covering')
    op.execute('DROP INDEX IF EXISTS ix_products_active')
    op.drop_index('ix_compat_door_types_gin', table_name='product_compatibilities')
    op.drop_index('ix_compat_door_materials_gin', table_name='product_compatibilities')
    op.drop_index('ix_products_name_trgm', table_name='products')
    op.drop_index('ix_brands_name_trgm', table_name='brands')
    op.drop_table('product_tags')
    op.drop_table('tags')
    op.drop_table('product_attributes')
    op.drop_table('product_price_history')
    op.drop_table('product_materials')
    op.drop_table('product_images')
    op.drop_table('product_3d_assets')
    op.drop_table('product_compatibilities')
    op.drop_table('product_dimensions')
    op.drop_table('products')
    op.drop_table('categories')
    op.drop_table('brands')
    op.execute('DROP EXTENSION IF EXISTS "btree_gin"')
    op.execute('DROP EXTENSION IF EXISTS "pg_trgm"')
    op.execute('DROP EXTENSION IF EXISTS "pgcrypto"')
    op.execute('DROP EXTENSION IF EXISTS "uuid-ossp"')