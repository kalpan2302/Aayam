"""Local dev-only asset storage path resolution.

Shared by app.main (mounting /static) and the assets upload endpoint, so the
directory-resolution logic (relative to the backend root, not cwd) lives in one
place. See Settings.LOCAL_STORAGE_DIR.
"""
from pathlib import Path

from app.config import get_settings


def get_storage_dir() -> Path:
    settings = get_settings()
    backend_root = Path(__file__).resolve().parent.parent
    storage_dir = backend_root / settings.LOCAL_STORAGE_DIR
    storage_dir.mkdir(parents=True, exist_ok=True)
    return storage_dir
