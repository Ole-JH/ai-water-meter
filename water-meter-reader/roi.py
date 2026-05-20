"""ROI extraction helpers and calibration CLI."""

from __future__ import annotations

import argparse
import logging
import sys
from pathlib import Path

import cv2
import numpy as np
import requests
import yaml
from PIL import Image

logger = logging.getLogger(__name__)

_CONFIG_PATH = Path(__file__).parent / "config.yaml"


# ---------------------------------------------------------------------------
# Library API
# ---------------------------------------------------------------------------

def extract_rois(img: Image.Image, rois: list[tuple]) -> list[Image.Image]:
    """Crop each ROI from *img*. Returns a list of PIL Images."""
    crops: list[Image.Image] = []
    w_img, h_img = img.size
    for x, y, w, h in rois:
        # Clamp to image bounds
        x1 = max(0, x)
        y1 = max(0, y)
        x2 = min(w_img, x + w)
        y2 = min(h_img, y + h)
        crops.append(img.crop((x1, y1, x2, y2)))
    return crops


# ---------------------------------------------------------------------------
# Internal helpers
# ---------------------------------------------------------------------------

def _load_config() -> dict:
    with open(_CONFIG_PATH) as fh:
        return yaml.safe_load(fh)


def _fetch_image(url: str, timeout: int) -> Image.Image:
    logger.info("Fetching snapshot from %s", url)
    resp = requests.get(url, timeout=timeout)
    resp.raise_for_status()
    from io import BytesIO
    return Image.open(BytesIO(resp.content)).convert("RGB")


def _draw_rois(cv_img: np.ndarray, rois: list) -> np.ndarray:
    out = cv_img.copy()
    for idx, (x, y, w, h) in enumerate(rois):
        cv2.rectangle(out, (x, y), (x + w, y + h), (0, 255, 0), 2)
        cv2.putText(
            out,
            str(idx),
            (x + 2, y + 14),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.5,
            (0, 255, 0),
            1,
            cv2.LINE_AA,
        )
    return out


# ---------------------------------------------------------------------------
# CLI: --capture
# ---------------------------------------------------------------------------

def _cmd_capture(cfg: dict) -> None:
    esp = cfg["esp32"]
    img = _fetch_image(esp["url"], esp["timeout_sec"])

    out_path = Path("calibration_capture.jpg")
    img.save(out_path)
    print(f"Snapshot saved to {out_path}")

    rois: list = cfg.get("rois", [])
    cv_img = cv2.cvtColor(np.array(img), cv2.COLOR_RGB2BGR)

    mouse_pos: list[tuple[int, int]] = [(0, 0)]

    def _on_mouse(event: int, x: int, y: int, flags: int, param: object) -> None:
        mouse_pos[0] = (x, y)
        if event == cv2.EVENT_MOUSEMOVE:
            pass  # coordinates printed in display loop

    win = "ROI Calibration — move mouse, press q to quit"
    cv2.namedWindow(win)
    cv2.setMouseCallback(win, _on_mouse)

    print("Move the mouse over the image to see pixel coordinates.")
    print("Press 'q' to exit and print the ROI YAML snippet.\n")

    while True:
        display = _draw_rois(cv_img, rois)
        x_m, y_m = mouse_pos[0]
        cv2.putText(
            display,
            f"({x_m}, {y_m})",
            (10, display.shape[0] - 10),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.5,
            (255, 255, 0),
            1,
            cv2.LINE_AA,
        )
        cv2.imshow(win, display)
        key = cv2.waitKey(30) & 0xFF
        if key == ord("q"):
            break

    cv2.destroyAllWindows()

    print("\nCurrent ROIs (paste into config.yaml):")
    print("rois:")
    for roi in rois:
        print(f"  - {list(roi)}")


# ---------------------------------------------------------------------------
# CLI: --test
# ---------------------------------------------------------------------------

def _cmd_test(cfg: dict) -> None:
    from align import align as align_image

    esp = cfg["esp32"]
    img = _fetch_image(esp["url"], esp["timeout_sec"])

    alignment_cfg = cfg.get("alignment", {})
    if alignment_cfg.get("enabled", False):
        ref_path = Path(cfg["meter"]["reference_image_path"])
        if ref_path.exists():
            reference = Image.open(ref_path).convert("RGB")
            img = align_image(
                img,
                reference,
                method=alignment_cfg.get("method", "ecc"),
                max_shift_px=alignment_cfg.get("max_shift_px", 30),
            )
        else:
            print(f"Warning: reference image not found at {ref_path} — skipping alignment")

    rois: list = cfg.get("rois", [])
    cv_img = cv2.cvtColor(np.array(img), cv2.COLOR_RGB2BGR)
    annotated = _draw_rois(cv_img, rois)

    out_path = Path("roi_test_output.jpg")
    cv2.imwrite(str(out_path), annotated)
    print(f"Annotated image saved to {out_path}")

    crops = extract_rois(img, [tuple(r) for r in rois])
    for idx, crop in enumerate(crops):
        print(f"ROI {idx}: {crop.size[0]}×{crop.size[1]} px")


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")

    parser = argparse.ArgumentParser(description="ROI calibration tool")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--capture", action="store_true", help="Fetch live image and open calibration window")
    group.add_argument("--test", action="store_true", help="Fetch live image, align, annotate ROIs, save to roi_test_output.jpg")
    args = parser.parse_args()

    cfg = _load_config()

    if args.capture:
        _cmd_capture(cfg)
    elif args.test:
        _cmd_test(cfg)
