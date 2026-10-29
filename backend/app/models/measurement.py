"""
ORM Models for Phase 5 measurement storage - matches migration 004 exactly.

Design decisions (mirrors app/models/product.py's own):
  - UUID PKs on all tables (API-safe, merge-safe across environments)
  - Lightweight device identity (client-generated UUID), NOT a real user/
    auth system - see docs/phases/phase-4-implementation-plan.md's Phase 5
    plan for why: no auth system exists in this backend yet at all, and
    building one is a separate, larger, security-critical project.
  - room_label/project_label on `doors` are free-text, not normalized
    Room/Project tables - nothing yet needs them to be reusable/shared
    entities (YAGNI over the roadmap's literal "per door/room/project"
    wording).
  - `door_measurements` is append-only history (multiple rows per door),
    not a single overwritten "current measurement" - that's the whole
    point of Phase 5 Step 7.
  - DECIMAL for all measurements (mm) and confidences (0-1), matching
    product.py's own precision conventions.
"""
from sqlalchemy import (
    String, ForeignKey, DECIMAL, DateTime, CheckConstraint, text,
)
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import mapped_column, relationship

from app.database import Base


class Device(Base):
    __tablename__ = "devices"

    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    device_uuid = mapped_column(String(64), unique=True, nullable=False)
    created_at = mapped_column(DateTime(timezone=True), server_default=text("now()"), nullable=False)
    last_seen_at = mapped_column(DateTime(timezone=True), server_default=text("now()"), nullable=False)

    doors = relationship("Door", back_populates="device", cascade="all, delete-orphan")


class Door(Base):
    __tablename__ = "doors"

    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    device_id = mapped_column(UUID(as_uuid=True), ForeignKey("devices.id", ondelete="CASCADE"), nullable=False)
    room_label = mapped_column(String(255), nullable=True)
    project_label = mapped_column(String(255), nullable=True)
    created_at = mapped_column(DateTime(timezone=True), server_default=text("now()"), nullable=False)

    device = relationship("Device", back_populates="doors")
    measurements = relationship("DoorMeasurement", back_populates="door", cascade="all, delete-orphan")


class DoorMeasurement(Base):
    __tablename__ = "door_measurements"
    __table_args__ = (
        CheckConstraint(
            "source IN ('lidar', 'ar_depth', 'cv_monocular', 'user_assisted', 'manual_input')",
            name="ck_door_measurements_source",
        ),
        CheckConstraint(
            "width_confidence IS NULL OR (width_confidence >= 0 AND width_confidence <= 1)",
            name="ck_door_measurements_width_confidence_range",
        ),
        CheckConstraint(
            "height_confidence IS NULL OR (height_confidence >= 0 AND height_confidence <= 1)",
            name="ck_door_measurements_height_confidence_range",
        ),
        CheckConstraint(
            "thickness_confidence IS NULL OR (thickness_confidence >= 0 AND thickness_confidence <= 1)",
            name="ck_door_measurements_thickness_confidence_range",
        ),
    )

    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    door_id = mapped_column(UUID(as_uuid=True), ForeignKey("doors.id", ondelete="CASCADE"), nullable=False)

    width_mm = mapped_column(DECIMAL(8, 2), nullable=True)
    height_mm = mapped_column(DECIMAL(8, 2), nullable=True)
    thickness_mm = mapped_column(DECIMAL(6, 2), nullable=True)

    width_confidence = mapped_column(DECIMAL(3, 2), nullable=True)
    height_confidence = mapped_column(DECIMAL(3, 2), nullable=True)
    thickness_confidence = mapped_column(DECIMAL(3, 2), nullable=True)

    source = mapped_column(String(20), nullable=False)

    material = mapped_column(String(100), nullable=True)
    opening_direction = mapped_column(String(20), nullable=True)
    door_type = mapped_column(String(50), nullable=True)

    existing_handle_height_mm = mapped_column(DECIMAL(8, 2), nullable=True)
    existing_lock_height_mm = mapped_column(DECIMAL(8, 2), nullable=True)
    existing_bore_diameter_mm = mapped_column(DECIMAL(6, 2), nullable=True)

    created_at = mapped_column(DateTime(timezone=True), server_default=text("now()"), nullable=False)

    door = relationship("Door", back_populates="measurements")
