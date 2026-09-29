# Aayam Backend API

High-performance REST API backend for the **Aayam** AI-powered AR platform, built with FastAPI, SQLAlchemy 2.0 (async), PostgreSQL, and Redis.

---

## 🚀 Quick Start with Docker Compose

The fastest way to spin up the backend along with PostgreSQL and Redis:

### 1. Prerequisites
- Docker (v24+) & Docker Compose (v2+)

### 2. Configure Environment
```bash
cp .env.example .env
```

### 3. Start Containers
```bash
docker compose up --build
```

The services will start with health checks:
- **FastAPI API Server**: [http://localhost:8000](http://localhost:8000)
- **PostgreSQL Database**: `localhost:5432` (`aayam` database)
- **Redis Cache**: `localhost:6379`

---

## 🛠️ Manual Setup Instructions

If running locally without Docker:

### 1. Prerequisites
- Python 3.12+
- PostgreSQL 16+
- Redis 7+

### 2. Create Virtual Environment
```bash
python -m venv .venv

# On Linux/macOS:
source .venv/bin/activate

# On Windows (PowerShell):
.venv\Scripts\Activate.ps1
```

### 3. Install Dependencies
```bash
pip install --upgrade pip
pip install -r requirements.txt
pip install -r requirements-dev.txt
```

### 4. Configure Environment
```bash
cp .env.example .env
```
Ensure PostgreSQL and Redis services are running, and update `.env` with your credentials.

### 5. Run Database Migrations
```bash
alembic upgrade head
```

### 6. Run the Development Server
```bash
uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```

---

## 📖 API Documentation

Once the server is running, interactive API documentation is available at:
- **Swagger UI**: [http://localhost:8000/docs](http://localhost:8000/docs)
- **ReDoc**: [http://localhost:8000/redoc](http://localhost:8000/redoc)
- **Health Check**: [http://localhost:8000/health](http://localhost:8000/health)

---

## 🧪 Running Tests

Run unit and integration tests using pytest:

```bash
# Run all tests
pytest

# Run tests with coverage report
pytest --cov=app --cov-report=term-missing

# Linting with Ruff
ruff check .

# Type checking with Mypy
mypy app
```

---

## ⚙️ Environment Variables

| Variable | Description | Example / Default | Required |
| :--- | :--- | :--- | :--- |
| `DATABASE_URL` | Async PostgreSQL connection string | `postgresql+asyncpg://aayam:aayam_dev_123@localhost:5432/aayam` | Yes |
| `REDIS_URL` | Redis cache and session store URL | `redis://localhost:6379/0` | Yes |
| `AWS_S3_BUCKET` | S3 bucket storing 3D GLB models & thumbnails | `aayam-assets-dev` | Yes |
| `AWS_REGION` | AWS Region for S3 / CloudFront | `ap-south-1` | Yes |
| `CLOUDFRONT_DOMAIN` | CDN distribution domain for assets | `assets.aayam.app` | No |
| `AWS_ACCESS_KEY_ID` | AWS IAM access key ID | `AKIA...` | No (in dev) |
| `AWS_SECRET_ACCESS_KEY` | AWS IAM secret access key | `...` | No (in dev) |
| `API_KEY` | Secret key for client app authentication | `dev-api-key-change-me` | Yes |
| `ADMIN_API_KEY` | Secret key for admin dashboard access | `dev-admin-key-change-me` | Yes |
| `ENVIRONMENT` | Application runtime environment | `development` / `staging` / `production` | Yes |
| `DEBUG` | Enable verbose logging and debug mode | `true` / `false` | No |

---

## 📁 Project Structure Overview

```text
backend/
├── app/
│   ├── api/              # API route definitions and endpoints (v1)
│   ├── core/             # Core configurations, security, dependencies
│   ├── models/           # SQLAlchemy 2.0 ORM models
│   ├── schemas/          # Pydantic 2.0 request/response schemas
│   ├── services/         # Business logic (3D GLB processing, S3, cache)
│   ├── main.py           # FastAPI application factory and lifecycle
│   └── config.py         # Pydantic-settings configuration
├── migrations/           # Alembic database migrations
│   ├── versions/         # Migration revision scripts
│   ├── env.py            # Alembic environment runner
│   └── script.py.mako    # Migration template
├── tests/                # Automated pytest suite
│   ├── __init__.py
│   ├── conftest.py       # Fixtures (FastAPI test client, settings)
│   └── test_health.py    # Health endpoint tests
├── .dockerignore         # Docker build ignore rules
├── .env.example          # Environment variables template
├── alembic.ini           # Alembic configuration
├── docker-compose.yml    # Local development stack (API + DB + Redis)
├── Dockerfile            # Multi-stage production container build
├── requirements.txt      # Production Python dependencies
└── requirements-dev.txt  # Development & test dependencies
```
