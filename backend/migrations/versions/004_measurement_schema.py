"""Phase 5 measurement schema - devices, doors, door_measurements

Revision ID: 004
Revises: 003
Create Date: 2026-10-29
"""
from typing import Sequence, Union
from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql

revision: str = '004'
down_revision: Union[str, None] = '003'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table('devices',
        sa.Column('id', postgresql.UUID(as_uuid=True), server_default=sa.text('gen_random_uuid()'), nullable=False),
        sa.Column('device_uuid', sa.String(64), nullable=False),
        sa.Column('created_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
        sa.Column('last_seen_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
        sa.PrimaryKeyConstraint('id'),
        sa.UniqueConstraint('device_uuid', name='uq_devices_device_uuid'),
    )

    op.create_table('doors',
        sa.Column('id', postgresql.UUID(as_uuid=True), server_default=sa.text('gen_random_uuid()'), nullable=False),
        sa.Column('device_id', postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column('room_label', sa.String(255), nullable=True),
        sa.Column('project_label', sa.String(255), nullable=True),
        sa.Column('created_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
        sa.ForeignKeyConstraint(['device_id'], ['devices.id'], name='fk_doors_device', ondelete='CASCADE'),
        sa.PrimaryKeyConstraint('id'),
    )
    op.create_index('ix_doors_device_id', 'doors', ['device_id'])

    op.create_table('door_measurements',
        sa.Column('id', postgresql.UUID(as_uuid=True), server_default=sa.text('gen_random_uuid()'), nullable=False),
        sa.Column('door_id', postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column('width_mm', sa.DECIMAL(8, 2), nullable=True),
        sa.Column('height_mm', sa.DECIMAL(8, 2), nullable=True),
        sa.Column('thickness_mm', sa.DECIMAL(6, 2), nullable=True),
        sa.Column('width_confidence', sa.DECIMAL(3, 2), nullable=True),
        sa.Column('height_confidence', sa.DECIMAL(3, 2), nullable=True),
        sa.Column('thickness_confidence', sa.DECIMAL(3, 2), nullable=True),
        sa.Column('source', sa.String(20), nullable=False),
        sa.Column('material', sa.String(100), nullable=True),
        sa.Column('opening_direction', sa.String(20), nullable=True),
        sa.Column('door_type', sa.String(50), nullable=True),
        sa.Column('existing_handle_height_mm', sa.DECIMAL(8, 2), nullable=True),
        sa.Column('existing_lock_height_mm', sa.DECIMAL(8, 2), nullable=True),
        sa.Column('existing_bore_diameter_mm', sa.DECIMAL(6, 2), nullable=True),
        sa.Column('created_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
        sa.ForeignKeyConstraint(['door_id'], ['doors.id'], name='fk_door_measurements_door', ondelete='CASCADE'),
        sa.PrimaryKeyConstraint('id'),
        sa.CheckConstraint(
            "source IN ('lidar', 'ar_depth', 'cv_monocular', 'user_assisted', 'manual_input')",
            name='ck_door_measurements_source',
        ),
        sa.CheckConstraint(
            'width_confidence IS NULL OR (width_confidence >= 0 AND width_confidence <= 1)',
            name='ck_door_measurements_width_confidence_range',
        ),
        sa.CheckConstraint(
            'height_confidence IS NULL OR (height_confidence >= 0 AND height_confidence <= 1)',
            name='ck_door_measurements_height_confidence_range',
        ),
        sa.CheckConstraint(
            'thickness_confidence IS NULL OR (thickness_confidence >= 0 AND thickness_confidence <= 1)',
            name='ck_door_measurements_thickness_confidence_range',
        ),
    )
    op.create_index('ix_door_measurements_door_id', 'door_measurements', ['door_id'])


def downgrade() -> None:
    op.drop_index('ix_door_measurements_door_id', table_name='door_measurements')
    op.drop_table('door_measurements')
    op.drop_index('ix_doors_device_id', table_name='doors')
    op.drop_table('doors')
    op.drop_table('devices')
