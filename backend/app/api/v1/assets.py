import re
from pathlib import Path
from typing import Optional

import qrcode  # type: ignore[import-untyped]  # no published type stubs
from fastapi import APIRouter, Depends, HTTPException, Query, UploadFile, File

from app.api.deps import verify_admin_key
from app.storage import get_storage_dir

router = APIRouter()

_SUBDIR_BY_TYPE = {
    "model": "models",
    "thumbnail": "thumbnails",
    "image": "images",
    "texture": "textures",
}
_ALLOWED_EXTENSIONS = {
    "model": {".glb", ".gltf"},
    "thumbnail": {".png", ".jpg", ".jpeg", ".webp"},
    "image": {".png", ".jpg", ".jpeg", ".webp"},
    "texture": {".png", ".jpg", ".jpeg", ".ktx2"},
}
# Filename/category must be a single safe path segment - no "..", no "/", no
# leading dot - since both are joined onto a filesystem path below.
_SAFE_SEGMENT = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.-]*$")


def _safe_segment(value: str, field: str) -> str:
    if not _SAFE_SEGMENT.match(value):
        raise HTTPException(status_code=400, detail=f"Invalid {field}")
    return value


@router.post("/upload", dependencies=[Depends(verify_admin_key)])
async def upload_asset(
    file: UploadFile = File(...),
    asset_type: str = Query(..., pattern="^(model|thumbnail|image|texture)$"),
    category: Optional[str] = Query(None, description="Subfolder for model files, e.g. 'lever_handle'"),
):
    filename = _safe_segment(Path(file.filename or "").name, "filename")
    ext = Path(filename).suffix.lower()
    if ext not in _ALLOWED_EXTENSIONS[asset_type]:
        allowed = ", ".join(sorted(_ALLOWED_EXTENSIONS[asset_type]))
        raise HTTPException(
            status_code=400,
            detail=f"'{ext}' is not valid for asset_type '{asset_type}' (allowed: {allowed})",
        )

    target_dir = get_storage_dir() / _SUBDIR_BY_TYPE[asset_type]
    if asset_type == "model" and category:
        target_dir = target_dir / _safe_segment(category, "category")
    target_dir.mkdir(parents=True, exist_ok=True)

    dest = target_dir / filename
    contents = await file.read()
    dest.write_bytes(contents)

    rel_path = dest.relative_to(get_storage_dir()).as_posix()
    return {"url": f"/static/{rel_path}", "size_bytes": len(contents)}

@router.get("/{product_id}/qr")
async def generate_qr(product_id: str):
    """Generate (or regenerate) a QR code encoding a deep link to this product.

    Does not validate that product_id refers to an existing product - the
    deep link is only resolved when the app scans it and fetches the real
    product, so an invalid id just 404s there, not here.
    """
    product_id = _safe_segment(product_id, "product_id")
    deep_link = f"aayam://product/{product_id}"

    qr = qrcode.QRCode(version=1, box_size=10, border=5)
    qr.add_data(deep_link)
    qr.make(fit=True)
    img = qr.make_image(fill_color="black", back_color="white")

    target_dir = get_storage_dir() / "qrcodes"
    target_dir.mkdir(parents=True, exist_ok=True)
    dest = target_dir / f"{product_id}.png"
    img.save(dest)

    return {"qr_url": f"/static/qrcodes/{product_id}.png", "deep_link": deep_link}
