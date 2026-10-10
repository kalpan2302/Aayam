from contextlib import asynccontextmanager
from pathlib import Path
from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from fastapi.staticfiles import StaticFiles
import time
from datetime import datetime

from app.config import get_settings
from app.database import init_db, engine
from app.api.v1.router import api_router

settings = get_settings()

# Dev-only local asset serving (see LOCAL_STORAGE_DIR) - no AWS/Cloudflare account
# needed. Created on import so a fresh checkout doesn't fail to start; real assets
# are gitignored (backend/storage/**/*), folder structure kept via .gitkeep.
_storage_dir = Path(__file__).resolve().parent.parent / settings.LOCAL_STORAGE_DIR
_storage_dir.mkdir(parents=True, exist_ok=True)

@asynccontextmanager
async def lifespan(app: FastAPI):
    # Connect to DB on startup
    await init_db()
    yield
    # Dispose on shutdown
    await engine.dispose()

app = FastAPI(
    title=settings.APP_NAME,
    version=settings.APP_VERSION,
    docs_url='/docs',
    redoc_url='/redoc',
    lifespan=lifespan
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.CORS_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.middleware("http")
async def log_requests(request: Request, call_next):
    start_time = time.time()
    response = await call_next(request)
    process_time = time.time() - start_time
    print(f"Path: {request.url.path} - Time: {process_time:.4f}s")
    return response

app.include_router(api_router, prefix="/api/v1")
app.mount("/static", StaticFiles(directory=_storage_dir), name="static")

@app.get("/health")
async def health_check():
    return {
        "status": "healthy",
        "version": settings.APP_VERSION,
        "environment": settings.ENVIRONMENT,
        "timestamp": datetime.utcnow().isoformat()
    }

@app.exception_handler(404)
async def custom_404_handler(request, exc):
    return JSONResponse(
        status_code=404,
        content={"error": {"code": "NOT_FOUND", "message": "The requested resource was not found"}}
    )

@app.exception_handler(422)
async def custom_422_handler(request, exc):
    return JSONResponse(
        status_code=422,
        content={"error": {"code": "UNPROCESSABLE_ENTITY", "message": "Validation error", "details": exc.errors()}}
    )

@app.exception_handler(500)
async def custom_500_handler(request, exc):
    return JSONResponse(
        status_code=500,
        content={"error": {"code": "INTERNAL_SERVER_ERROR", "message": "An unexpected error occurred"}}
    )
