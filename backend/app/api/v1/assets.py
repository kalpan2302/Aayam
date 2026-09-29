from fastapi import APIRouter, Depends, UploadFile, File
from app.api.deps import verify_admin_key

router = APIRouter()

@router.post("/upload", dependencies=[Depends(verify_admin_key)])
async def upload_asset(file: UploadFile = File(...)):
    return {"filename": file.filename}

@router.get("/{product_id}/qr")
async def generate_qr(product_id: str):
    return {"qr_url": f"http://example.com/qr/{product_id}"}
