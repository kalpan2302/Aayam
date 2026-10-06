from sqlalchemy import Column, String, Integer, text
from sqlalchemy.dialects.postgresql import UUID, JSONB
from sqlalchemy.orm import mapped_column

from app.database import Base

class AuditLog(Base):
    __tablename__ = "audit_logs"
    id = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))
    entity_name = mapped_column(String, nullable=False)
    entity_id = mapped_column(UUID(as_uuid=True), nullable=False)
    action = mapped_column(String, nullable=False)
    changes = mapped_column(JSONB)
    user_id = mapped_column(UUID(as_uuid=True))
