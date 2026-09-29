from fastapi import APIRouter
from app.api.v1 import products, brands, categories, assets

api_router = APIRouter()
api_router.include_router(products.router, prefix="/products", tags=["Products"])
api_router.include_router(brands.router, prefix="/brands", tags=["Brands"])
api_router.include_router(categories.router, prefix="/categories", tags=["Categories"])
api_router.include_router(assets.router, prefix="/assets", tags=["Assets"])
