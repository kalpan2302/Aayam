from datetime import datetime, timezone
from typing import Optional
from uuid import UUID

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.measurement import Device, Door, DoorMeasurement


class MeasurementService:
    @staticmethod
    async def register_device(db: AsyncSession, device_uuid: str) -> Device:
        """Create-or-touch: returns the existing device row (bumping
        last_seen_at) if this device_uuid was seen before, otherwise
        creates a new one. Idempotent from the client's perspective -
        safe to call on every app launch."""
        result = await db.execute(select(Device).where(Device.device_uuid == device_uuid))
        device = result.scalar_one_or_none()
        if device is not None:
            device.last_seen_at = datetime.now(timezone.utc)
            await db.commit()
            await db.refresh(device)
            return device

        device = Device(device_uuid=device_uuid)
        db.add(device)
        await db.commit()
        await db.refresh(device)
        return device

    @staticmethod
    async def create_door(db: AsyncSession, device_id: UUID, room_label: Optional[str], project_label: Optional[str]) -> Door:
        door = Door(device_id=device_id, room_label=room_label, project_label=project_label)
        db.add(door)
        await db.commit()
        await db.refresh(door)
        return door

    @staticmethod
    async def list_doors_for_device(db: AsyncSession, device_id: UUID) -> list[Door]:
        result = await db.execute(
            select(Door).where(Door.device_id == device_id).order_by(Door.created_at.desc())
        )
        return list(result.scalars().all())

    @staticmethod
    async def get_door(db: AsyncSession, door_id: UUID) -> Optional[Door]:
        result = await db.execute(select(Door).where(Door.id == door_id))
        return result.scalar_one_or_none()

    @staticmethod
    async def save_measurement(db: AsyncSession, door_id: UUID, data: dict) -> DoorMeasurement:
        measurement = DoorMeasurement(door_id=door_id, **data)
        db.add(measurement)
        await db.commit()
        await db.refresh(measurement)
        return measurement

    @staticmethod
    async def list_measurements_for_door(db: AsyncSession, door_id: UUID) -> list[DoorMeasurement]:
        result = await db.execute(
            select(DoorMeasurement)
            .where(DoorMeasurement.door_id == door_id)
            .order_by(DoorMeasurement.created_at.desc())
        )
        return list(result.scalars().all())
