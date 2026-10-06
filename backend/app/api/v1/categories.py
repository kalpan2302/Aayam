from fastapi import APIRouter

router = APIRouter()

@router.get("/")
async def list_categories():
    return []

@router.get("/{slug}/products")
async def list_category_products(slug: str):
    return []
