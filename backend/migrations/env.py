import sys
from logging.config import fileConfig
from pathlib import Path

from alembic import context
from sqlalchemy import engine_from_config, pool

# Ensure backend root is on sys.path
backend_root = Path(__file__).resolve().parent.parent
if str(backend_root) not in sys.path:
    sys.path.insert(0, str(backend_root))

# this is the Alembic Config object, which provides
# access to the values within the .ini file in use.
config = context.config

# Interpret the config file for Python logging.
if config.config_file_name is not None:
    fileConfig(config.config_file_name)

# Import Base model and configuration settings
try:
    from app.config import get_settings
    from app.models import Base

    settings = get_settings()
    target_metadata = Base.metadata
except ImportError:
    settings = None
    target_metadata = None


def get_url() -> str:
    """Retrieve synchronous database URL from settings or alembic config."""
    if settings is not None:
        if hasattr(settings, "DATABASE_URL_SYNC") and settings.DATABASE_URL_SYNC:
            return str(settings.DATABASE_URL_SYNC)
        if hasattr(settings, "DATABASE_URL") and settings.DATABASE_URL:
            return str(settings.DATABASE_URL).replace(
                "postgresql+asyncpg://", "postgresql+psycopg2://"
            )
    return config.get_main_option(
        "sqlalchemy.url",
        "postgresql+psycopg2://aayam:aayam_dev_123@localhost:5432/aayam",
    )


def run_migrations_offline() -> None:
    """Run migrations in 'offline' mode.

    This configures the context with just a URL
    and not an Engine, though an Engine is acceptable
    here as well. By skipping the Engine creation
    we don't even need a DBAPI to be available.
    """
    url = get_url()
    context.configure(
        url=url,
        target_metadata=target_metadata,
        literal_binds=True,
        dialect_opts={"paramstyle": "named"},
    )

    with context.begin_transaction():
        context.run_migrations()


def run_migrations_online() -> None:
    """Run migrations in 'online' mode.

    In this scenario we need to create an Engine
    and associate a connection with the context.
    """
    configuration = config.get_section(config.config_ini_section, {})
    configuration["sqlalchemy.url"] = get_url()

    connectable = engine_from_config(
        configuration,
        prefix="sqlalchemy.",
        poolclass=pool.NullPool,
    )

    with connectable.connect() as connection:
        context.configure(
            connection=connection,
            target_metadata=target_metadata,
        )

        with context.begin_transaction():
            context.run_migrations()


if context.is_offline_mode():
    run_migrations_offline()
else:
    run_migrations_online()
