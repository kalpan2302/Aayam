from sqlalchemy import String, Boolean, DateTime, text
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import mapped_column

from app.database import Base


class User(Base):
    """Fitora customer account (2026-11-04 rebrand/redesign pass - first real
    user-accounts feature this project has; everything before this used
    dev/admin API keys, not per-user auth). password_hash is a bcrypt hash,
    never the plaintext password - see app/services/auth_service.py."""

    __tablename__ = "users"

    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    email = mapped_column(String(255), unique=True, nullable=False, index=True)
    password_hash = mapped_column(String(255), nullable=False)
    name = mapped_column(String(255), nullable=False)
    is_active = mapped_column(Boolean, server_default=text("true"), nullable=False)
    created_at = mapped_column(DateTime(timezone=True), server_default=text("now()"), nullable=False)
    updated_at = mapped_column(DateTime(timezone=True), server_default=text("now()"), nullable=False)
