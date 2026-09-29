from fastapi import APIRouter

router = APIRouter()

@router.get("/")
async def list_brands():
    return []

@router.get("/{slug}")
async def get_brand(slug: str):
    return {}

@router.get("/{slug}/products")
async def list_brand_products(slug: str):
    return []
