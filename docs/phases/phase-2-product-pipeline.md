# Phase 2: Create the Product Pipeline
**Duration:** 2-4 weeks (Month 2)

This phase creates the core product data system and 3D asset pipeline for Aayam.

## Overview
- **Goal:** Build the product database and 3D asset pipeline for 20 real products.
- This database will become one of our biggest business assets.
- **Initial Inventory:** 10 handles, 5 locks, 5 door accessories.

---

## Step 1: Database Schema Design (Week 1)

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

### Unity Implementation
- **Data Loading:** Use Unity WebRequests to fetch JSON from `GET /api/v1/products`.
- **Model Downloading:** Implement async downloading of GLB files using `UnityWebRequest` and GLTFUtility/Siccity.
- **Caching:** Cache downloaded models locally in `Application.persistentDataPath` to avoid re-downloading.
- **UI:** Implement a scrollable grid view of product cards showing thumbnails, names, and prices. Tap to open a detailed preview.

---

## Step 6: QR Code System (Weeks 3-4)

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

Products must be anchored correctly on the detected door plane. Standard mounting heights:
- **Handle:** ~1000mm from floor.
- **Lock:** ~1000mm from floor.
- **Peephole:** ~1500mm from floor.
- **Door Knocker:** ~1400mm from floor.

*Metadata implementation:* The anchor coordinates `(x,y,z)` relative to the door corner/floor are stored in the `product_3d_assets` table.

---

## Step 8: Create Initial 20 Products

**Sourcing:** Gather real specs/prices from Indian hardware brands (Godrej, Yale, Dorset).

1. **10 Handles:**
   - Lever handles (3x), Knob (2x), Pull handle (3x), Mortise handle (2x).
2. **5 Locks:**
   - Deadbolt, Smart lock, Padlock, Mortise lock, Rim lock.
3. **5 Accessories:**
   - Door stopper, Peephole, Hinge, Door closer, Knocker.

---

## Deliverables
- [ ] PostgreSQL schema deployed
- [ ] 20 product records with full metadata
- [ ] 20 3D models (glTF/GLB) in S3
- [ ] FastAPI backend with product endpoints
- [ ] Product catalogue UI in app
- [ ] QR code system working
- [ ] Anchor point system defined
- [ ] API documentation
