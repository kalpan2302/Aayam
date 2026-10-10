"""Views and materialized views

Revision ID: 003
Revises: 002
Create Date: 2026-10-06
"""
from typing import Sequence, Union
from alembic import op
import sqlalchemy as sa

revision: str = '003'
down_revision: Union[str, None] = '002'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

def upgrade() -> None:
    op.execute("""
    CREATE OR REPLACE VIEW v_product_list AS
    SELECT
        p.id, p.sku, p.name, p.price, p.mrp, p.currency, p.finish,
        p.mounting_type, p.is_featured, p.is_active, p.created_at,
        b.id AS brand_id, b.name AS brand_name, b.slug AS brand_slug,
        c.id AS category_id, c.name AS category_name, c.slug AS category_slug,
        COALESCE(a.thumbnail_url, p.thumbnail_url) AS thumbnail_url,
        a.model_url IS NOT NULL AS has_3d_model
    FROM products p
    JOIN brands b ON p.brand_id = b.id
    JOIN categories c ON p.category_id = c.id
    LEFT JOIN product_3d_assets a ON p.id = a.product_id AND a.lod_level = 0
    WHERE p.is_deleted = FALSE AND p.is_active = TRUE;
    """)

    op.execute("""
    CREATE OR REPLACE VIEW v_product_detail AS
    SELECT
        p.id, p.sku, p.name, p.description, p.price, p.mrp, p.currency,
        p.finish, p.mounting_type, p.is_featured, p.is_active,
        p.created_at, p.updated_at,
        b.id AS brand_id, b.name AS brand_name, b.slug AS brand_slug, b.logo_url AS brand_logo_url,
        c.id AS category_id, c.name AS category_name, c.slug AS category_slug,
        pd.width_mm, pd.height_mm, pd.depth_mm, pd.weight_grams,
        pc.door_materials, pc.door_types, pc.opening_directions,
        pc.min_door_thickness_mm, pc.max_door_thickness_mm,
        pc.bore_diameter_mm, pc.backset_mm,
        a.model_url, a.model_format, a.file_size_bytes, a.polygon_count,
        a.thumbnail_url AS asset_thumbnail_url,
        a.anchor_x_mm, a.anchor_y_mm, a.anchor_z_mm
    FROM products p
    JOIN brands b ON p.brand_id = b.id
    JOIN categories c ON p.category_id = c.id
    LEFT JOIN product_dimensions pd ON p.id = pd.product_id
    LEFT JOIN product_compatibilities pc ON p.id = pc.product_id
    LEFT JOIN product_3d_assets a ON p.id = a.product_id AND a.lod_level = 0
    WHERE p.is_deleted = FALSE;
    """)

    op.execute("CREATE MATERIALIZED VIEW mv_product_catalogue AS SELECT * FROM v_product_list WITH DATA;")
    op.execute("CREATE UNIQUE INDEX ix_mv_product_catalogue_id ON mv_product_catalogue (id);")
    op.execute("CREATE INDEX ix_mv_product_catalogue_category ON mv_product_catalogue (category_id, price);")
    op.execute("CREATE INDEX ix_mv_product_catalogue_brand ON mv_product_catalogue (brand_id, price);")

    op.execute("""
    CREATE OR REPLACE FUNCTION fn_refresh_product_catalogue()
    RETURNS VOID AS $$
    BEGIN
        REFRESH MATERIALIZED VIEW CONCURRENTLY mv_product_catalogue;
    END;
    $$ LANGUAGE plpgsql;
    """)

def downgrade() -> None:
    op.execute("DROP FUNCTION IF EXISTS fn_refresh_product_catalogue();")
    op.execute("DROP MATERIALIZED VIEW IF EXISTS mv_product_catalogue;")
    op.execute("DROP VIEW IF EXISTS v_product_detail;")
    op.execute("DROP VIEW IF EXISTS v_product_list;")