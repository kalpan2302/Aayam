"""Phase 3 Step 2 — fine-tune YOLOv8n on the door+handle dataset.

Trains locally via PyTorch MPS (Apple Silicon GPU) instead of Colab, since
this machine (M4) is capable enough for a nano-model fine-tune on ~1300
images. See docs/phases/phase-3-implementation-plan.md Step 2.
"""

from ultralytics import YOLO


def main() -> None:
    model = YOLO("yolov8n.pt")
    results = model.train(
        data="datasets/data.yaml",
        epochs=100,
        imgsz=640,
        batch=16,
        device="mps",
        project="ml/runs",
        name="door_handle_v1",
    )

    metrics = model.val()
    print(f"mAP50-95: {metrics.box.map}")
    print(f"mAP50: {metrics.box.map50}")
    print(f"per-class mAP50: {dict(zip(metrics.names.values(), metrics.box.maps))}")


if __name__ == "__main__":
    main()
