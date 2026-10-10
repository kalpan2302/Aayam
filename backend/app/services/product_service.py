from typing import Optional
from uuid import UUID

from sqlalchemy import func, select, text
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.product import Product

_SORT_COLUMNS = {
    "price_asc": "v.price ASC",
    "price_desc": "v.price DESC",
    "newest": "v.created_at DESC",
    "name": "v.name ASC",
}

_DIMENSION_FIELDS = ("width_mm", "height_mm", "depth_mm", "weight_grams")
_ASSET_FIELDS = (
    "model_url", "model_format", "file_size_bytes", "polygon_count",
    "asset_thumbnail_url", "anchor_x_mm", "anchor_y_mm", "anchor_z_mm",
)
_COMPATIBILITY_FIELDS = (
    "door_materials", "door_types", "opening_directions",
    "min_door_thickness_mm", "max_door_thickness_mm", "bore_diameter_mm", "backset_mm",
)


class ProductService:
    @staticmethod
    async def list_products(db: AsyncSession, filters: dict, page: int, page_size: int) -> tuple[list[dict], int]:
        where = ["1=1"]
        params: dict = {}

        if filters.get("category"):
            where.append("v.category_slug = :category")
            params["category"] = filters["category"]
        if filters.get("brand"):
            where.append("v.brand_slug = :brand")
            params["brand"] = filters["brand"]
        if filters.get("min_price") is not None:
            where.append("v.price >= :min_price")
            params["min_price"] = filters["min_price"]
        if filters.get("max_price") is not None:
            where.append("v.price <= :max_price")
            params["max_price"] = filters["max_price"]
        if filters.get("finish"):
            where.append("v.finish = :finish")
            params["finish"] = filters["finish"]
        if filters.get("search"):
            where.append("v.name ILIKE :search")
            params["search"] = f"%{filters['search']}%"

        # door_material / door_thickness_mm live on product_compatibilities, not v_product_list.
        join_compat = ""
        if filters.get("door_material") or filters.get("door_thickness_mm") is not None:
            join_compat = "JOIN product_compatibilities pc ON pc.product_id = v.id"
            if filters.get("door_material"):
                where.append("pc.door_materials @> jsonb_build_array(:door_material ::text)")
                params["door_material"] = filters["door_material"]
            if filters.get("door_thickness_mm") is not None:
                where.append(
                    "(pc.min_door_thickness_mm IS NULL OR pc.min_door_thickness_mm <= :door_thickness_mm) "
                    "AND (pc.max_door_thickness_mm IS NULL OR pc.max_door_thickness_mm >= :door_thickness_mm)"
                )
                params["door_thickness_mm"] = filters["door_thickness_mm"]

        order_by = _SORT_COLUMNS.get(filters.get("sort") or "", "v.created_at DESC")
        where_clause = " AND ".join(where)
        offset = (page - 1) * page_size

        total = (await db.execute(
            text(f"SELECT COUNT(*) FROM v_product_list v {join_compat} WHERE {where_clause}"), params
        )).scalar_one()
        result = await db.execute(
            text(
                f"SELECT v.* FROM v_product_list v {join_compat} WHERE {where_clause} "
                f"ORDER BY {order_by} LIMIT :limit OFFSET :offset"
            ),
            {**params, "limit": page_size, "offset": offset},
        )
        return [dict(row) for row in result.mappings().all()], total

    @staticmethod
    async def _fetch_detail(db: AsyncSession, where_sql: str, params: dict) -> Optional[dict]:
        row = (await db.execute(text(f"SELECT * FROM v_product_detail WHERE {where_sql}"), params)).mappings().first()
        if row is None:
            return None
        data = dict(row)

        has_dimensions = any(data.get(f) is not None for f in _DIMENSION_FIELDS)
        data["dimensions"] = {f: data.pop(f, None) for f in _DIMENSION_FIELDS} if has_dimensions else None
        if not has_dimensions:
            for f in _DIMENSION_FIELDS:
                data.pop(f, None)

        has_asset = data.get("model_url") is not None
        if has_asset:
            asset = {f: data.pop(f) for f in _ASSET_FIELDS}
            asset["thumbnail_url"] = asset.pop("asset_thumbnail_url")
            data["asset_3d"] = asset
        else:
            for f in _ASSET_FIELDS:
                data.pop(f, None)
            data["asset_3d"] = None

        has_compat = data.get("door_materials") is not None
        data["compatibility"] = {f: data.pop(f, None) for f in _COMPATIBILITY_FIELDS} if has_compat else None
        if not has_compat:
            for f in _COMPATIBILITY_FIELDS:
                data.pop(f, None)

        product_id = data["id"]
        images = (await db.execute(
            text(
                "SELECT id, image_url, alt_text, display_order, is_primary FROM product_images "
                "WHERE product_id = :pid ORDER BY display_order"
            ),
            {"pid": product_id},
        )).mappings().all()
        materials = (await db.execute(
            text("SELECT material_name, percentage FROM product_materials WHERE product_id = :pid"),
            {"pid": product_id},
        )).mappings().all()
        data["images"] = [dict(r) for r in images]
        data["materials"] = [dict(r) for r in materials]
        return data

    @staticmethod
    async def get_product(db: AsyncSession, product_id: UUID) -> Optional[dict]:
        return await ProductService._fetch_detail(db, "id = :id", {"id": product_id})

    @staticmethod
    async def get_product_by_sku(db: AsyncSession, sku: str) -> Optional[dict]:
        return await ProductService._fetch_detail(db, "sku = :sku", {"sku": sku})

    @staticmethod
    async def create_product(db: AsyncSession, data: dict) -> dict:
        product = Product(**data)
        db.add(product)
        await db.commit()
        created = await ProductService.get_product(db, product.id)
        assert created is not None, "just-inserted product must be readable back"
        return created

    @staticmethod
    async def update_product(db: AsyncSession, product_id: UUID, data: dict) -> Optional[dict]:
        result = await db.execute(select(Product).where(Product.id == product_id))
        product = result.scalar_one_or_none()
        if product is None:
            return None
        for key, value in data.items():
            setattr(product, key, value)
        await db.commit()
        return await ProductService.get_product(db, product_id)

    @staticmethod
    async def soft_delete_product(db: AsyncSession, product_id: UUID) -> bool:
        result = await db.execute(
            select(Product).where(Product.id == product_id, Product.deleted_at.is_(None))
        )
        product = result.scalar_one_or_none()
        if product is None:
            return False
        product.deleted_at = func.now()
        await db.commit()
        return True
