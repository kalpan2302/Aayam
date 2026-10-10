# Phase 2: Create the Product Pipeline
**Duration:** 2-4 weeks (Month 2)

This phase creates the core product data system and 3D asset pipeline for Aayam.

## Overview
- **Goal:** Build the product database and 3D asset pipeline for 20 real products.
- This database will become one of our biggest business assets.
- **Initial Inventory:** 10 handles, 5 locks, 5 door accessories.

---

## Step 1: Database Schema Design (Week 1)

**Status: ✅ Done (2026-10-10).** Actual implementation exceeds this sketch —
see `backend/migrations/versions/001-003` for the real schema (12 tables, named
FKs, CHECK constraints, GIN/trigram/partial/covering indexes, 7 triggers,
partitioned audit log, views + materialized view). This section's SQL is
historical/illustrative, not what's deployed.

### Product Data Model Example (JSON)
```json
{
  "product": "HANDLE_001",
  "name": "Premium Matte Black Door Handle",
  "brand": "Godrej",
  "sku": "GDJ-MBH-001",
  "price": 1499,
  "currency": "INR",
  "dimensions": {
    "width_mm": 42,
    "height_mm": 180,
    "depth_mm": 55
  },
  "door_thickness": {
    "min_mm": 35,
    "max_mm": 50
  },
  "material": ["zinc_alloy"],
  "finish": "matte_black",
  "mounting": {
    "type": "mortise",
    "bore_diameter_mm": 54,
    "backset_mm": 70
  },
  "compatibility": {
    "door_materials": ["wood", "metal"],
    "door_types": ["single", "double"],
    "opening_directions": ["left", "right"]
  },
  "3d_asset": {
    "model_url": "s3://aayam-assets/models/HANDLE_001.glb",
    "thumbnail_url": "s3://aayam-assets/thumbnails/HANDLE_001.png",
    "anchor_point": {
      "x_mm": 720,
      "y_mm": 1000,
      "z_mm": 0
    }
  }
}
```

### PostgreSQL Schema

```sql
-- Brands
CREATE TABLE brands (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    logo_url VARCHAR(255),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Categories
CREATE TABLE categories (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL, -- e.g. Handle, Lock, Accessory
    parent_id INT REFERENCES categories(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Products
CREATE TABLE products (
    id SERIAL PRIMARY KEY,
    brand_id INT REFERENCES brands(id) ON DELETE RESTRICT,
    category_id INT REFERENCES categories(id) ON DELETE RESTRICT,
    sku VARCHAR(100) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    price DECIMAL(10, 2) NOT NULL,
    currency VARCHAR(3) DEFAULT 'INR',
    finish VARCHAR(100),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Product Dimensions
CREATE TABLE product_dimensions (
    product_id INT PRIMARY KEY REFERENCES products(id) ON DELETE CASCADE,
    width_mm DECIMAL(10, 2),
    height_mm DECIMAL(10, 2),
    depth_mm DECIMAL(10, 2),
    min_door_thickness_mm DECIMAL(10, 2),
    max_door_thickness_mm DECIMAL(10, 2)
);

-- Product Compatibility (JSONB for flexibility)
CREATE TABLE product_compatibility (
    product_id INT PRIMARY KEY REFERENCES products(id) ON DELETE CASCADE,
    door_materials JSONB, -- ["wood", "metal"]
    door_types JSONB, -- ["single", "double"]
    opening_directions JSONB -- ["left", "right"]
);

-- Product Materials
CREATE TABLE product_materials (
    product_id INT REFERENCES products(id) ON DELETE CASCADE,
    material_name VARCHAR(100),
    PRIMARY KEY (product_id, material_name)
);

-- Product 3D Assets
CREATE TABLE product_3d_assets (
    product_id INT PRIMARY KEY REFERENCES products(id) ON DELETE CASCADE,
    model_url VARCHAR(500) NOT NULL,
    thumbnail_url VARCHAR(500),
    anchor_x_mm DECIMAL(10, 2),
    anchor_y_mm DECIMAL(10, 2),
    anchor_z_mm DECIMAL(10, 2),
    file_size_bytes BIGINT
);

-- Product Images
CREATE TABLE product_images (
    id SERIAL PRIMARY KEY,
    product_id INT REFERENCES products(id) ON DELETE CASCADE,
    image_url VARCHAR(500) NOT NULL,
    is_primary BOOLEAN DEFAULT false,
    display_order INT DEFAULT 0
);
```

### Sample Data Insertion
```sql
INSERT INTO brands (name) VALUES ('Godrej'), ('Yale'), ('Dorset');
INSERT INTO categories (name) VALUES ('Handle'), ('Lock'), ('Accessory');

INSERT INTO products (brand_id, category_id, sku, name, description, price, finish)
VALUES (1, 1, 'GDJ-MBH-001', 'Premium Matte Black Door Handle', 'A premium handle', 1499.00, 'matte_black');

INSERT INTO product_dimensions (product_id, width_mm, height_mm, depth_mm, min_door_thickness_mm, max_door_thickness_mm)
VALUES (1, 42, 180, 55, 35, 50);

INSERT INTO product_3d_assets (product_id, model_url, thumbnail_url, anchor_x_mm, anchor_y_mm, anchor_z_mm)
VALUES (1, 's3://aayam-assets/models/HANDLE_001.glb', 's3://aayam-assets/thumbnails/HANDLE_001.png', 720, 1000, 0);
```

---

## Step 2: 3D Asset Pipeline (Weeks 1-2)

**Status: 🔶 1/20 done (verified 2026-10-11).** `HANDLE_001` has a real
Blender-authored GLB (`backend/storage/models/lever_handle/HANDLE_001.glb`,
~2.0MB) swapped in — but its pivot sits at the mesh's geometric center, not
the real mounting point, so it needs either a re-export with the origin at
the anchor or a runtime offset fix before it places correctly. The other 19
products (`backend/storage/models/*/*.glb`, ~1.7KB each) are temporary
placeholder boxes sized to each product's real dimensions
(`backend/scripts/generate_placeholder_models.py`), not real art — sourcing
real models for them hasn't started.

### Blender Workflow
1. **Source:** Obtain CAD files or reference photos.
2. **Modeling:** Create geometry in Blender, matching exact physical dimensions.
3. **Optimization:** Reduce polygon count (target < 50k per model).
4. **UV Mapping & Texturing:** Unwrap UVs efficiently, apply PBR materials.
5. **Export:** Export as glTF 2.0 / GLB format.

### Quality Requirements
- **Polygon Count:** Max 50,000 tris per model (ideally < 20k for performance).
- **Materials:** PBR (Metallic-Roughness workflow).
- **Scale:** 1 Unit = 1 Meter in Blender.
- **Pivot/Anchor:** Origin set at the exact mounting point.
- **Naming Convention:** `CATEGORY_BRAND_SKU.glb` (e.g., `HANDLE_GODREJ_GDJ-MBH-001.glb`).

### Texture Requirements
- **Resolution:** Max 2048x2048 (1024x1024 preferred for performance).
- **Formats:** PNG or KTX2 (for GPU compression).
- **Maps Required:** Albedo (Base Color), Normal, Metallic/Roughness (packed), Ambient Occlusion (AO).

---

## Step 3: AWS S3 Setup (Week 1)

**Status: ✅ Done (2026-10-10) — via local static file serving, not AWS.** To
avoid any cloud billing during dev, assets are served from the backend itself
instead: `backend/storage/{models,thumbnails,images,textures}/` mounted at
`/static` (see `app/storage.py`, `app/main.py`). `model_url`/`thumbnail_url`
are stored as relative paths (`/static/...`), so migrating to real S3 +
CloudFront later is a URL-prefix change, not a rewrite. Revisit this section
(the actual S3/CloudFront plan below) before a retail pilot needs assets
reachable from outside your own network.

### S3 Bucket Structure
```text
aayam-assets/
├── models/
│   ├── handles/
│   ├── locks/
│   └── accessories/
├── thumbnails/
├── images/
└── textures/
```

### Infrastructure Setup
1. **S3 Bucket:** Create `aayam-assets` bucket (block public access, manage via CloudFront).
2. **CloudFront CDN:** Set up a distribution pointing to the S3 bucket to ensure low-latency model downloads globally.
3. **IAM Policies:** Set up restricted IAM roles for the backend/upload scripts to write to S3.

### Example Upload Script (Python/Boto3)
```python
import boto3
import os

s3 = boto3.client('s3')
BUCKET_NAME = 'aayam-assets'

def upload_model(file_path, category, filename):
    object_name = f"models/{category}/{filename}"
    s3.upload_file(file_path, BUCKET_NAME, object_name, ExtraArgs={'ContentType': 'model/gltf-binary'})
    print(f"Uploaded {filename} to {object_name}")
```

---

## Step 4: Backend API (Week 2)

**Status: ✅ Done (2026-10-10).** `app/api/v1/{products,brands,categories}.py`
wired to the real DB (list/filter/sort/paginate, get by id/sku, create/update/
soft-delete, all tested — see `backend/tests/`). `app/api/v1/assets.py`'s
upload endpoint now writes to local storage (Step 3) with extension/path-
traversal validation. The endpoint list below is illustrative, not literal —
`/3d-asset` and `/compatibility` aren't separate routes; that data is nested
in `GET /products/{id}` instead (fewer round trips). `/qr` was a stub when
this was first written — it's done now, see Step 6.

### FastAPI Setup
```python
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import List, Optional

app = FastAPI(title="Aayam Product API", version="1.0.0")

class ProductResponse(BaseModel):
    id: int
    sku: str
    name: str
    brand: str
    price: float
    model_url: str

@app.get("/api/v1/products", response_model=List[ProductResponse])
def get_products():
    # Database fetch logic
    return [
        {"id": 1, "sku": "GDJ-MBH-001", "name": "Premium Handle", "brand": "Godrej", "price": 1499, "model_url": "..."}
    ]

@app.get("/api/v1/products/{product_id}")
def get_product(product_id: int):
    # Fetch specific product details
    pass
```

### Endpoints
- `GET /api/v1/products`
- `GET /api/v1/products/{id}`
- `GET /api/v1/products/category/{category}`
- `GET /api/v1/products/{id}/3d-asset`
- `GET /api/v1/products/{id}/compatibility`
- `POST /api/v1/products` (admin only)

---

## Step 5: Product Catalogue in App (Week 3)

**Status: ✅ Verified working (2026-10-10).** Played through the full
catalogue flow against the real backend/DB — list loads, thumbnails render,
tap-to-select works. Found and fixed a real bug along the way: `ProductCard`
was a second class living inside `ProductSelector.cs`, so Unity never
generated a MonoScript asset for it and the prefab baked `m_Script: {fileID:
0}` ("missing script"), silently breaking every card. Fixed by moving it to
its own `Assets/_Project/Scripts/UI/ProductCard.cs` and repointing the
prefab's `m_Script` GUID at the new asset.

### Unity Implementation
- **Data Loading:** Use Unity WebRequests to fetch JSON from `GET /api/v1/products`.
- **Model Downloading:** Implement async downloading of GLB files using `UnityWebRequest` and GLTFUtility/Siccity.
- **Caching:** Cache downloaded models locally in `Application.persistentDataPath` to avoid re-downloading.
- **UI:** Implement a scrollable grid view of product cards showing thumbnails, names, and prices. Tap to open a detailed preview.

---

## Step 6: QR Code System (Weeks 3-4)

**Status: 🔶 Generation done & verified; scanning code correct but not yet
device-verified (2026-10-10/11).** `GET /api/v1/assets/{id}/qr` generates real
`aayam://product/{sku}` QR PNGs — ran it for all 20 real products, confirmed
20 files on disk at `backend/storage/qrcodes/*.png`. `QRScanner.cs`'s decode
pipeline (camera frame → `TryAcquireLatestCpuImage` → ZXing →
`DeepLinkParser`) was proven bug-free via temporary diagnostic logging: every
frame, CPU image acquisition and ZXing decode both ran with zero errors — the
only reason decode kept returning null in testing is that XR Simulation's
camera capture only sees the baked Simulation Environment geometry, not an
ad-hoc QR texture placed in the scene. That's a testing-environment dead end,
not an app bug (see memory `xr-simulation-qr-scan-testing`). Still needs a
real on-device ARKit test with an actual printed/displayed QR code as the
real verification — that hasn't happened yet.

- **QR Generation:** Generate unique QR codes representing deep links (e.g., `aayam://product/GDJ-MBH-001`).
- **Unity Scanning:** Integrate a QR code scanning library (e.g., ZXing) in the Unity app.
- **Flow:** Scan QR → Decode ID → Fetch Product from API → Open AR visualization.

### QR Code Generator (Python)
```python
import qrcode

def generate_product_qr(product_id, output_path):
    url = f"aayam://product/{product_id}"
    qr = qrcode.QRCode(version=1, box_size=10, border=5)
    qr.add_data(url)
    qr.make(fit=True)
    img = qr.make_image(fill_color="black", back_color="white")
    img.save(output_path)
```

---

## Step 7: Anchor Point System (Week 3)

**Status: 🔶 Height-snap math verified, live tap-to-place trigger not yet
verified (2026-10-10/11).** `DoorSurfacePlacer.SnapToStandardHeight()` (floor
Y + `anchor_y_mm` → final world Y) was checked directly against 3 cases, all
correct. The live flow — aim the reticle at a vertical plane within 4m
(`PlacementReticle._maxDistance`, `_verticalOnly`), tap, `ObjectPlacement
Controller.ExecutePlacement()` fires, snapped to height if
`FloorPlaneDetector.HasFloor` — has **not** been exercised end-to-end live.
Two automated attempts (script-driven Play mode, no real mouse/keyboard)
both hit a Unity Editor bug where `Time.frameCount` stays frozen solid
whenever the Editor window lacks real OS focus, even with `isPlaying=true` —
this only resolves with a human actually driving Play mode. Manual test
steps are written up and waiting on Kalpan to run them and report the
console output.

Products must be anchored correctly on the detected door plane. Standard mounting heights:
- **Handle:** ~1000mm from floor.
- **Lock:** ~1000mm from floor.
- **Peephole:** ~1500mm from floor.
- **Door Knocker:** ~1400mm from floor.

*Metadata implementation:* The anchor coordinates `(x,y,z)` relative to the door corner/floor are stored in the `product_3d_assets` table.

---

## Step 8: Create Initial 20 Products

**Status: ✅ Done — the 2026-10-10 caveat below was wrong, corrected
2026-10-11.** Original note claimed this only ran against a throwaway test DB,
blocked on a port-5432 conflict. Verified directly against the actual running
system instead: `aayam_db` (docker container, mapped to host port **5434**,
not 5432 — so there was never a real conflict) is up and healthy, and holds
exactly 20 `products` rows / 20 `product_3d_assets` rows, matching the 10
handles / 5 locks / 5 accessories mix. This **is** the real persistent dev DB.
Also found and deleted one leftover test row (`LIFECYCLE_78D6948C`) that had
been written into this same real DB during earlier ad-hoc testing.

**Sourcing:** Gather real specs/prices from Indian hardware brands (Godrej, Yale, Dorset).

1. **10 Handles:**
   - Lever handles (3x), Knob (2x), Pull handle (3x), Mortise handle (2x).
2. **5 Locks:**
   - Deadbolt, Smart lock, Padlock, Mortise lock, Rim lock.
3. **5 Accessories:**
   - Door stopper, Peephole, Hinge, Door closer, Knocker.

---

## Deliverables

Status as of 2026-10-11 — see per-step notes above for detail/caveats.

- [x] PostgreSQL schema deployed — confirmed live on the real dev DB (`aayam_db`, port 5434), not a throwaway one
- [x] 20 product records with full metadata — confirmed, 20 rows in `products` + `product_3d_assets`
- [ ] 20 3D models (glTF/GLB) — only 1/20 is real art (`HANDLE_001`, pivot still wrong); other 19 are placeholder boxes
- [x] FastAPI backend with product endpoints — including QR generation (Step 6), verified for all 20 products
- [x] Product catalogue UI in app — played through end-to-end against the real backend; one real bug found and fixed (`ProductCard` missing-script)
- [~] QR code system — generation done & verified; scanning code proven correct in isolation but not yet verified on a real device (XR Simulation can't synthetic-test this)
- [~] Anchor point system — data model done, height-snap math verified in isolation; live tap-to-place trigger not yet exercised end-to-end (blocked on Unity Editor focus issue, needs Kalpan to run it live)
- [x] API documentation (auto-generated OpenAPI/Swagger at `/docs` — no separate hand-written doc)
