"""Resume Phase 3 Step 8 segmentation training from the last checkpoint.

The first attempt was killed at epoch 24/100 when its monitoring process was
stopped (it was running training as a direct child, an architecture mistake -
this resume runs fully detached instead, see docs/phases/phase-3-implementation-plan.md
Step 8 for the full story). Ultralytics' resume=True reads the saved args
and optimizer state from last.pt and continues toward the original epoch
target, not a fresh 100.
"""

from ultralytics import YOLO


def main() -> None:
    model = YOLO("runs/segment/ml/runs/door_handle_seg_v1/weights/last.pt")
    model.train(resume=True)


if __name__ == "__main__":
    main()
