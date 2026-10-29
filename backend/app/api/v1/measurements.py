from uuid import UUID

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.ext.asyncio import AsyncSession

from app.database import get_db
from app.api.deps import verify_api_key
from app.schemas.measurement import (
    DeviceRegister, DeviceResponse,
    DoorCreate, DoorResponse,
    DoorMeasurementCreate, DoorMeasurementResponse,
)
from app.services.measurement_service import MeasurementService

router = APIRouter()

# All routes here are gated by verify_api_key (app/api/deps.py) - this is
# its first real use anywhere in this backend. Not a user-auth system:
# device_id scoping (passed explicitly, see each route below) is what
# separates one installation's doors/measurements from another's, not the
# API key, which just gates "is this a legitimate app client" the same way
# verify_admin_key already gates catalog writes.


@router.post("/devices/register", response_model=DeviceResponse, dependencies=[Depends(verify_api_key)])
async def register_device(body: DeviceRegister, db: AsyncSession = Depends(get_db)):
    device = await MeasurementService.register_device(db, body.device_uuid)
    return DeviceResponse.model_validate(device)


@router.post("/doors/", response_model=DoorResponse, status_code=status.HTTP_201_CREATED, dependencies=[Depends(verify_api_key)])
async def create_door(device_id: UUID, body: DoorCreate, db: AsyncSession = Depends(get_db)):
    door = await MeasurementService.create_door(db, device_id, body.room_label, body.project_label)
    return DoorResponse.model_validate(door)


@router.get("/doors/", response_model=list[DoorResponse], dependencies=[Depends(verify_api_key)])
async def list_doors(device_id: UUID, db: AsyncSession = Depends(get_db)):
    doors = await MeasurementService.list_doors_for_device(db, device_id)
    return [DoorResponse.model_validate(d) for d in doors]


@router.post(
    "/doors/{door_id}/measurements",
    response_model=DoorMeasurementResponse,
    status_code=status.HTTP_201_CREATED,
    dependencies=[Depends(verify_api_key)],
)
async def save_measurement(door_id: UUID, body: DoorMeasurementCreate, db: AsyncSession = Depends(get_db)):
    door = await MeasurementService.get_door(db, door_id)
    if door is None:
        raise HTTPException(status_code=404, detail="Door not found")
    measurement = await MeasurementService.save_measurement(db, door_id, body.model_dump(exclude_unset=True))
    return DoorMeasurementResponse.model_validate(measurement)


@router.get(
    "/doors/{door_id}/measurements",
    response_model=list[DoorMeasurementResponse],
    dependencies=[Depends(verify_api_key)],
)
async def list_measurements(door_id: UUID, db: AsyncSession = Depends(get_db)):
    door = await MeasurementService.get_door(db, door_id)
    if door is None:
        raise HTTPException(status_code=404, detail="Door not found")
    measurements = await MeasurementService.list_measurements_for_door(db, door_id)
    return [DoorMeasurementResponse.model_validate(m) for m in measurements]
