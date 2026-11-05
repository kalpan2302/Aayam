from uuid import UUID

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.exc import IntegrityError
from sqlalchemy.ext.asyncio import AsyncSession

from app.database import get_db
from app.schemas.material_variant import MaterialVariantCreate, MaterialVariantResponse
from app.api.deps import verify_admin_key
from app.services.material_variant_service import MaterialVariantService

router = APIRouter()


@router.get("/products/{product_id}/material-variants", response_model=list[MaterialVariantResponse])
async def list_material_variants(product_id: UUID, db: AsyncSession = Depends(get_db)):
    return await MaterialVariantService.list_variants_for_product(db, product_id)


@router.post(
    "/products/{product_id}/material-variants",
    response_model=MaterialVariantResponse,
    status_code=status.HTTP_201_CREATED,
    dependencies=[Depends(verify_admin_key)],
)
async def create_material_variant(product_id: UUID, body: MaterialVariantCreate, db: AsyncSession = Depends(get_db)):
    try:
        return await MaterialVariantService.create_variant(db, product_id, body.model_dump())
    except IntegrityError:
        await db.rollback()
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail=f"A material variant named '{body.finish_name}' already exists for this product.",
        )
