"""Phase 3 Step 8 — fine-tune YOLOv8n-seg on door+handle segmentation masks.

Masks are SAM-generated from the existing box-annotated dataset's ground-truth
boxes (box-to-mask promotion, not hand-labeled polygons), then simplified via
cv2.approxPolyDP. See docs/phases/phase-3-implementation-plan.md Step 8 for
the full pipeline and the quality-filtering attempt that was tried and
abandoned (geometric heuristics didn't correlate with actual mask quality).

Mirrors ml/train_door_handle.py's approach (local PyTorch MPS, not Colab).
"""

from ultralytics import YOLO


def main() -> None:
    model = YOLO("yolov8n-seg.pt")
    results = model.train(
        data="datasets_seg_clean/data.yaml",
        epochs=100,
        imgsz=640,
        batch=16,
        device="mps",
        project="ml/runs",
        name="door_handle_seg_v1",
    )

    metrics = model.val()
    print(f"Box mAP50-95: {metrics.box.map}")
    print(f"Box mAP50: {metrics.box.map50}")
    print(f"Mask mAP50-95: {metrics.seg.map}")
    print(f"Mask mAP50: {metrics.seg.map50}")
    print(f"per-class mask mAP50: {dict(zip(metrics.names.values(), metrics.seg.maps))}")


if __name__ == "__main__":
    main()
