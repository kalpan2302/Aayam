# Phase 7: Compatibility Engine

**Duration:** 3-5 weeks (Month 6)

This phase implements the core Unique Selling Proposition (USP) of Aayam: the Compatibility Engine.

## Overview
- **Goal:** Determine if a product is compatible with the scanned door.
- **CRITICAL:** Use DETERMINISTIC RULES, not LLMs.
- LLMs can explain results later, but decisions must come from structured data and rules.

## The Compatibility Model

```text
Door Properties          Product Requirements
- Thickness: 40mm        - Req thickness: 35-50mm
- Material: Wood         - Materials: Wood/Metal
- Opening: Right         - Opening: Left/Right
- Bore: X                - Bore: X
```

**Result:**
- 🟢 Compatible
- 🟡 Additional information required
- 🔴 Appears incompatible

---

## Step 1: Rules Engine Design (Week 1)

- Define compatibility rules as structured data.
- **Rule categories:**
  1. **Dimensional compatibility**
     - Door thickness within product range
     - Door width sufficient for product
     - Bore diameter match
     - Backset compatibility
  2. **Material compatibility**
     - Product supports door material
     - Weight limits for door material
     - Screw type for material
  3. **Mechanical compatibility**
     - Opening direction
     - Handing (left/right)
     - Existing hardware conflicts
     - Mounting type compatibility
  4. **Regulatory/safety**
     - Fire door requirements
     - Accessibility requirements
     - Grade/security level

### Rules Engine Implementation

```python
from typing import Callable, Any, Dict, List
from enum import Enum
from pydantic import BaseModel

class Status(str, Enum):
    PASS = "pass"
    FAIL = "fail"
    WARNING = "warning"
    INFO = "info"

class RuleResult(BaseModel):
    status: Status
    detail: str
    confidence: float = 1.0

class CompatibilityRule:
    def __init__(
        self,
        name: str,
        category: str,
        check: Callable[[Dict[str, Any], Dict[str, Any]], RuleResult],
        severity: str,
        message_pass: str,
        message_fail: str
    ):
        self.name = name
        self.category = category
        self.check = check
        self.severity = severity
        self.message_pass = message_pass
        self.message_fail = message_fail

    def evaluate(self, door: Dict[str, Any], product: Dict[str, Any]) -> RuleResult:
        try:
            return self.check(door, product)
        except Exception as e:
            return RuleResult(status=Status.WARNING, detail=f"Check failed due to missing data: {str(e)}", confidence=0.0)

class CompatibilityEngine:
    def __init__(self):
        self.rules: List[CompatibilityRule] = []

    def add_rule(self, rule: CompatibilityRule):
        self.rules.append(rule)

    def evaluate(self, door: Dict[str, Any], product: Dict[str, Any]) -> Dict[str, Any]:
        results = []
        overall_confidence = 1.0
        overall_status = "compatible"

        for rule in self.rules:
            res = rule.evaluate(door, product)
            results.append({
                "rule": rule.name,
                "category": rule.category,
                "status": res.status.value,
                "detail": res.detail,
                "confidence": res.confidence
            })
            overall_confidence *= res.confidence
            if res.status == Status.FAIL and rule.severity == "critical":
                overall_status = "incompatible"
            elif res.status == Status.WARNING and overall_status != "incompatible":
                overall_status = "needs_info"

        return {
            "overall_status": overall_status,
            "confidence": overall_confidence,
            "checks": results,
            "warnings": [r for r in results if r["status"] == Status.WARNING.value],
            "recommendations": []
        }
```

---

## Step 2: Compatibility API (Week 2)

**FastAPI Endpoint:**
`POST /api/v1/compatibility/check`

**Request Body:**
```json
{
  "door": {
    "width_mm": 900,
    "height_mm": 2100,
    "thickness_mm": 40,
    "material": "wood",
    "opening_direction": "right",
    "existing_bore_mm": 54
  },
  "product_id": "HANDLE_001"
}
```

**Response:**
```json
{
  "overall_status": "compatible",
  "confidence": 0.95,
  "checks": [
    {"rule": "thickness", "status": "pass", "detail": "40mm within 35-50mm range"},
    {"rule": "material", "status": "pass", "detail": "Wood is supported"},
    {"rule": "opening", "status": "pass", "detail": "Right-hand compatible"},
    {"rule": "bore", "status": "pass", "detail": "54mm bore matches"}
  ],
  "warnings": [],
  "recommendations": ["Professional installation recommended"]
}
```

### FastAPI Implementation
```python
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import Optional

app = FastAPI()
engine = CompatibilityEngine()
# ... engine rules initialization ...

class DoorModel(BaseModel):
    width_mm: float
    height_mm: float
    thickness_mm: float
    material: str
    opening_direction: str
    existing_bore_mm: Optional[float] = None

class ProductCheckRequest(BaseModel):
    door: DoorModel
    product_id: str

@app.post("/api/v1/compatibility/check")
async def check_compatibility(request: ProductCheckRequest):
    # Mock product fetch
    product = get_product_by_id(request.product_id)
    if not product:
        raise HTTPException(status_code=404, detail="Product not found")
    
    result = engine.evaluate(request.door.dict(), product)
    return result
```

---

## Step 3: Confidence Scoring (Week 2-3)

- **Per-check confidence:** Measured value confidence * rule match confidence.
- **Overall confidence:** Weighted product of all individual checks.
- **Confidence thresholds:**
  - `> 0.85`: 🟢 Compatible
  - `0.60 - 0.85`: 🟡 Likely compatible (needs verification)
  - `< 0.60`: 🔴 Appears incompatible
- **Measurement uncertainty propagation:** Incorporate AR measurement variance (e.g., ±2mm) into the rule confidence.

---

## Step 4: Product Recommendation (Week 3)

- **Goal:** Given door properties, find all compatible products in the catalog.
- **Ranking Logic:**
  1. Compatibility score (highest first)
  2. Price (match user preference/budget)
  3. Popularity/Rating
  4. Material match (e.g., matching finishes)
- **Filters:** Category, price range, brand, finish.

**API Endpoint:** `POST /api/v1/compatibility/recommend`

---

## Step 5: UI Integration (Week 4)

- **Compatibility Indicator:** Add 🟢, 🟡, 🔴 indicators directly on product cards in the catalog.
- **Detailed Report Screen:** Tapping the indicator opens a bottom sheet or screen showing the breakdown.
- **Interactive:** Tap each check for details (e.g., "Why did this fail?").
- **Warning Cards:** Display actionable info when a check fails (e.g., "Requires an adapter plate").

### UI Mockup Description
- **Product Details:** Top right corner shows a shield icon with color.
- **Compatibility Drawer:**
  - Header: "Compatibility: 95% Match"
  - List: Green checkmarks for Thickness, Handing. Yellow warning for Bore size ("Might require drilling").
  - Footer: "Buy Adapter Kit" button.

---

## Step 6: Manual Verification Prompts (Week 4)

- **When confidence is medium (0.60 - 0.85):**
  - Ask user to verify specific measurements manually.
  - Ask user to check existing hardware or door frames.
  - Provide guidance photos (e.g., "Measure this part of the door").
- **Flow:** User gets a prompt -> Inputs manual value -> Recalculate compatibility.

---

## Step 7: LLM Explanation Layer (Week 5, Optional)

- **Concept:** After deterministic checks, send results to an LLM to generate a natural, readable explanation.
- **Example output:** "This lock appears suitable because your door thickness (40mm) is well within the manufacturer's specified range (35-50mm), and the right-hand opening matches your door configuration."
- **Status:** Keep optional for MVP.

**Prompt Engineering Example:**
```text
Given the following compatibility check results:
{results_json}
Write a concise, friendly 2-sentence explanation for the user on why this product fits or doesn't fit their door. Avoid technical jargon where possible.
```

---

## Compatibility Rules Database

### Sample Rules

```python
def check_thickness(door, product):
    req = product.get('req_thickness', {})
    val = door.get('thickness_mm')
    if req.get('min') <= val <= req.get('max'):
        return RuleResult(status=Status.PASS, detail=f"Thickness {val}mm is within range")
    return RuleResult(status=Status.FAIL, detail=f"Thickness {val}mm is outside {req.get('min')}-{req.get('max')}mm")

def check_material(door, product):
    supported = product.get('supported_materials', [])
    if door.get('material') in supported:
        return RuleResult(status=Status.PASS, detail=f"Material {door.get('material')} is supported")
    return RuleResult(status=Status.FAIL, detail="Material not supported by product")

def check_handing(door, product):
    door_handing = door.get('opening_direction')
    prod_handing = product.get('handing')
    if prod_handing == 'universal' or door_handing == prod_handing:
        return RuleResult(status=Status.PASS, detail="Handing matches")
    return RuleResult(status=Status.FAIL, detail=f"Product requires {prod_handing} door")

# 15+ more rules covering bore, backset, weight, fire-rating, etc.
```

---

## Edge Cases

- **Unknown door material:** Rule checks for material fallback to `Status.WARNING`.
- **Non-standard door dimensions:** Triggers a flag for manual verification or custom order recommendation.
- **Missing measurements:** Any rule requiring a missing measurement returns `needs_info`.
- **Multiple products on same door:** E.g., a smart lock and a separate handle. Check for physical overlap/clearance.
- **Conflicting products:** Highlight if two selected products cannot be installed together.

---

## Deliverables

- [x] Rules engine implemented
- [ ] 30+ compatibility rules defined
- [x] Compatibility API endpoint
- [x] Confidence scoring system
- [x] Product recommendation engine
- [x] UI integration (compatibility indicators)
- [x] Manual verification flow
- [x] LLM explanation (optional)
- [ ] Test suite with 50+ test cases

### Test Suite Example

```python
import unittest

class TestCompatibility(unittest.TestCase):
    def setUp(self):
        self.engine = CompatibilityEngine()
        self.engine.add_rule(CompatibilityRule(
            "thickness", "dimensional", check_thickness, "critical", "Pass", "Fail"
        ))

    def test_thickness_pass(self):
        door = {"thickness_mm": 40}
        product = {"req_thickness": {"min": 35, "max": 50}}
        result = self.engine.evaluate(door, product)
        self.assertEqual(result["overall_status"], "compatible")

    def test_thickness_fail(self):
        door = {"thickness_mm": 55}
        product = {"req_thickness": {"min": 35, "max": 50}}
        result = self.engine.evaluate(door, product)
        self.assertEqual(result["overall_status"], "incompatible")
```
