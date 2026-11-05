"""Phase 6 Step 6 - product material variants (runtime finish switching)

Revision ID: 005
Revises: 004
Create Date: 2026-10-29
"""
from typing import Sequence, Union
from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql

revision: str = '005'
down_revision: Union[str, None] = '004'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table('product_material_variants',
        sa.Column('id', postgresql.UUID(as_uuid=True), server_default=sa.text('gen_random_uuid()'), nullable=False),
        sa.Column('product_id', postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column('finish_name', sa.String(100), nullable=False),
        sa.Column('display_order', sa.Integer, server_default=sa.text('0'), nullable=False),
        sa.Column('base_color_hex', sa.String(9), nullable=False),
        sa.Column('metallic_factor', sa.DECIMAL(3, 2), nullable=False),
        sa.Column('roughness_factor', sa.DECIMAL(3, 2), nullable=False),
        sa.Column('model_url', sa.String, nullable=True),
        sa.Column('thumbnail_url', sa.String, nullable=True),
        sa.Column('is_default', sa.Boolean, server_default=sa.text('false'), nullable=False),
        sa.Column('created_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
        sa.ForeignKeyConstraint(['product_id'], ['products.id'], name='fk_material_variants_product', ondelete='CASCADE'),
        sa.PrimaryKeyConstraint('id'),
        sa.UniqueConstraint('product_id', 'finish_name', name='uq_material_variants_product_finish'),
        sa.CheckConstraint(
            'metallic_factor >= 0 AND metallic_factor <= 1',
            name='ck_material_variants_metallic_range',
        ),
        sa.CheckConstraint(
            'roughness_factor >= 0 AND roughness_factor <= 1',
            name='ck_material_variants_roughness_range',
        ),
    )
    op.create_index('ix_material_variants_product_id', 'product_material_variants', ['product_id'])


def downgrade() -> None:
    op.drop_index('ix_material_variants_product_id', table_name='product_material_variants')
    op.drop_table('product_material_variants')
