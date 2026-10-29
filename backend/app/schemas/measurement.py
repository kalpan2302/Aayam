"""
Pydantic schemas for Phase 5 measurement API - request/response models.
Mirrors app/schemas/product.py's *Create/*Response pairing and
ConfigDict(from_attributes=True) convention.
"""
from datetime import datetime
from decimal import Decimal
from typing import Optional, Literal
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field

# Matches DoorMeasurement.source's CHECK constraint exactly - keep both in
# sync if this list ever changes. Mirrors Unity's MeasurementSource enum
# (Models/CompatibilityResult.cs): LiDAR, ARDepth, CVMonocular,
# UserAssisted, ManualInput -> lowercase-snake on the wire.
MeasurementSource = Literal["lidar", "ar_depth", "cv_monocular", "user_assisted", "manual_input"]


# ─── Device ─────────────────────────────────────────────────────

class DeviceRegister(BaseModel):
    device_uuid: str = Field(min_length=8, max_length=64)


class DeviceResponse(BaseModel):
    id: UUID
    device_uuid: str
    created_at: datetime
    last_seen_at: datetime
    model_config = ConfigDict(from_attributes=True)


# ─── Door ───────────────────────────────────────────────────────

class DoorCreate(BaseModel):
    room_label: Optional[str] = Field(None, max_length=255)
    project_label: Optional[str] = Field(None, max_length=255)


class DoorResponse(BaseModel):
    id: UUID
    device_id: UUID
    room_label: Optional[str] = None
    project_label: Optional[str] = None
    created_at: datetime
    model_config = ConfigDict(from_attributes=True)


# ─── Door Measurement ───────────────────────────────────────────

class DoorMeasurementCreate(BaseModel):
    width_mm: Optional[Decimal] = Field(None, gt=0)
    height_mm: Optional[Decimal] = Field(None, gt=0)
    thickness_mm: Optional[Decimal] = Field(None, gt=0)
    width_confidence: Optional[Decimal] = Field(None, ge=0, le=1)
    height_confidence: Optional[Decimal] = Field(None, ge=0, le=1)
    thickness_confidence: Optional[Decimal] = Field(None, ge=0, le=1)
    source: MeasurementSource
    material: Optional[str] = Field(None, max_length=100)
    opening_direction: Optional[str] = Field(None, max_length=20)
    door_type: Optional[str] = Field(None, max_length=50)
    existing_handle_height_mm: Optional[Decimal] = Field(None, gt=0)
    existing_lock_height_mm: Optional[Decimal] = Field(None, gt=0)
    existing_bore_diameter_mm: Optional[Decimal] = Field(None, gt=0)


class DoorMeasurementResponse(BaseModel):
    id: UUID
    door_id: UUID
    width_mm: Optional[Decimal] = None
    height_mm: Optional[Decimal] = None
    thickness_mm: Optional[Decimal] = None
    width_confidence: Optional[Decimal] = None
    height_confidence: Optional[Decimal] = None
    thickness_confidence: Optional[Decimal] = None
    source: str
    material: Optional[str] = None
    opening_direction: Optional[str] = None
    door_type: Optional[str] = None
    existing_handle_height_mm: Optional[Decimal] = None
    existing_lock_height_mm: Optional[Decimal] = None
    existing_bore_diameter_mm: Optional[Decimal] = None
    created_at: datetime
    model_config = ConfigDict(from_attributes=True)
