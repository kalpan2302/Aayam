"""
Generate placeholder box-shaped GLB models for every seeded product, sized to
its real dimensions, so Phase 2 Step 5 (catalogue -> download -> AR placement)
is testable before real Blender art (Step 2) exists. Swap these out later by
just overwriting the same files - nothing else needs to change.

Run: python -m scripts.generate_placeholder_models
"""
import asyncio
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from sqlalchemy import select
from app.database import async_session_maker
from app.models import Product, ProductDimension, Product3DAsset
from app.storage import get_storage_dir

from pygltflib import (
    GLTF2, Scene, Node, Mesh, Primitive, Attributes,
    Buffer, BufferView, Accessor, Material, PbrMetallicRoughness,
    FLOAT, UNSIGNED_SHORT, ARRAY_BUFFER, ELEMENT_ARRAY_BUFFER,
    VEC3, SCALAR,
)

_CATEGORY_COLORS = {
    "handle": (0.15, 0.15, 0.17, 1.0),    # dark gunmetal
    "lock": (0.45, 0.36, 0.12, 1.0),      # brass-ish
    "accs": (0.55, 0.55, 0.55, 1.0),      # neutral grey
}


def _color_for_sku(sku: str) -> tuple:
    prefix = sku.split("_")[0].lower()
    return _CATEGORY_COLORS.get(prefix, _CATEGORY_COLORS["accs"])


def _box_geometry(width_m: float, height_m: float, depth_m: float):
    """6 faces x 4 verts, outward-facing normals, CCW winding viewed from
    outside. Material is double-sided anyway (see _build_glb), so a winding
    mistake here costs shading quality, not visibility."""
    hw, hh, hd = width_m / 2, height_m / 2, depth_m / 2
    faces = [
        ((1, 0, 0),  [(hw, -hh, hd), (hw, -hh, -hd), (hw, hh, -hd), (hw, hh, hd)]),
        ((-1, 0, 0), [(-hw, -hh, -hd), (-hw, -hh, hd), (-hw, hh, hd), (-hw, hh, -hd)]),
        ((0, 1, 0),  [(-hw, hh, hd), (hw, hh, hd), (hw, hh, -hd), (-hw, hh, -hd)]),
        ((0, -1, 0), [(-hw, -hh, -hd), (hw, -hh, -hd), (hw, -hh, hd), (-hw, -hh, hd)]),
        ((0, 0, 1),  [(-hw, -hh, hd), (-hw, hh, hd), (hw, hh, hd), (hw, -hh, hd)]),
        ((0, 0, -1), [(hw, -hh, -hd), (hw, hh, -hd), (-hw, hh, -hd), (-hw, -hh, -hd)]),
    ]
    positions, normals, indices = [], [], []
    for normal, corners in faces:
        base = len(positions)
        positions.extend(corners)
        normals.extend([normal] * 4)
        indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])
    return positions, normals, indices


def _pad4(b: bytes) -> bytes:
    return b + b"\x00" * (-len(b) % 4)


def _build_glb_bytes(width_m: float, height_m: float, depth_m: float, color) -> GLTF2:
    positions, normals, indices = _box_geometry(
        max(width_m, 0.01), max(height_m, 0.01), max(depth_m, 0.01)
    )

    pos_bytes = _pad4(b"".join(struct.pack("<3f", *p) for p in positions))
    norm_bytes = _pad4(b"".join(struct.pack("<3f", *n) for n in normals))
    idx_bytes = _pad4(b"".join(struct.pack("<H", i) for i in indices))
    blob = pos_bytes + norm_bytes + idx_bytes

    flat_x = [p[0] for p in positions]
    flat_y = [p[1] for p in positions]
    flat_z = [p[2] for p in positions]

    gltf = GLTF2()
    gltf.scene = 0
    gltf.scenes = [Scene(nodes=[0])]
    gltf.nodes = [Node(mesh=0)]
    gltf.meshes = [Mesh(primitives=[Primitive(
        attributes=Attributes(POSITION=0, NORMAL=1), indices=2, material=0,
    )])]
    gltf.materials = [Material(
        pbrMetallicRoughness=PbrMetallicRoughness(
            baseColorFactor=list(color), metallicFactor=0.3, roughnessFactor=0.6,
        ),
        doubleSided=True,
    )]
    gltf.buffers = [Buffer(byteLength=len(blob))]
    gltf.bufferViews = [
        BufferView(buffer=0, byteOffset=0, byteLength=len(positions) * 12, target=ARRAY_BUFFER),
        BufferView(buffer=0, byteOffset=len(pos_bytes), byteLength=len(normals) * 12, target=ARRAY_BUFFER),
        BufferView(buffer=0, byteOffset=len(pos_bytes) + len(norm_bytes),
                   byteLength=len(indices) * 2, target=ELEMENT_ARRAY_BUFFER),
    ]
    gltf.accessors = [
        Accessor(bufferView=0, componentType=FLOAT, count=len(positions), type=VEC3,
                 min=[min(flat_x), min(flat_y), min(flat_z)],
                 max=[max(flat_x), max(flat_y), max(flat_z)]),
        Accessor(bufferView=1, componentType=FLOAT, count=len(normals), type=VEC3),
        Accessor(bufferView=2, componentType=UNSIGNED_SHORT, count=len(indices), type=SCALAR),
    ]
    gltf.set_binary_blob(blob)
    return gltf


async def main():
    async with async_session_maker() as session:
        result = await session.execute(
            select(Product.sku, ProductDimension.width_mm, ProductDimension.height_mm,
                   ProductDimension.depth_mm, Product3DAsset.model_url)
            .join(ProductDimension, ProductDimension.product_id == Product.id)
            .join(Product3DAsset, Product3DAsset.product_id == Product.id)
            .order_by(Product.sku)
        )
        rows = result.all()

    if not rows:
        print("No products found - run `python -m scripts.seed_data` first.")
        return

    storage_root = get_storage_dir()
    written = 0
    for sku, width_mm, height_mm, depth_mm, model_url in rows:
        if not model_url or not model_url.startswith("/static/"):
            print(f"  skip {sku}: unexpected model_url {model_url!r}")
            continue

        dest = storage_root / model_url[len("/static/"):]
        dest.parent.mkdir(parents=True, exist_ok=True)

        width_m = float(width_mm or 50) / 1000.0
        height_m = float(height_mm or 50) / 1000.0
        depth_m = float(depth_mm or 20) / 1000.0

        gltf = _build_glb_bytes(width_m, height_m, depth_m, _color_for_sku(sku))
        gltf.save_binary(str(dest))

        written += 1
        print(f"  [{written}/{len(rows)}] {sku} -> {dest.relative_to(storage_root)} "
              f"({width_m:.3f} x {height_m:.3f} x {depth_m:.3f} m)")

    print(f"Done - wrote {written} placeholder GLB(s). These are boxes, not real "
          f"products - swap them out by overwriting the same files once Step 2 art exists.")


if __name__ == "__main__":
    asyncio.run(main())
