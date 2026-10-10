"""Triggers, functions, and partitioned audit_log

Revision ID: 002
Revises: 001
Create Date: 2026-10-06
"""
from typing import Sequence, Union
from alembic import op
import sqlalchemy as sa

revision: str = '002'
down_revision: Union[str, None] = '001'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

def upgrade() -> None:
    # 1. Auto updated_at
    op.execute("""
    CREATE OR REPLACE FUNCTION fn_set_updated_at()
    RETURNS TRIGGER AS $$
    BEGIN
        NEW.updated_at = NOW();
        RETURN NEW;
    END;
    $$ LANGUAGE plpgsql;
    """)
    op.execute("CREATE TRIGGER trg_brands_updated_at BEFORE UPDATE ON brands FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();")
    op.execute("CREATE TRIGGER trg_products_updated_at BEFORE UPDATE ON products FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();")

    # 2. Partitioned audit_log
    op.execute("""
    CREATE TABLE audit_log (
        id UUID DEFAULT gen_random_uuid(),
        table_name TEXT NOT NULL,
        record_id UUID NOT NULL,
        action TEXT NOT NULL CHECK (action IN ('INSERT', 'UPDATE', 'DELETE')),
        old_data JSONB,
        new_data JSONB,
        changed_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL,
        changed_by UUID
    ) PARTITION BY RANGE (changed_at);
    """)
    op.execute("CREATE TABLE audit_log_2026_10 PARTITION OF audit_log FOR VALUES FROM ('2026-10-01') TO ('2026-11-01');")
    op.execute("CREATE TABLE audit_log_2026_11 PARTITION OF audit_log FOR VALUES FROM ('2026-11-01') TO ('2026-12-01');")
    op.execute("CREATE TABLE audit_log_2026_12 PARTITION OF audit_log FOR VALUES FROM ('2026-12-01') TO ('2027-01-01');")
    op.execute("CREATE TABLE audit_log_2027_01 PARTITION OF audit_log FOR VALUES FROM ('2027-01-01') TO ('2027-02-01');")
    op.execute("CREATE TABLE audit_log_default PARTITION OF audit_log DEFAULT;")
    op.execute("CREATE INDEX ix_audit_log_table_record ON audit_log (table_name, record_id, changed_at DESC);")

    # 3. Audit trigger
    op.execute("""
    CREATE OR REPLACE FUNCTION fn_audit_trigger()
    RETURNS TRIGGER AS $$
    BEGIN
        IF TG_OP = 'INSERT' THEN
            INSERT INTO audit_log (table_name, record_id, action, new_data)
            VALUES (TG_TABLE_NAME, NEW.id, TG_OP, to_jsonb(NEW));
            RETURN NEW;
        ELSIF TG_OP = 'UPDATE' THEN
            INSERT INTO audit_log (table_name, record_id, action, old_data, new_data)
            VALUES (TG_TABLE_NAME, NEW.id, TG_OP, to_jsonb(OLD), to_jsonb(NEW));
            RETURN NEW;
        ELSIF TG_OP = 'DELETE' THEN
            INSERT INTO audit_log (table_name, record_id, action, old_data)
            VALUES (TG_TABLE_NAME, OLD.id, TG_OP, to_jsonb(OLD));
            RETURN OLD;
        END IF;
    END;
    $$ LANGUAGE plpgsql;
    """)
    op.execute("CREATE TRIGGER trg_products_audit AFTER INSERT OR UPDATE OR DELETE ON products FOR EACH ROW EXECUTE FUNCTION fn_audit_trigger();")
    op.execute("CREATE TRIGGER trg_product_3d_assets_audit AFTER INSERT OR UPDATE OR DELETE ON product_3d_assets FOR EACH ROW EXECUTE FUNCTION fn_audit_trigger();")
    op.execute("CREATE TRIGGER trg_product_price_history_audit AFTER INSERT OR UPDATE OR DELETE ON product_price_history FOR EACH ROW EXECUTE FUNCTION fn_audit_trigger();")

    # 4. Single primary image
    op.execute("""
    CREATE OR REPLACE FUNCTION fn_enforce_single_primary_image()
    RETURNS TRIGGER AS $$
    BEGIN
        IF NEW.is_primary = TRUE THEN
            UPDATE product_images SET is_primary = FALSE
            WHERE product_id = NEW.product_id AND id != NEW.id AND is_primary = TRUE;
        END IF;
        RETURN NEW;
    END;
    $$ LANGUAGE plpgsql;
    """)
    op.execute("CREATE TRIGGER trg_enforce_single_primary_image BEFORE INSERT OR UPDATE OF is_primary ON product_images FOR EACH ROW WHEN (NEW.is_primary = TRUE) EXECUTE FUNCTION fn_enforce_single_primary_image();")

    # 5. Category depth
    op.execute("""
    CREATE OR REPLACE FUNCTION fn_set_category_depth()
    RETURNS TRIGGER AS $$
    DECLARE parent_depth INT;
    BEGIN
        IF NEW.parent_id IS NULL THEN
            NEW.depth = 0;
        ELSE
            SELECT depth INTO parent_depth FROM categories WHERE id = NEW.parent_id;
            IF NOT FOUND THEN
                RAISE EXCEPTION 'Parent category does not exist';
            END IF;
            NEW.depth = parent_depth + 1;
            IF NEW.depth > 5 THEN
                RAISE EXCEPTION 'Category nesting depth exceeds maximum of 5';
            END IF;
        END IF;
        RETURN NEW;
    END;
    $$ LANGUAGE plpgsql;
    """)
    op.execute("CREATE TRIGGER trg_set_category_depth BEFORE INSERT OR UPDATE OF parent_id ON categories FOR EACH ROW EXECUTE FUNCTION fn_set_category_depth();")

    # 6. Price change history
    op.execute("""
    CREATE OR REPLACE FUNCTION fn_record_price_change()
    RETURNS TRIGGER AS $$
    BEGIN
        IF NEW.price IS DISTINCT FROM OLD.price THEN
            INSERT INTO product_price_history (product_id, old_price, new_price, changed_at)
            VALUES (NEW.id, OLD.price, NEW.price, NOW());
        END IF;
        RETURN NEW;
    END;
    $$ LANGUAGE plpgsql;
    """)
    op.execute("CREATE TRIGGER trg_record_price_change AFTER UPDATE OF price ON products FOR EACH ROW EXECUTE FUNCTION fn_record_price_change();")

    # 7. Soft delete cascade
    op.execute("""
    CREATE OR REPLACE FUNCTION fn_soft_delete_product()
    RETURNS TRIGGER AS $$
    BEGIN
        IF NEW.deleted_at IS NOT NULL AND OLD.deleted_at IS NULL THEN
            NEW.is_active = FALSE;
            NEW.is_deleted = TRUE;
        END IF;
        RETURN NEW;
    END;
    $$ LANGUAGE plpgsql;
    """)
    op.execute("CREATE TRIGGER trg_soft_delete_product BEFORE UPDATE OF deleted_at ON products FOR EACH ROW EXECUTE FUNCTION fn_soft_delete_product();")

def downgrade() -> None:
    op.execute("DROP TRIGGER IF EXISTS trg_soft_delete_product ON products;")
    op.execute("DROP FUNCTION IF EXISTS fn_soft_delete_product();")
    op.execute("DROP TRIGGER IF EXISTS trg_record_price_change ON products;")
    op.execute("DROP FUNCTION IF EXISTS fn_record_price_change();")
    op.execute("DROP TRIGGER IF EXISTS trg_set_category_depth ON categories;")
    op.execute("DROP FUNCTION IF EXISTS fn_set_category_depth();")
    op.execute("DROP TRIGGER IF EXISTS trg_enforce_single_primary_image ON product_images;")
    op.execute("DROP FUNCTION IF EXISTS fn_enforce_single_primary_image();")
    op.execute("DROP TRIGGER IF EXISTS trg_product_price_history_audit ON product_price_history;")
    op.execute("DROP TRIGGER IF EXISTS trg_product_3d_assets_audit ON product_3d_assets;")
    op.execute("DROP TRIGGER IF EXISTS trg_products_audit ON products;")
    op.execute("DROP FUNCTION IF EXISTS fn_audit_trigger();")
    op.execute("DROP TABLE IF EXISTS audit_log CASCADE;")
    op.execute("DROP TRIGGER IF EXISTS trg_products_updated_at ON products;")
    op.execute("DROP TRIGGER IF EXISTS trg_brands_updated_at ON brands;")
    op.execute("DROP FUNCTION IF EXISTS fn_set_updated_at();")