"""Image alignment helpers for stabilising ESP32-CAM frames."""

from __future__ import annotations

import logging
import math

import cv2
import numpy as np
from PIL import Image

logger = logging.getLogger(__name__)


def _pil_to_gray(img: Image.Image) -> np.ndarray:
    return np.array(img.convert("L"), dtype=np.float32)


def _shift_magnitude(warp_matrix: np.ndarray) -> float:
    tx = float(warp_matrix[0, 2])
    ty = float(warp_matrix[1, 2])
    return math.hypot(tx, ty)


def _align_ecc(
    src: Image.Image,
    reference: Image.Image,
    max_shift_px: int,
) -> Image.Image:
    src_gray = _pil_to_gray(src)
    ref_gray = _pil_to_gray(reference)

    warp_matrix = np.eye(2, 3, dtype=np.float32)
    criteria = (
        cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_COUNT,
        50,
        1e-4,
    )

    try:
        _, warp_matrix = cv2.findTransformECC(
            ref_gray,
            src_gray,
            warp_matrix,
            cv2.MOTION_TRANSLATION,
            criteria,
        )
    except cv2.error as exc:
        logger.warning("ECC alignment failed: %s — returning source unchanged", exc)
        return src

    shift = _shift_magnitude(warp_matrix)
    if shift > max_shift_px:
        logger.warning(
            "ECC shift %.1f px exceeds max_shift_px=%d — returning source unchanged",
            shift,
            max_shift_px,
        )
        return src

    logger.debug("ECC shift: %.2f px", shift)
    h, w = src_gray.shape
    src_array = np.array(src)
    aligned = cv2.warpAffine(
        src_array,
        warp_matrix,
        (w, h),
        flags=cv2.WARP_INVERSE_MAP | cv2.INTER_LINEAR,
    )
    return Image.fromarray(aligned)


def _align_orb(
    src: Image.Image,
    reference: Image.Image,
    max_shift_px: int,
) -> Image.Image:
    src_gray = np.array(src.convert("L"))
    ref_gray = np.array(reference.convert("L"))

    orb = cv2.ORB_create()
    kp_src, des_src = orb.detectAndCompute(src_gray, None)
    kp_ref, des_ref = orb.detectAndCompute(ref_gray, None)

    if des_src is None or des_ref is None or len(kp_src) < 4 or len(kp_ref) < 4:
        logger.warning("ORB: insufficient keypoints — returning source unchanged")
        return src

    matcher = cv2.BFMatcher(cv2.NORM_HAMMING, crossCheck=True)
    matches = matcher.match(des_src, des_ref)
    matches = sorted(matches, key=lambda m: m.distance)[:50]

    if len(matches) < 4:
        logger.warning("ORB: too few matches (%d) — returning source unchanged", len(matches))
        return src

    pts_src = np.float32([kp_src[m.queryIdx].pt for m in matches]).reshape(-1, 1, 2)
    pts_ref = np.float32([kp_ref[m.trainIdx].pt for m in matches]).reshape(-1, 1, 2)

    H, mask = cv2.findHomography(pts_src, pts_ref, cv2.RANSAC, 5.0)
    if H is None:
        logger.warning("ORB: findHomography failed — returning source unchanged")
        return src

    # Estimate translation magnitude from homography for the guard check
    shift = math.hypot(float(H[0, 2]), float(H[1, 2]))
    if shift > max_shift_px:
        logger.warning(
            "ORB shift %.1f px exceeds max_shift_px=%d — returning source unchanged",
            shift,
            max_shift_px,
        )
        return src

    logger.debug("ORB shift: %.2f px", shift)
    h, w = src_gray.shape
    src_array = np.array(src)
    aligned = cv2.warpPerspective(src_array, H, (w, h))
    return Image.fromarray(aligned)


def align(
    src: Image.Image,
    reference: Image.Image,
    method: str,
    max_shift_px: int,
) -> Image.Image:
    """Return a version of *src* aligned to *reference*.

    Falls back to returning *src* unmodified if alignment cannot be computed
    or if the detected shift exceeds *max_shift_px*.
    """
    if method == "none":
        return src
    if method == "ecc":
        return _align_ecc(src, reference, max_shift_px)
    if method == "orb":
        return _align_orb(src, reference, max_shift_px)
    raise ValueError(f"Unknown alignment method: {method!r}. Use 'ecc', 'orb', or 'none'.")
