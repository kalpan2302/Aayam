from datetime import datetime, timedelta, timezone
from typing import Optional
from uuid import UUID

import bcrypt
import jwt
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.config import get_settings
from app.models.user import User

settings = get_settings()


class AuthService:
    # ─── Passwords ──────────────────────────────────────────────
    # bcrypt directly (not passlib) - simpler, actively maintained, and this
    # project has no other password-hashing call site to share an
    # abstraction with. bcrypt truncates input past 72 bytes by design;
    # UserSignupRequest.password already caps at 128 chars in the schema,
    # so this is a known, accepted bcrypt limitation, not an oversight.
    @staticmethod
    def hash_password(password: str) -> str:
        return bcrypt.hashpw(password.encode("utf-8"), bcrypt.gensalt()).decode("utf-8")

    @staticmethod
    def verify_password(password: str, password_hash: str) -> bool:
        return bcrypt.checkpw(password.encode("utf-8"), password_hash.encode("utf-8"))

    # ─── JWT ────────────────────────────────────────────────────
    @staticmethod
    def create_access_token(user_id: UUID) -> str:
        expire = datetime.now(timezone.utc) + timedelta(minutes=settings.JWT_EXPIRE_MINUTES)
        payload = {"sub": str(user_id), "exp": expire}
        return jwt.encode(payload, settings.JWT_SECRET, algorithm=settings.JWT_ALGORITHM)

    @staticmethod
    def decode_access_token(token: str) -> Optional[UUID]:
        try:
            payload = jwt.decode(token, settings.JWT_SECRET, algorithms=[settings.JWT_ALGORITHM])
            return UUID(payload["sub"])
        except (jwt.PyJWTError, KeyError, ValueError):
            return None

    # ─── User lookups ───────────────────────────────────────────
    @staticmethod
    async def get_user_by_email(db: AsyncSession, email: str) -> Optional[User]:
        result = await db.execute(select(User).where(User.email == email.lower()))
        return result.scalar_one_or_none()

    @staticmethod
    async def get_user_by_id(db: AsyncSession, user_id: UUID) -> Optional[User]:
        result = await db.execute(select(User).where(User.id == user_id))
        return result.scalar_one_or_none()

    @staticmethod
    async def create_user(db: AsyncSession, email: str, password: str, name: str) -> User:
        user = User(email=email.lower(), password_hash=AuthService.hash_password(password), name=name)
        db.add(user)
        await db.commit()
        await db.refresh(user)
        return user
