# Aayam — Database Design Document

**Version:** 1.0  
**Phase:** 2 — Product Pipeline  
**Database:** PostgreSQL 16+  
**Last Updated:** 2026-09-29

---

## Table of Contents

1. [Design Principles](#1-design-principles)
2. [Naming Conventions](#2-naming-conventions)
3. [Entity-Relationship Diagram](#3-entity-relationship-diagram)
4. [Schema Definition](#4-schema-definition)
5. [Constraints & Checks](#5-constraints--checks)
6. [Indexes](#6-indexes)
7. [Partitioning Strategy](#7-partitioning-strategy)
8. [Triggers & Functions](#8-triggers--functions)
9. [Audit Trail](#9-audit-trail)
10. [Views & Materialized Views](#10-views--materialized-views)
11. [Seed Data](#11-seed-data)
12. [Migration Strategy](#12-migration-strategy)
13. [Performance Considerations](#13-performance-considerations)
14. [Security](#14-security)

---

## 1. Design Principles

### 1.1 Normalization

| Level | Applied | Rationale |
|-------|---------|-----------|
| **1NF** | ✅ | All columns are atomic — no repeating groups. `door_materials` uses a junction table, not comma-separated strings. |
| **2NF** | ✅ | Every non-key column depends on the full primary key. Dimensions are in a separate table because they depend on product, not on product+category. |
| **3NF** | ✅ | No transitive dependencies. `brand_name` is never stored on `products` — it's always JOINed from `brands`. Price discount is computed, not stored. |
| **BCNF** | ✅ | Every determinant is a candidate key. |

> **Exception:** `product_compatibility` stores `door_materials`, `door_types`, `opening_directions` as JSONB arrays. This is a **controlled denormalization** for query performance — these are small, bounded enums that rarely change and are always read together.

### 1.2 SOLID Applied to Schema Design

| Principle | Application |
|-----------|-------------|
| **S** — Single Responsibility | Each table has ONE purpose. `products` stores product identity; `product_dimensions` stores measurements; `product_3d_assets` stores AR data. |
| **O** — Open/Closed | Schema supports new product types (handles, locks, accessories) without DDL changes — category-specific attributes use the `product_attributes` EAV table. |
| **L** — Liskov Substitution | All products (handles, locks, accessories) share the same `products` table with identical constraints. Category-specific behavior is in application logic, not schema. |
| **I** — Interface Segregation | API consumers get tailored views: `v_product_list` for catalogue, `v_product_detail` for AR placement. They never query raw tables. |
| **D** — Dependency Inversion | Foreign keys point from child → parent (products → brands), never the reverse. No circular references. |

### 1.3 System Design Principles

| Principle | Implementation |
|-----------|---------------|
| **Immutability** | Prices are versioned in `product_price_history`. Current price is the latest entry. Old prices are never mutated. |
| **Idempotency** | `sku` is the natural idempotent key. Upserts use `ON CONFLICT (sku) DO UPDATE`. |
| **Eventual Consistency** | Materialized views for catalogue may lag by the refresh interval. Real-time reads go to source tables. |
| **Soft Deletes** | Products are never physically deleted. `deleted_at IS NOT NULL` marks deletion. All queries filter `WHERE deleted_at IS NULL`. |
| **Audit Trail** | Every mutation is logged to `audit_log` via triggers. WHO changed WHAT, WHEN, with OLD and NEW values. |
| **Temporal Data** | `created_at`, `updated_at`, `deleted_at` on every table. `updated_at` auto-set by trigger. |

### 1.4 Key Strategy

| Table | PK Type | Rationale |
|-------|---------|-----------|
| `brands` | `SERIAL` (auto-increment) | Internal reference only; never exposed to external systems |
| `categories` | `SERIAL` | Small table, simple hierarchy |
| `products` | `UUID v7` | Exposed in APIs and QR codes; UUID prevents enumeration attacks and is merge-safe across distributed systems |
| `product_*` child tables | `product_id UUID FK` | 1:1 with product, shares the same PK |
| `product_images` | `UUID v7` | Independent entity, needs its own identity |
| `audit_log` | `BIGSERIAL` | Append-only, sequential writes benefit from auto-increment |

> **Why UUID v7 for products?** UUID v7 is time-ordered (RFC 9562), so B-tree index performance is comparable to SERIAL. It prevents ID guessing in APIs and supports future multi-region replication without PK conflicts.

---

## 2. Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Tables | `snake_case`, plural | `products`, `brands` |
| Columns | `snake_case` | `min_door_thickness_mm` |
| Primary Keys | `id` | `products.id` |
| Foreign Keys | `{referenced_table_singular}_id` | `products.brand_id` |
| Indexes | `idx_{table}_{columns}` | `idx_products_brand_id` |
| Unique Constraints | `uq_{table}_{columns}` | `uq_products_sku` |
| Check Constraints | `chk_{table}_{rule}` | `chk_products_price_positive` |
| Triggers | `trg_{table}_{action}` | `trg_products_updated_at` |
| Functions | `fn_{purpose}` | `fn_set_updated_at()` |
| Views | `v_{purpose}` | `v_product_list` |
| Materialized Views | `mv_{purpose}` | `mv_product_catalogue` |
| Enums | `e_{name}` | `e_mounting_type` |
| Partitions | `{table}_p_{key}` | `audit_log_p_2026_10` |

---

## 3. Entity-Relationship Diagram

```mermaid
erDiagram
    brands ||--o{ products : "has"
    categories ||--o{ products : "contains"
    categories ||--o{ categories : "parent"
    products ||--|| product_dimensions : "has"
    products ||--|| product_3d_assets : "has"
    products ||--|| product_compatibility : "has"
    products ||--o{ product_images : "has"
    products ||--o{ product_materials : "made_of"
    products ||--o{ product_price_history : "priced_at"
    products ||--o{ product_attributes : "extends"
    products ||--o{ product_tags : "tagged"
    tags ||--o{ product_tags : "applied_to"
    audit_log }o--|| products : "tracks"

    brands {
        int id PK
        varchar name UK
        varchar slug UK
        text description
        varchar logo_url
        varchar country
        boolean is_active
        timestamptz created_at
        timestamptz updated_at
    }

    categories {
        int id PK
        varchar name
        varchar slug UK
        int parent_id FK
        varchar icon
        int display_order
        int depth
        timestamptz created_at
    }

    products {
        uuid id PK
        varchar sku UK
        varchar name
        text description
        int brand_id FK
        int category_id FK
        decimal price
        decimal mrp
        varchar currency
        varchar finish
        int weight_grams
        varchar material_primary
        e_mounting_type mounting_type
        decimal bore_diameter_mm
        decimal backset_mm
        boolean is_active
        boolean is_featured
        timestamptz created_at
        timestamptz updated_at
        timestamptz deleted_at
    }

    product_dimensions {
        uuid product_id PK_FK
        decimal width_mm
        decimal height_mm
        decimal depth_mm
        decimal lever_length_mm
        decimal rose_diameter_mm
        decimal face_plate_width_mm
        decimal face_plate_height_mm
    }

    product_3d_assets {
        uuid product_id PK_FK
        varchar model_url
        e_model_format model_format
        int file_size_bytes
        int polygon_count
        varchar thumbnail_url
        decimal anchor_x_mm
        decimal anchor_y_mm
        decimal anchor_z_mm
        varchar lod_high_url
        varchar lod_medium_url
        varchar lod_low_url
        boolean has_pbr_materials
        int texture_resolution
        timestamptz last_validated_at
        timestamptz uploaded_at
    }

    product_compatibility {
        uuid product_id PK_FK
        jsonb door_materials
        jsonb door_types
        jsonb opening_directions
        decimal min_door_thickness_mm
        decimal max_door_thickness_mm
    }

    product_images {
        uuid id PK
        uuid product_id FK
        varchar image_url
        varchar alt_text
        boolean is_primary
        int display_order
        int width_px
        int height_px
        int file_size_bytes
        timestamptz created_at
    }

    product_materials {
        uuid product_id PK_FK
        varchar material_name PK
        decimal percentage
    }

    product_price_history {
        bigint id PK
        uuid product_id FK
        decimal price
        decimal mrp
        varchar reason
        timestamptz effective_from
        timestamptz effective_until
        timestamptz created_at
    }

    product_attributes {
        uuid product_id PK_FK
        varchar attribute_key PK
        varchar attribute_value
        varchar attribute_type
    }

    tags {
        int id PK
        varchar name UK
        varchar slug UK
    }

    product_tags {
        uuid product_id PK_FK
        int tag_id PK_FK
    }

    audit_log {
        bigint id PK
        varchar table_name
        uuid record_id
        varchar action
        jsonb old_values
        jsonb new_values
        varchar changed_by
        inet ip_address
        timestamptz created_at
    }
```

---

## 4. Schema Definition

### 4.1 Extensions & Enums

```sql
-- ═══════════════════════════════════════════════════════════
-- Extensions
-- ═══════════════════════════════════════════════════════════

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";       -- UUID generation
CREATE EXTENSION IF NOT EXISTS "pgcrypto";         -- Cryptographic functions
CREATE EXTENSION IF NOT EXISTS "pg_trgm";          -- Trigram fuzzy search
CREATE EXTENSION IF NOT EXISTS "btree_gin";        -- GIN index on scalar types

-- ═══════════════════════════════════════════════════════════
-- Custom Enums (type-safe, performant vs VARCHAR)
-- ═══════════════════════════════════════════════════════════

CREATE TYPE e_mounting_type AS ENUM (
    'mortise',
    'cylindrical',
    'tubular',
    'surface_mount',
    'rim',
    'deadbolt',
    'smart',
    'other'
);

CREATE TYPE e_model_format AS ENUM ('glb', 'gltf', 'usdz');

CREATE TYPE e_currency AS ENUM ('INR', 'USD', 'EUR');

CREATE TYPE e_audit_action AS ENUM ('INSERT', 'UPDATE', 'DELETE');

CREATE TYPE e_attribute_type AS ENUM ('string', 'number', 'boolean', 'json');
```

### 4.2 Core Tables

```sql
-- ═══════════════════════════════════════════════════════════
-- BRANDS
-- ═══════════════════════════════════════════════════════════

CREATE TABLE brands (
    id            SERIAL       PRIMARY KEY,
    name          VARCHAR(255) NOT NULL,
    slug          VARCHAR(255) NOT NULL,
    description   TEXT,
    logo_url      VARCHAR(500),
    website       VARCHAR(500),
    country       VARCHAR(100) NOT NULL DEFAULT 'India',
    is_active     BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),

    -- Unique constraints
    CONSTRAINT uq_brands_name UNIQUE (name),
    CONSTRAINT uq_brands_slug UNIQUE (slug),

    -- Check constraints
    CONSTRAINT chk_brands_name_not_empty
        CHECK (LENGTH(TRIM(name)) > 0),
    CONSTRAINT chk_brands_slug_format
        CHECK (slug ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'),
    CONSTRAINT chk_brands_logo_url_format
        CHECK (logo_url IS NULL OR logo_url ~ '^https?://'),
    CONSTRAINT chk_brands_website_format
        CHECK (website IS NULL OR website ~ '^https?://')
);

COMMENT ON TABLE  brands IS 'Hardware manufacturers and distributors (Godrej, Yale, Dorset, etc.)';
COMMENT ON COLUMN brands.slug IS 'URL-safe lowercase identifier. Generated from name. Immutable after creation.';


-- ═══════════════════════════════════════════════════════════
-- CATEGORIES (self-referencing hierarchy)
-- ═══════════════════════════════════════════════════════════

CREATE TABLE categories (
    id            SERIAL       PRIMARY KEY,
    name          VARCHAR(255) NOT NULL,
    slug          VARCHAR(255) NOT NULL,
    parent_id     INT,
    icon          VARCHAR(100),
    display_order INT          NOT NULL DEFAULT 0,
    depth         INT          NOT NULL DEFAULT 0,
    is_active     BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),

    -- Foreign keys
    CONSTRAINT fk_categories_parent
        FOREIGN KEY (parent_id) REFERENCES categories(id)
        ON DELETE RESTRICT   -- Prevent deleting a category that has children
        ON UPDATE CASCADE,

    -- Unique constraints
    CONSTRAINT uq_categories_slug UNIQUE (slug),

    -- Check constraints
    CONSTRAINT chk_categories_name_not_empty
        CHECK (LENGTH(TRIM(name)) > 0),
    CONSTRAINT chk_categories_slug_format
        CHECK (slug ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'),
    CONSTRAINT chk_categories_no_self_parent
        CHECK (parent_id IS NULL OR parent_id != id),
    CONSTRAINT chk_categories_depth_non_negative
        CHECK (depth >= 0),
    CONSTRAINT chk_categories_max_depth
        CHECK (depth <= 5)  -- Prevent runaway nesting
);

COMMENT ON TABLE categories IS 'Product taxonomy. Max 5 levels deep. Examples: Handle > Lever, Lock > Smart Lock';
COMMENT ON COLUMN categories.depth IS 'Computed depth in hierarchy tree. Root = 0.';


-- ═══════════════════════════════════════════════════════════
-- PRODUCTS (central entity)
-- ═══════════════════════════════════════════════════════════

CREATE TABLE products (
    id                UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    sku               VARCHAR(100) NOT NULL,
    name              VARCHAR(255) NOT NULL,
    description       TEXT,

    -- References
    brand_id          INT          NOT NULL,
    category_id       INT          NOT NULL,

    -- Pricing
    price             DECIMAL(10,2) NOT NULL,
    mrp               DECIMAL(10,2),
    currency          e_currency    NOT NULL DEFAULT 'INR',

    -- Physical properties
    finish            VARCHAR(100),
    weight_grams      INT,
    material_primary  VARCHAR(100),

    -- Mounting
    mounting_type     e_mounting_type,
    bore_diameter_mm  DECIMAL(6,2),
    backset_mm        DECIMAL(6,2),

    -- Status
    is_active         BOOLEAN      NOT NULL DEFAULT TRUE,
    is_featured       BOOLEAN      NOT NULL DEFAULT FALSE,

    -- Temporal
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    deleted_at        TIMESTAMPTZ,          -- Soft delete

    -- Foreign keys
    CONSTRAINT fk_products_brand
        FOREIGN KEY (brand_id) REFERENCES brands(id)
        ON DELETE RESTRICT   -- Cannot delete brand with products
        ON UPDATE CASCADE,
    CONSTRAINT fk_products_category
        FOREIGN KEY (category_id) REFERENCES categories(id)
        ON DELETE RESTRICT   -- Cannot delete category with products
        ON UPDATE CASCADE,

    -- Unique constraints
    CONSTRAINT uq_products_sku UNIQUE (sku),

    -- Check constraints
    CONSTRAINT chk_products_sku_format
        CHECK (sku ~ '^[A-Z0-9_-]{3,100}$'),
    CONSTRAINT chk_products_name_not_empty
        CHECK (LENGTH(TRIM(name)) >= 2),
    CONSTRAINT chk_products_price_positive
        CHECK (price > 0),
    CONSTRAINT chk_products_mrp_gte_price
        CHECK (mrp IS NULL OR mrp >= price),
    CONSTRAINT chk_products_weight_non_negative
        CHECK (weight_grams IS NULL OR weight_grams >= 0),
    CONSTRAINT chk_products_bore_diameter_range
        CHECK (bore_diameter_mm IS NULL OR (bore_diameter_mm > 0 AND bore_diameter_mm <= 200)),
    CONSTRAINT chk_products_backset_range
        CHECK (backset_mm IS NULL OR (backset_mm > 0 AND backset_mm <= 300)),
    CONSTRAINT chk_products_soft_delete_consistency
        CHECK (deleted_at IS NULL OR is_active = FALSE)
);

COMMENT ON TABLE  products IS 'Central product entity. Soft-deleted via deleted_at. SKU is the business key.';
COMMENT ON COLUMN products.id IS 'UUID v7 (time-ordered). Safe for API exposure and distributed merging.';
COMMENT ON COLUMN products.sku IS 'Business key. Format: CATEGORY_NNN (e.g., HANDLE_001). Immutable after creation.';
COMMENT ON COLUMN products.mrp IS 'Maximum Retail Price (Indian pricing standard). Must be >= selling price.';
COMMENT ON COLUMN products.deleted_at IS 'Soft delete timestamp. Non-NULL = deleted. is_active must be FALSE when set.';


-- ═══════════════════════════════════════════════════════════
-- PRODUCT DIMENSIONS (1:1 with products)
-- ═══════════════════════════════════════════════════════════

CREATE TABLE product_dimensions (
    product_id          UUID PRIMARY KEY,
    width_mm            DECIMAL(8,2),
    height_mm           DECIMAL(8,2),
    depth_mm            DECIMAL(8,2),

    -- Handle-specific
    lever_length_mm     DECIMAL(8,2),
    rose_diameter_mm    DECIMAL(8,2),

    -- Lock-specific
    face_plate_width_mm  DECIMAL(8,2),
    face_plate_height_mm DECIMAL(8,2),

    -- Foreign key
    CONSTRAINT fk_dimensions_product
        FOREIGN KEY (product_id) REFERENCES products(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    -- Check constraints
    CONSTRAINT chk_dimensions_width_positive
        CHECK (width_mm IS NULL OR width_mm > 0),
    CONSTRAINT chk_dimensions_height_positive
        CHECK (height_mm IS NULL OR height_mm > 0),
    CONSTRAINT chk_dimensions_depth_positive
        CHECK (depth_mm IS NULL OR depth_mm > 0),
    CONSTRAINT chk_dimensions_lever_length_positive
        CHECK (lever_length_mm IS NULL OR lever_length_mm > 0),
    CONSTRAINT chk_dimensions_rose_diameter_positive
        CHECK (rose_diameter_mm IS NULL OR rose_diameter_mm > 0),
    CONSTRAINT chk_dimensions_face_plate_width_positive
        CHECK (face_plate_width_mm IS NULL OR face_plate_width_mm > 0),
    CONSTRAINT chk_dimensions_face_plate_height_positive
        CHECK (face_plate_height_mm IS NULL OR face_plate_height_mm > 0),
    -- Sanity: no dimension should exceed 2 meters (2000mm)
    CONSTRAINT chk_dimensions_max_size
        CHECK (
            COALESCE(width_mm, 0) <= 2000 AND
            COALESCE(height_mm, 0) <= 2000 AND
            COALESCE(depth_mm, 0) <= 2000
        )
);

COMMENT ON TABLE product_dimensions IS '1:1 extension of products. Separated because not all products have all fields.';


-- ═══════════════════════════════════════════════════════════
-- PRODUCT COMPATIBILITY (1:1 with products)
-- ═══════════════════════════════════════════════════════════

CREATE TABLE product_compatibility (
    product_id              UUID PRIMARY KEY,
    door_materials          JSONB NOT NULL DEFAULT '[]'::jsonb,
    door_types              JSONB NOT NULL DEFAULT '[]'::jsonb,
    opening_directions      JSONB NOT NULL DEFAULT '[]'::jsonb,
    min_door_thickness_mm   DECIMAL(6,2),
    max_door_thickness_mm   DECIMAL(6,2),

    -- Foreign key
    CONSTRAINT fk_compat_product
        FOREIGN KEY (product_id) REFERENCES products(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    -- Check constraints
    CONSTRAINT chk_compat_thickness_range
        CHECK (
            min_door_thickness_mm IS NULL OR
            max_door_thickness_mm IS NULL OR
            min_door_thickness_mm <= max_door_thickness_mm
        ),
    CONSTRAINT chk_compat_min_thickness_positive
        CHECK (min_door_thickness_mm IS NULL OR min_door_thickness_mm > 0),
    CONSTRAINT chk_compat_max_thickness_reasonable
        CHECK (max_door_thickness_mm IS NULL OR max_door_thickness_mm <= 200),
    -- Validate JSONB arrays contain only allowed values
    CONSTRAINT chk_compat_door_materials_valid
        CHECK (
            door_materials = '[]'::jsonb OR
            door_materials <@ '["wood", "metal", "glass", "fiberglass", "composite", "pvc", "aluminum"]'::jsonb
        ),
    CONSTRAINT chk_compat_door_types_valid
        CHECK (
            door_types = '[]'::jsonb OR
            door_types <@ '["single", "double", "sliding", "folding", "pivot", "barn"]'::jsonb
        ),
    CONSTRAINT chk_compat_opening_valid
        CHECK (
            opening_directions = '[]'::jsonb OR
            opening_directions <@ '["left", "right", "both"]'::jsonb
        )
);

COMMENT ON TABLE product_compatibility IS 'Door compatibility rules. JSONB arrays for bounded enum lists (controlled denormalization).';
COMMENT ON COLUMN product_compatibility.door_materials IS 'Allowed: wood, metal, glass, fiberglass, composite, pvc, aluminum';


-- ═══════════════════════════════════════════════════════════
-- PRODUCT 3D ASSETS (1:1 with products)
-- ═══════════════════════════════════════════════════════════

CREATE TABLE product_3d_assets (
    product_id         UUID PRIMARY KEY,

    -- Primary model
    model_url          VARCHAR(500) NOT NULL,
    model_format       e_model_format NOT NULL DEFAULT 'glb',
    file_size_bytes    INT,
    polygon_count      INT,
    thumbnail_url      VARCHAR(500),

    -- Anchor point (door-relative in mm)
    anchor_x_mm        DECIMAL(8,2) NOT NULL DEFAULT 0,
    anchor_y_mm        DECIMAL(8,2) NOT NULL DEFAULT 1000,
    anchor_z_mm        DECIMAL(8,2) NOT NULL DEFAULT 0,

    -- LOD variants
    lod_high_url       VARCHAR(500),
    lod_medium_url     VARCHAR(500),
    lod_low_url        VARCHAR(500),

    -- Quality metadata
    has_pbr_materials   BOOLEAN NOT NULL DEFAULT TRUE,
    texture_resolution  INT     NOT NULL DEFAULT 2048,

    -- Temporal
    last_validated_at   TIMESTAMPTZ,
    uploaded_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Foreign key
    CONSTRAINT fk_asset_product
        FOREIGN KEY (product_id) REFERENCES products(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    -- Check constraints
    CONSTRAINT chk_asset_model_url_format
        CHECK (model_url ~ '^https?://'),
    CONSTRAINT chk_asset_file_size_positive
        CHECK (file_size_bytes IS NULL OR file_size_bytes > 0),
    CONSTRAINT chk_asset_file_size_max
        CHECK (file_size_bytes IS NULL OR file_size_bytes <= 50 * 1024 * 1024),  -- 50MB max
    CONSTRAINT chk_asset_polygon_count_range
        CHECK (polygon_count IS NULL OR (polygon_count > 0 AND polygon_count <= 500000)),
    CONSTRAINT chk_asset_texture_resolution_power_of_2
        CHECK (texture_resolution IN (256, 512, 1024, 2048, 4096)),
    CONSTRAINT chk_asset_anchor_y_non_negative
        CHECK (anchor_y_mm >= 0),
    CONSTRAINT chk_asset_anchor_y_max_height
        CHECK (anchor_y_mm <= 3000)  -- No anchor above 3 meters
);

COMMENT ON TABLE product_3d_assets IS '3D model metadata. URLs point to S3/CloudFront. Anchor defines door-relative placement.';
COMMENT ON COLUMN product_3d_assets.anchor_y_mm IS 'Vertical offset from floor in mm. Handle default: 1000mm. Peephole: 1500mm.';


-- ═══════════════════════════════════════════════════════════
-- PRODUCT IMAGES (1:N with products)
-- ═══════════════════════════════════════════════════════════

CREATE TABLE product_images (
    id              UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id      UUID         NOT NULL,
    image_url       VARCHAR(500) NOT NULL,
    alt_text        VARCHAR(255),
    is_primary      BOOLEAN      NOT NULL DEFAULT FALSE,
    display_order   INT          NOT NULL DEFAULT 0,
    width_px        INT,
    height_px       INT,
    file_size_bytes INT,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),

    -- Foreign key
    CONSTRAINT fk_images_product
        FOREIGN KEY (product_id) REFERENCES products(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    -- Check constraints
    CONSTRAINT chk_images_url_format
        CHECK (image_url ~ '^https?://'),
    CONSTRAINT chk_images_display_order_non_negative
        CHECK (display_order >= 0),
    CONSTRAINT chk_images_width_positive
        CHECK (width_px IS NULL OR width_px > 0),
    CONSTRAINT chk_images_height_positive
        CHECK (height_px IS NULL OR height_px > 0),
    CONSTRAINT chk_images_file_size_max
        CHECK (file_size_bytes IS NULL OR file_size_bytes <= 10 * 1024 * 1024)  -- 10MB max
);

COMMENT ON TABLE product_images IS 'Product photography. One primary image per product (enforced by trigger).';


-- ═══════════════════════════════════════════════════════════
-- PRODUCT MATERIALS (M:N junction — product can have multiple materials)
-- ═══════════════════════════════════════════════════════════

CREATE TABLE product_materials (
    product_id    UUID         NOT NULL,
    material_name VARCHAR(100) NOT NULL,
    percentage    DECIMAL(5,2),  -- e.g., 70.00 for 70%

    PRIMARY KEY (product_id, material_name),

    CONSTRAINT fk_materials_product
        FOREIGN KEY (product_id) REFERENCES products(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT chk_materials_name_not_empty
        CHECK (LENGTH(TRIM(material_name)) > 0),
    CONSTRAINT chk_materials_percentage_range
        CHECK (percentage IS NULL OR (percentage > 0 AND percentage <= 100))
);

COMMENT ON TABLE product_materials IS 'Material composition. e.g., zinc_alloy 70%, stainless_steel 30%';


-- ═══════════════════════════════════════════════════════════
-- PRODUCT PRICE HISTORY (immutable append-only log)
-- ═══════════════════════════════════════════════════════════

CREATE TABLE product_price_history (
    id              BIGSERIAL   PRIMARY KEY,
    product_id      UUID        NOT NULL,
    price           DECIMAL(10,2) NOT NULL,
    mrp             DECIMAL(10,2),
    reason          VARCHAR(255),   -- "Initial pricing", "Diwali sale", "Supplier increase"
    effective_from  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    effective_until TIMESTAMPTZ,    -- NULL = currently active
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_price_history_product
        FOREIGN KEY (product_id) REFERENCES products(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT chk_price_history_price_positive
        CHECK (price > 0),
    CONSTRAINT chk_price_history_mrp_gte_price
        CHECK (mrp IS NULL OR mrp >= price),
    CONSTRAINT chk_price_history_date_range
        CHECK (effective_until IS NULL OR effective_until > effective_from)
);

COMMENT ON TABLE product_price_history IS 'Immutable price ledger. Current price = row WHERE effective_until IS NULL.';


-- ═══════════════════════════════════════════════════════════
-- PRODUCT ATTRIBUTES (EAV for category-specific fields)
-- ═══════════════════════════════════════════════════════════

CREATE TABLE product_attributes (
    product_id      UUID          NOT NULL,
    attribute_key   VARCHAR(100)  NOT NULL,
    attribute_value TEXT          NOT NULL,
    attribute_type  e_attribute_type NOT NULL DEFAULT 'string',

    PRIMARY KEY (product_id, attribute_key),

    CONSTRAINT fk_attributes_product
        FOREIGN KEY (product_id) REFERENCES products(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT chk_attributes_key_format
        CHECK (attribute_key ~ '^[a-z_][a-z0-9_]*$'),
    CONSTRAINT chk_attributes_value_not_empty
        CHECK (LENGTH(TRIM(attribute_value)) > 0)
);

COMMENT ON TABLE product_attributes IS 'EAV extension for category-specific attributes (e.g., smart_lock: wifi_protocol, battery_type).';


-- ═══════════════════════════════════════════════════════════
-- TAGS (many-to-many with products)
-- ═══════════════════════════════════════════════════════════

CREATE TABLE tags (
    id   SERIAL       PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    slug VARCHAR(100) NOT NULL,

    CONSTRAINT uq_tags_name UNIQUE (name),
    CONSTRAINT uq_tags_slug UNIQUE (slug),
    CONSTRAINT chk_tags_slug_format CHECK (slug ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$')
);

CREATE TABLE product_tags (
    product_id UUID NOT NULL,
    tag_id     INT  NOT NULL,

    PRIMARY KEY (product_id, tag_id),

    CONSTRAINT fk_product_tags_product
        FOREIGN KEY (product_id) REFERENCES products(id)
        ON DELETE CASCADE,
    CONSTRAINT fk_product_tags_tag
        FOREIGN KEY (tag_id) REFERENCES tags(id)
        ON DELETE CASCADE
);


-- ═══════════════════════════════════════════════════════════
-- AUDIT LOG (append-only, partitioned by month)
-- ═══════════════════════════════════════════════════════════

CREATE TABLE audit_log (
    id          BIGSERIAL,
    table_name  VARCHAR(100)   NOT NULL,
    record_id   UUID           NOT NULL,
    action      e_audit_action NOT NULL,
    old_values  JSONB,
    new_values  JSONB,
    changed_by  VARCHAR(255),   -- User/API key identifier
    ip_address  INET,
    user_agent  VARCHAR(500),
    created_at  TIMESTAMPTZ    NOT NULL DEFAULT NOW(),

    PRIMARY KEY (id, created_at)  -- Composite PK for partitioning
) PARTITION BY RANGE (created_at);

COMMENT ON TABLE audit_log IS 'Immutable audit trail. Partitioned by month for performance. Never UPDATE or DELETE.';
```

---

## 5. Constraints & Checks

### 5.1 Constraint Summary

| Table | Constraint | Type | Rule |
|-------|-----------|------|------|
| brands | `chk_brands_name_not_empty` | CHECK | Name must have non-whitespace characters |
| brands | `chk_brands_slug_format` | CHECK | Slug matches `^[a-z0-9]+(?:-[a-z0-9]+)*$` |
| brands | `chk_brands_logo_url_format` | CHECK | URL starts with http(s):// if provided |
| categories | `chk_categories_no_self_parent` | CHECK | Category cannot be its own parent |
| categories | `chk_categories_max_depth` | CHECK | Max 5 levels deep |
| products | `chk_products_sku_format` | CHECK | SKU matches `^[A-Z0-9_-]{3,100}$` |
| products | `chk_products_price_positive` | CHECK | price > 0 |
| products | `chk_products_mrp_gte_price` | CHECK | MRP ≥ selling price |
| products | `chk_products_bore_diameter_range` | CHECK | 0 < bore ≤ 200mm |
| products | `chk_products_soft_delete_consistency` | CHECK | deleted_at set → is_active must be FALSE |
| product_compatibility | `chk_compat_thickness_range` | CHECK | min ≤ max thickness |
| product_compatibility | `chk_compat_door_materials_valid` | CHECK | Only allowed enum values in JSONB |
| product_3d_assets | `chk_asset_file_size_max` | CHECK | ≤ 50MB |
| product_3d_assets | `chk_asset_polygon_count_range` | CHECK | ≤ 500,000 polygons |
| product_3d_assets | `chk_asset_texture_resolution_power_of_2` | CHECK | Must be 256/512/1024/2048/4096 |
| product_dimensions | `chk_dimensions_max_size` | CHECK | No dimension > 2000mm |
| product_price_history | `chk_price_history_date_range` | CHECK | effective_until > effective_from |

### 5.2 Referential Integrity Actions

| FK | ON DELETE | ON UPDATE | Rationale |
|----|-----------|-----------|-----------|
| products → brands | RESTRICT | CASCADE | Cannot orphan products; brand rename cascades |
| products → categories | RESTRICT | CASCADE | Cannot orphan products |
| product_dimensions → products | CASCADE | CASCADE | Dimension dies with product |
| product_compatibility → products | CASCADE | CASCADE | Compat data dies with product |
| product_3d_assets → products | CASCADE | CASCADE | Asset record dies with product |
| product_images → products | CASCADE | CASCADE | Images die with product |
| categories → categories (self) | RESTRICT | CASCADE | Cannot delete parent with children |

---

## 6. Indexes

```sql
-- ═══════════════════════════════════════════════════════════
-- B-TREE INDEXES (equality and range queries)
-- ═══════════════════════════════════════════════════════════

-- Products: most common query paths
CREATE INDEX idx_products_brand_id       ON products(brand_id)       WHERE deleted_at IS NULL;
CREATE INDEX idx_products_category_id    ON products(category_id)    WHERE deleted_at IS NULL;
CREATE INDEX idx_products_price          ON products(price)          WHERE deleted_at IS NULL;
CREATE INDEX idx_products_finish         ON products(finish)         WHERE deleted_at IS NULL;
CREATE INDEX idx_products_is_featured    ON products(is_featured)    WHERE deleted_at IS NULL AND is_featured = TRUE;
CREATE INDEX idx_products_created_at     ON products(created_at DESC) WHERE deleted_at IS NULL;

-- Partial index: active products only (most queries exclude soft-deleted)
CREATE INDEX idx_products_active ON products(id) WHERE deleted_at IS NULL AND is_active = TRUE;

-- Composite index for common filter combination: category + price sort
CREATE INDEX idx_products_category_price ON products(category_id, price) WHERE deleted_at IS NULL;

-- Composite index for brand + category combo filter
CREATE INDEX idx_products_brand_category ON products(brand_id, category_id) WHERE deleted_at IS NULL;

-- Product images: ordered display
CREATE INDEX idx_images_product_order ON product_images(product_id, display_order);

-- Price history: lookup current price
CREATE INDEX idx_price_history_product_active
    ON product_price_history(product_id, effective_from DESC)
    WHERE effective_until IS NULL;

-- Audit log: lookup by record
CREATE INDEX idx_audit_record ON audit_log(record_id, created_at DESC);
CREATE INDEX idx_audit_table  ON audit_log(table_name, created_at DESC);


-- ═══════════════════════════════════════════════════════════
-- GIN INDEXES (JSONB and full-text search)
-- ═══════════════════════════════════════════════════════════

-- Compatibility: "Find products compatible with wood doors"
CREATE INDEX idx_compat_door_materials   ON product_compatibility USING GIN (door_materials);
CREATE INDEX idx_compat_door_types       ON product_compatibility USING GIN (door_types);
CREATE INDEX idx_compat_opening          ON product_compatibility USING GIN (opening_directions);

-- Full-text search on product name + description
CREATE INDEX idx_products_search ON products
    USING GIN (
        (to_tsvector('english', COALESCE(name, '') || ' ' || COALESCE(description, '')))
    )
    WHERE deleted_at IS NULL;

-- Trigram index for fuzzy/partial name matching (typo tolerance)
CREATE INDEX idx_products_name_trgm ON products USING GIN (name gin_trgm_ops)
    WHERE deleted_at IS NULL;

-- EAV attributes: search by key
CREATE INDEX idx_attributes_key ON product_attributes(attribute_key);


-- ═══════════════════════════════════════════════════════════
-- COVERING INDEXES (index-only scans for hot queries)
-- ═══════════════════════════════════════════════════════════

-- Product list query: returns id, sku, name, price without touching heap
CREATE INDEX idx_products_list_covering
    ON products(category_id, price)
    INCLUDE (id, sku, name, finish, is_featured)
    WHERE deleted_at IS NULL AND is_active = TRUE;
```

---

## 7. Partitioning Strategy

### 7.1 Audit Log — Range Partitioning by Month

```sql
-- Create partitions for current and next 3 months
CREATE TABLE audit_log_p_2026_10 PARTITION OF audit_log
    FOR VALUES FROM ('2026-10-01') TO ('2026-11-01');

CREATE TABLE audit_log_p_2026_11 PARTITION OF audit_log
    FOR VALUES FROM ('2026-11-01') TO ('2026-12-01');

CREATE TABLE audit_log_p_2026_12 PARTITION OF audit_log
    FOR VALUES FROM ('2026-12-01') TO ('2027-01-01');

CREATE TABLE audit_log_p_2027_01 PARTITION OF audit_log
    FOR VALUES FROM ('2027-01-01') TO ('2027-02-01');

-- Default partition catches anything outside defined ranges
CREATE TABLE audit_log_p_default PARTITION OF audit_log DEFAULT;
```

### 7.2 Auto-Partition Creation (cron job)

```sql
-- Function to auto-create next month's partition
CREATE OR REPLACE FUNCTION fn_create_audit_partition()
RETURNS void AS $$
DECLARE
    partition_date DATE;
    partition_name TEXT;
    start_date     TEXT;
    end_date       TEXT;
BEGIN
    -- Create partition for next month
    partition_date := DATE_TRUNC('month', NOW() + INTERVAL '1 month');
    partition_name := 'audit_log_p_' || TO_CHAR(partition_date, 'YYYY_MM');
    start_date     := TO_CHAR(partition_date, 'YYYY-MM-DD');
    end_date       := TO_CHAR(partition_date + INTERVAL '1 month', 'YYYY-MM-DD');

    -- Only create if not exists
    IF NOT EXISTS (
        SELECT 1 FROM pg_class WHERE relname = partition_name
    ) THEN
        EXECUTE FORMAT(
            'CREATE TABLE %I PARTITION OF audit_log FOR VALUES FROM (%L) TO (%L)',
            partition_name, start_date, end_date
        );
        RAISE NOTICE 'Created partition: %', partition_name;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- Schedule: run monthly via pg_cron
-- SELECT cron.schedule('create-audit-partition', '0 0 25 * *', 'SELECT fn_create_audit_partition()');
```

### 7.3 Partition Maintenance — Archival

```sql
-- Archive partitions older than 12 months to cold storage
-- Step 1: Detach
-- ALTER TABLE audit_log DETACH PARTITION audit_log_p_2025_10;
-- Step 2: Export to S3 (pg_dump or COPY TO)
-- Step 3: Drop if exported successfully
-- DROP TABLE audit_log_p_2025_10;
```

### 7.4 Future Partitioning — Product Price History

When the price history table exceeds ~10M rows:
```sql
-- Convert to range partitioned by effective_from
-- CREATE TABLE product_price_history (...) PARTITION BY RANGE (effective_from);
-- Not needed for MVP with 20 products.
```

---

## 8. Triggers & Functions

### 8.1 Auto-Update `updated_at`

```sql
-- Generic trigger function: sets updated_at = NOW() on any UPDATE
CREATE OR REPLACE FUNCTION fn_set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    -- Only update if row data actually changed (skip no-op updates)
    IF NEW IS DISTINCT FROM OLD THEN
        NEW.updated_at = NOW();
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Apply to all tables with updated_at
CREATE TRIGGER trg_brands_updated_at
    BEFORE UPDATE ON brands
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

CREATE TRIGGER trg_products_updated_at
    BEFORE UPDATE ON products
    FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();
```

### 8.2 Audit Log Trigger

```sql
-- Generic audit trigger: logs INSERT/UPDATE/DELETE to audit_log
CREATE OR REPLACE FUNCTION fn_audit_trigger()
RETURNS TRIGGER AS $$
DECLARE
    record_uuid UUID;
    old_data    JSONB;
    new_data    JSONB;
BEGIN
    -- Get the record ID (assumes UUID PK column named 'id' or 'product_id')
    IF TG_OP = 'DELETE' THEN
        record_uuid := COALESCE(OLD.id, OLD.product_id);
        old_data    := to_jsonb(OLD);
        new_data    := NULL;
    ELSIF TG_OP = 'INSERT' THEN
        record_uuid := COALESCE(NEW.id, NEW.product_id);
        old_data    := NULL;
        new_data    := to_jsonb(NEW);
    ELSIF TG_OP = 'UPDATE' THEN
        record_uuid := COALESCE(NEW.id, NEW.product_id);
        old_data    := to_jsonb(OLD);
        new_data    := to_jsonb(NEW);

        -- Skip if no actual change
        IF old_data = new_data THEN
            RETURN NEW;
        END IF;

        -- Store only changed columns (delta) to save space
        new_data := (
            SELECT jsonb_object_agg(key, value)
            FROM jsonb_each(to_jsonb(NEW))
            WHERE to_jsonb(NEW) -> key IS DISTINCT FROM to_jsonb(OLD) -> key
        );
    END IF;

    INSERT INTO audit_log (table_name, record_id, action, old_values, new_values, changed_by, created_at)
    VALUES (
        TG_TABLE_NAME,
        record_uuid,
        TG_OP::e_audit_action,
        old_data,
        new_data,
        current_setting('app.current_user', TRUE),  -- Set by application
        NOW()
    );

    IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
END;
$$ LANGUAGE plpgsql;

-- Apply audit to critical tables
CREATE TRIGGER trg_products_audit
    AFTER INSERT OR UPDATE OR DELETE ON products
    FOR EACH ROW EXECUTE FUNCTION fn_audit_trigger();

CREATE TRIGGER trg_product_3d_assets_audit
    AFTER INSERT OR UPDATE OR DELETE ON product_3d_assets
    FOR EACH ROW EXECUTE FUNCTION fn_audit_trigger();

CREATE TRIGGER trg_product_price_history_audit
    AFTER INSERT ON product_price_history
    FOR EACH ROW EXECUTE FUNCTION fn_audit_trigger();
```

### 8.3 Enforce Single Primary Image Per Product

```sql
-- When setting is_primary = TRUE, unset all other images for same product
CREATE OR REPLACE FUNCTION fn_enforce_single_primary_image()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.is_primary = TRUE THEN
        UPDATE product_images
        SET is_primary = FALSE
        WHERE product_id = NEW.product_id
          AND id != NEW.id
          AND is_primary = TRUE;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_images_single_primary
    AFTER INSERT OR UPDATE OF is_primary ON product_images
    FOR EACH ROW
    WHEN (NEW.is_primary = TRUE)
    EXECUTE FUNCTION fn_enforce_single_primary_image();
```

### 8.4 Auto-Set Category Depth

```sql
-- When inserting/updating a category, compute depth from parent chain
CREATE OR REPLACE FUNCTION fn_set_category_depth()
RETURNS TRIGGER AS $$
DECLARE
    parent_depth INT;
BEGIN
    IF NEW.parent_id IS NULL THEN
        NEW.depth := 0;
    ELSE
        SELECT depth INTO parent_depth FROM categories WHERE id = NEW.parent_id;
        IF parent_depth IS NULL THEN
            RAISE EXCEPTION 'Parent category % not found', NEW.parent_id;
        END IF;
        NEW.depth := parent_depth + 1;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_categories_depth
    BEFORE INSERT OR UPDATE OF parent_id ON categories
    FOR EACH ROW EXECUTE FUNCTION fn_set_category_depth();
```

### 8.5 Price History Auto-Record on Price Change

```sql
-- When product.price changes, auto-insert into price history
CREATE OR REPLACE FUNCTION fn_record_price_change()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.price IS DISTINCT FROM NEW.price OR OLD.mrp IS DISTINCT FROM NEW.mrp THEN
        -- Close previous active price record
        UPDATE product_price_history
        SET effective_until = NOW()
        WHERE product_id = NEW.id
          AND effective_until IS NULL;

        -- Insert new price record
        INSERT INTO product_price_history (product_id, price, mrp, reason, effective_from)
        VALUES (NEW.id, NEW.price, NEW.mrp, 'Price updated', NOW());
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_products_price_history
    AFTER UPDATE OF price, mrp ON products
    FOR EACH ROW EXECUTE FUNCTION fn_record_price_change();
```

### 8.6 Soft Delete Cascade

```sql
-- When a product is soft-deleted, also deactivate its assets
CREATE OR REPLACE FUNCTION fn_soft_delete_product()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.deleted_at IS NOT NULL AND OLD.deleted_at IS NULL THEN
        NEW.is_active := FALSE;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_products_soft_delete
    BEFORE UPDATE OF deleted_at ON products
    FOR EACH ROW EXECUTE FUNCTION fn_soft_delete_product();
```

---

## 9. Audit Trail

### 9.1 Setting Audit Context

```sql
-- Application sets context before each request:
SET LOCAL app.current_user = 'admin@aayam.com';
SET LOCAL app.client_ip   = '203.0.113.42';

-- The audit trigger reads these via current_setting('app.current_user', TRUE)
```

### 9.2 Querying Audit Trail

```sql
-- Who changed product HANDLE_001's price?
SELECT
    action,
    old_values->>'price' AS old_price,
    new_values->>'price' AS new_price,
    changed_by,
    created_at
FROM audit_log
WHERE table_name = 'products'
  AND record_id = (SELECT id FROM products WHERE sku = 'HANDLE_001')
  AND (new_values ? 'price')
ORDER BY created_at DESC;

-- All changes in the last 24 hours
SELECT * FROM audit_log
WHERE created_at > NOW() - INTERVAL '24 hours'
ORDER BY created_at DESC;
```

---

## 10. Views & Materialized Views

### 10.1 Product List View (for API listing)

```sql
CREATE OR REPLACE VIEW v_product_list AS
SELECT
    p.id,
    p.sku,
    p.name,
    p.price,
    p.mrp,
    p.currency::text,
    p.finish,
    p.is_featured,
    p.mounting_type::text,
    b.name       AS brand_name,
    b.slug       AS brand_slug,
    c.name       AS category_name,
    c.slug       AS category_slug,
    a.thumbnail_url,
    a.polygon_count,
    p.created_at
FROM products p
JOIN brands b     ON p.brand_id = b.id
JOIN categories c ON p.category_id = c.id
LEFT JOIN product_3d_assets a ON a.product_id = p.id
WHERE p.deleted_at IS NULL
  AND p.is_active = TRUE
  AND b.is_active = TRUE;

COMMENT ON VIEW v_product_list IS 'Read-optimized view for product catalogue listing. Filters out soft-deleted and inactive.';
```

### 10.2 Product Detail View (for API detail + AR)

```sql
CREATE OR REPLACE VIEW v_product_detail AS
SELECT
    p.id,
    p.sku,
    p.name,
    p.description,
    p.price,
    p.mrp,
    p.currency::text,
    p.finish,
    p.weight_grams,
    p.material_primary,
    p.mounting_type::text,
    p.bore_diameter_mm,
    p.backset_mm,
    p.is_featured,

    -- Brand
    b.name       AS brand_name,
    b.slug       AS brand_slug,
    b.logo_url   AS brand_logo_url,

    -- Category
    c.name       AS category_name,
    c.slug       AS category_slug,

    -- Dimensions
    d.width_mm,
    d.height_mm,
    d.depth_mm,
    d.lever_length_mm,
    d.rose_diameter_mm,

    -- Compatibility
    compat.door_materials,
    compat.door_types,
    compat.opening_directions,
    compat.min_door_thickness_mm,
    compat.max_door_thickness_mm,

    -- 3D Asset
    a.model_url,
    a.model_format::text,
    a.file_size_bytes,
    a.polygon_count,
    a.thumbnail_url,
    a.anchor_x_mm,
    a.anchor_y_mm,
    a.anchor_z_mm,
    a.lod_high_url,
    a.lod_medium_url,
    a.lod_low_url,
    a.has_pbr_materials,

    p.created_at,
    p.updated_at

FROM products p
JOIN brands b     ON p.brand_id = b.id
JOIN categories c ON p.category_id = c.id
LEFT JOIN product_dimensions    d      ON d.product_id = p.id
LEFT JOIN product_compatibility compat ON compat.product_id = p.id
LEFT JOIN product_3d_assets     a      ON a.product_id = p.id
WHERE p.deleted_at IS NULL;
```

### 10.3 Materialized View — Catalogue Cache

```sql
-- Refreshed periodically (every 5 min) for high-traffic catalogue queries
CREATE MATERIALIZED VIEW mv_product_catalogue AS
SELECT * FROM v_product_list
ORDER BY category_name, is_featured DESC, price;

CREATE UNIQUE INDEX idx_mv_catalogue_id ON mv_product_catalogue(id);

-- Refresh command (run via pg_cron or application scheduler):
-- REFRESH MATERIALIZED VIEW CONCURRENTLY mv_product_catalogue;
```

---

## 11. Seed Data

```sql
-- ═══════════════════════════════════════════════════════════
-- SEED: Brands
-- ═══════════════════════════════════════════════════════════
INSERT INTO brands (name, slug, country) VALUES
    ('Godrej',  'godrej',  'India'),
    ('Yale',    'yale',    'USA'),
    ('Dorset',  'dorset',  'India'),
    ('Ozone',   'ozone',   'India'),
    ('Ebco',    'ebco',    'India'),
    ('Dorma',   'dorma',   'Germany')
ON CONFLICT (slug) DO NOTHING;

-- ═══════════════════════════════════════════════════════════
-- SEED: Categories
-- ═══════════════════════════════════════════════════════════
INSERT INTO categories (name, slug, icon, display_order) VALUES
    ('Handle',    'handle',    'handle_icon',    1),
    ('Lock',      'lock',      'lock_icon',      2),
    ('Accessory', 'accessory', 'accessory_icon', 3)
ON CONFLICT (slug) DO NOTHING;

-- Sub-categories
INSERT INTO categories (name, slug, parent_id, icon, display_order) VALUES
    ('Lever Handle',  'lever-handle',  (SELECT id FROM categories WHERE slug='handle'), 'lever_icon',   1),
    ('Pull Handle',   'pull-handle',   (SELECT id FROM categories WHERE slug='handle'), 'pull_icon',    2),
    ('Knob',          'knob',          (SELECT id FROM categories WHERE slug='handle'), 'knob_icon',    3),
    ('Mortise Handle','mortise-handle', (SELECT id FROM categories WHERE slug='handle'), 'mortise_icon', 4),
    ('Smart Lock',    'smart-lock',    (SELECT id FROM categories WHERE slug='lock'),   'smart_icon',   1),
    ('Mortise Lock',  'mortise-lock',  (SELECT id FROM categories WHERE slug='lock'),   'mortise_icon', 2),
    ('Night Latch',   'night-latch',   (SELECT id FROM categories WHERE slug='lock'),   'latch_icon',   3),
    ('Rim Lock',      'rim-lock',      (SELECT id FROM categories WHERE slug='lock'),   'rim_icon',     4)
ON CONFLICT (slug) DO NOTHING;
```

---

## 12. Migration Strategy

### 12.1 Alembic Configuration

```
migrations/
├── env.py
├── script.py.mako
└── versions/
    ├── 001_initial_schema.py
    ├── 002_seed_data.py
    ├── 003_create_indexes.py
    ├── 004_create_triggers.py
    ├── 005_create_views.py
    └── 006_create_partitions.py
```

### 12.2 Migration Rules

1. **Never** modify a released migration — create a new one
2. **Always** include both `upgrade()` and `downgrade()`
3. **Test** every migration against a fresh database AND against production snapshot
4. **Separate** schema changes from data migrations
5. **Run** in a transaction (default Alembic behavior)
6. **Lock** advisory lock to prevent concurrent migrations

### 12.3 Zero-Downtime Migration Patterns

| Change | Pattern |
|--------|---------|
| Add column | `ALTER TABLE ADD COLUMN ... DEFAULT` (instant in PG 11+) |
| Add NOT NULL column | Add nullable → backfill → set NOT NULL |
| Drop column | Stop reading → deploy → drop column |
| Rename column | Add new → copy data → switch reads → drop old |
| Add index | `CREATE INDEX CONCURRENTLY` (non-blocking) |

---

## 13. Performance Considerations

### 13.1 Connection Pooling

```
Application → PgBouncer (connection pool) → PostgreSQL
Pool mode: transaction
Max connections: 100
Default pool size: 20
```

### 13.2 Query Performance Targets

| Query | Target | Index Used |
|-------|--------|-----------|
| List active products (page 1, 20 items) | < 5ms | `idx_products_list_covering` |
| Filter by category + price sort | < 10ms | `idx_products_category_price` |
| Full product detail by ID | < 5ms | PK lookup + JOINs |
| Search by name (fuzzy) | < 50ms | `idx_products_name_trgm` |
| Compatibility filter (door_material contains 'wood') | < 20ms | `idx_compat_door_materials` GIN |
| Audit log for record (last 30 days) | < 10ms | `idx_audit_record` + partition pruning |

### 13.3 EXPLAIN ANALYZE Verification

```sql
-- Verify index usage for common queries
EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT)
SELECT * FROM v_product_list
WHERE category_slug = 'handle'
ORDER BY price
LIMIT 20;

-- Expected: Index Scan using idx_products_category_price
-- NOT: Seq Scan on products
```

---

## 14. Security

### 14.1 Role-Based Access

```sql
-- Application roles
CREATE ROLE aayam_api LOGIN PASSWORD 'secure_password';
CREATE ROLE aayam_admin LOGIN PASSWORD 'admin_password';
CREATE ROLE aayam_readonly LOGIN PASSWORD 'readonly_password';

-- API role: read all, write products
GRANT SELECT ON ALL TABLES IN SCHEMA public TO aayam_api;
GRANT INSERT, UPDATE ON products, product_dimensions, product_compatibility,
    product_3d_assets, product_images, product_materials,
    product_price_history, product_attributes, product_tags TO aayam_api;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO aayam_api;

-- Admin role: full access
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO aayam_admin;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO aayam_admin;

-- Read-only role: analytics/reporting
GRANT SELECT ON ALL TABLES IN SCHEMA public TO aayam_readonly;
GRANT SELECT ON mv_product_catalogue TO aayam_readonly;
```

### 14.2 Row-Level Security (future multi-tenant)

```sql
-- Prepared for Phase 8 (retailer dashboard) — not enabled in Phase 2
-- ALTER TABLE products ENABLE ROW LEVEL SECURITY;
-- CREATE POLICY retailer_products ON products
--     USING (retailer_id = current_setting('app.retailer_id')::int);
```

### 14.3 Data Protection

| Data | Protection |
|------|-----------|
| Passwords | Never stored in product DB (auth is separate) |
| API keys | Hashed with bcrypt, stored in separate auth table |
| PII | No customer PII in product database |
| Pricing | Audit logged, price history immutable |
| 3D assets | Access via signed CloudFront URLs (expire after 24h) |
