from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.ext.asyncio import AsyncSession

from app.database import get_db
from app.api.deps import get_current_user
from app.models.user import User
from app.schemas.user import AuthTokenResponse, UserLoginRequest, UserResponse, UserSignupRequest
from app.services.auth_service import AuthService

router = APIRouter()


@router.post("/signup", response_model=AuthTokenResponse, status_code=status.HTTP_201_CREATED)
async def signup(body: UserSignupRequest, db: AsyncSession = Depends(get_db)):
    existing = await AuthService.get_user_by_email(db, body.email)
    if existing is not None:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="An account with this email already exists.")

    user = await AuthService.create_user(db, body.email, body.password, body.name)
    token = AuthService.create_access_token(user.id)
    return AuthTokenResponse(access_token=token, user=UserResponse.model_validate(user))


@router.post("/login", response_model=AuthTokenResponse)
async def login(body: UserLoginRequest, db: AsyncSession = Depends(get_db)):
    user = await AuthService.get_user_by_email(db, body.email)
    # Same generic message for "no such user" and "wrong password" - do not
    # let a login attempt reveal whether an email is registered.
    invalid = HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Incorrect email or password.")
    if user is None or not AuthService.verify_password(body.password, user.password_hash):
        raise invalid
    if not user.is_active:
        raise invalid

    token = AuthService.create_access_token(user.id)
    return AuthTokenResponse(access_token=token, user=UserResponse.model_validate(user))


@router.get("/me", response_model=UserResponse)
async def get_me(current_user: User = Depends(get_current_user)):
    return UserResponse.model_validate(current_user)
