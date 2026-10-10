"""
Seed the database with initial brands, categories, and 20 products.
Run: python -m scripts.seed_data
"""
import asyncio
import sys
from decimal import Decimal
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession
from app.database import async_session_maker, engine
from app.models import (
    Brand, Category, Product, ProductDimension,
    ProductCompatibility, Product3DAsset, ProductImage, ProductMaterial,
)

BRANDS = [
    {"name": "Godrej", "slug": "godrej", "country": "India", "description": "India's largest lock manufacturer."},
    {"name": "Yale", "slug": "yale", "country": "USA", "description": "Global leader in smart locks."},
    {"name": "Dorset", "slug": "dorset", "country": "India", "description": "Premium architectural hardware."},
    {"name": "Ozone", "slug": "ozone", "country": "India", "description": "Contemporary hardware fittings."},
    {"name": "Ebco", "slug": "ebco", "country": "India", "description": "Furniture and kitchen hardware."},
    {"name": "Dorma", "slug": "dorma", "country": "Germany", "description": "Door closers and access control."},
]

PRODUCTS = [
    ("HANDLE_001","Premium Matte Black Lever Handle","godrej","lever-handle",1499,1999,"matte_black","mortise","zinc_alloy",(130,55,65,280),35,50,54,70,1000),
    ("HANDLE_002","Brushed Gold Lever Handle Set","dorset","lever-handle",2299,2999,"brushed_gold","mortise","brass",(135,58,62,350),35,50,54,70,1000),
    ("HANDLE_003","Satin Nickel Pull Handle 300mm","godrej","pull-handle",1899,2499,"satin_nickel","surface_mount","stainless_steel",(300,30,30,420),30,60,None,None,1000),
    ("HANDLE_004","Rose Gold Mortise Handle Set","dorset","mortise-handle",3499,4499,"rose_gold","mortise","brass",(140,60,68,380),35,55,54,70,1000),
    ("HANDLE_005","Chrome Round Door Knob","godrej","knob",599,799,"chrome","cylindrical","zinc_alloy",(60,60,60,180),30,45,54,None,1000),
    ("HANDLE_006","Antique Brass Lever Handle","dorset","lever-handle",1799,2299,"antique_brass","mortise","brass",(132,56,64,310),35,50,54,70,1000),
    ("HANDLE_007","Modern Flat Pull Handle 450mm","ozone","pull-handle",2499,3299,"brushed_nickel","surface_mount","stainless_steel",(450,30,30,580),30,65,None,None,1000),
    ("HANDLE_008","Teak Wood D-Handle","ebco","pull-handle",899,1199,"natural_wood","surface_mount","wood",(300,32,32,250),25,60,None,None,1000),
    ("HANDLE_009","SS304 Commercial D-Handle","godrej","pull-handle",1299,1699,"brushed_steel","surface_mount","stainless_steel",(300,30,30,450),30,60,None,None,1000),
    ("HANDLE_010","Crystal Glass Knob Set","dorset","knob",799,1099,"crystal_clear","cylindrical","glass",(60,60,60,120),30,45,54,None,1000),
    ("LOCK_001","Smart WiFi Deadbolt Lock","yale","smart-lock",8999,10999,"satin_nickel","deadbolt","zinc_alloy",(200,120,22,1100),35,55,54,60,1000),
    ("LOCK_002","Premium Mortise Lock Body","godrej","mortise-lock",3799,4499,"chrome","mortise","stainless_steel",(200,120,22,950),40,55,54,57,1000),
    ("LOCK_003","Night Latch Door Lock","godrej","night-latch",1299,1699,"chrome","rim","zinc_alloy",(100,80,40,450),35,50,None,None,1000),
    ("LOCK_004","Digital Touchscreen Door Lock","yale","smart-lock",15999,19999,"matte_black","smart","zinc_alloy",(260,75,30,1350),38,55,54,60,1000),
    ("LOCK_005","Heavy Duty Rim Lock","godrej","rim-lock",899,1199,"chrome","rim","iron",(120,85,35,650),35,50,None,None,1000),
    ("ACCS_001","Chrome Magnetic Door Stopper","ebco","accessory",299,449,"chrome","surface_mount","stainless_steel",(50,50,50,120),None,None,None,None,50),
    ("ACCS_002","180-Degree Wide Angle Peephole","godrej","accessory",449,599,"chrome","cylindrical","brass",(30,30,55,35),35,55,None,None,1500),
    ("ACCS_003","SS Ball Bearing Hinge 4-inch","ozone","accessory",349,499,"satin_nickel","surface_mount","stainless_steel",(100,100,10,180),None,None,None,None,300),
    ("ACCS_004","Hydraulic Door Closer","dorma","accessory",2499,3199,"silver","surface_mount","aluminum",(250,50,55,950),None,None,None,None,2000),
    ("ACCS_005","Antique Brass Lion Knocker","dorset","accessory",699,899,"antique_brass","surface_mount","brass",(100,150,30,420),30,60,None,None,1400),
]

async def seed_data():
    async with async_session_maker() as session:
        async with session.begin():
            existing = await session.execute(select(Brand).limit(1))
            if existing.scalar_one_or_none():
                print("Database already seeded. Skipping.")
                return

            print("Seeding brands...")
            brand_map = {}
            for b in BRANDS:
                brand = Brand(**b)
                session.add(brand)
                brand_map[b["slug"]] = brand
            await session.flush()
            print(f"  Created {len(brand_map)} brands")

            print("Seeding categories...")
            roots = {
                "handle": Category(name="Handle", slug="handle", icon="door"),
                "lock": Category(name="Lock", slug="lock", icon="lock"),
                "accessory": Category(name="Accessory", slug="accessory", icon="wrench"),
            }
            for r in roots.values():
                session.add(r)
            await session.flush()

            subcats = {
                "lever-handle": ("handle", "Lever Handle", 1),
                "pull-handle": ("handle", "Pull Handle", 2),
                "knob": ("handle", "Knob", 3),
                "mortise-handle": ("handle", "Mortise Handle", 4),
                "smart-lock": ("lock", "Smart Lock", 1),
                "mortise-lock": ("lock", "Mortise Lock", 2),
                "night-latch": ("lock", "Night Latch", 3),
                "rim-lock": ("lock", "Rim Lock", 4),
            }
            cat_map = dict(roots)
            for slug, (parent_slug, name, order) in subcats.items():
                cat = Category(name=name, slug=slug, parent_id=roots[parent_slug].id, display_order=order)
                session.add(cat)
                cat_map[slug] = cat
            await session.flush()
            print(f"  Created {len(cat_map)} categories")

            print("Seeding 20 products...")
            for i, p in enumerate(PRODUCTS):
                sku, name, brand_slug, cat_slug, price, mrp, finish, mount, mat, dims, min_th, max_th, bore, backset, anchor_y = p
                brand = brand_map[brand_slug]
                category = cat_map[cat_slug]
                w, h, d, weight = dims

                product = Product(
                    sku=sku, name=name,
                    description=f"{name} by {brand.name}. Finish: {finish}.",
                    brand_id=brand.id, category_id=category.id,
                    price=Decimal(str(price)), mrp=Decimal(str(mrp)),
                    finish=finish, mounting_type=mount,
                )
                session.add(product)
                await session.flush()

                session.add(ProductDimension(
                    product_id=product.id,
                    width_mm=Decimal(str(w)), height_mm=Decimal(str(h)),
                    depth_mm=Decimal(str(d)), weight_grams=Decimal(str(weight)),
                ))
                session.add(ProductCompatibility(
                    product_id=product.id,
                    door_materials=["wood", "metal"], door_types=["single"],
                    opening_directions=["left", "right"],
                    min_door_thickness_mm=Decimal(str(min_th)) if min_th else None,
                    max_door_thickness_mm=Decimal(str(max_th)) if max_th else None,
                    bore_diameter_mm=Decimal(str(bore)) if bore else None,
                    backset_mm=Decimal(str(backset)) if backset else None,
                ))
                cat_path = cat_slug.replace("-", "_")
                session.add(Product3DAsset(
                    product_id=product.id,
                    model_url=f"/static/models/{cat_path}/{sku}.glb",
                    model_format="glb", file_size_bytes=1024000, polygon_count=5000,
                    thumbnail_url=f"/static/thumbnails/{sku}.png",
                    anchor_x_mm=Decimal("0"), anchor_y_mm=Decimal(str(anchor_y)),
                    anchor_z_mm=Decimal("0"), lod_level=0,
                ))
                session.add(ProductImage(
                    product_id=product.id,
                    image_url=f"/static/images/{sku}.jpg",
                    alt_text=name, display_order=0, is_primary=True,
                ))
                session.add(ProductMaterial(
                    product_id=product.id, material_name=mat, percentage=Decimal("100"),
                ))
                print(f"  [{i+1:2d}/20] {sku} - {name}")

            await session.flush()
            print("Seeding complete!")

if __name__ == "__main__":
    asyncio.run(seed_data())