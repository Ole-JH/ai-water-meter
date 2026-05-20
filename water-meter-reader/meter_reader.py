"""Water meter reader — main inference loop.

Usage:
    python3 meter_reader.py [--debug]
"""

from __future__ import annotations

import argparse
import json
import logging
import time
from io import BytesIO
from pathlib import Path

import numpy as np
import paho.mqtt.client as mqtt
import requests
import yaml
from PIL import Image
from pycoral.adapters import common
from pycoral.utils.edgetpu import make_interpreter

import align
import roi

logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

def _load_config(path: Path) -> dict:
    with open(path) as fh:
        return yaml.safe_load(fh)


# ---------------------------------------------------------------------------
# Image fetch
# ---------------------------------------------------------------------------

def _fetch_image(url: str, timeout: int) -> Image.Image:
    resp = requests.get(url, timeout=timeout)
    resp.raise_for_status()
    return Image.open(BytesIO(resp.content)).convert("RGB")


# ---------------------------------------------------------------------------
# Inference
# ---------------------------------------------------------------------------

def _run_inference(
    interpreter,
    roi_img: Image.Image,
    model_type: str,
) -> float:
    input_size = common.input_size(interpreter)
    resized = roi_img.resize(input_size, Image.LANCZOS)
    common.set_input(interpreter, resized)
    interpreter.invoke()
    output = common.output_tensor(interpreter, 0).flatten()

    if model_type == "digital":
        return float(int(np.argmax(output)))
    # analog: single regression output in [0, 1] representing position 0.0–10.0
    return float(output[0]) * 10.0


def _assemble_reading(digits: list[float], integer_digits: int, decimal_digits: int) -> str:
    int_part = "".join(str(int(round(d))) for d in digits[:integer_digits])
    dec_part = "".join(str(int(round(d))) for d in digits[integer_digits : integer_digits + decimal_digits])
    return f"{int_part}.{dec_part}"


# ---------------------------------------------------------------------------
# MQTT
# ---------------------------------------------------------------------------

def _build_mqtt_client(cfg: dict) -> mqtt.Client:
    client = mqtt.Client(client_id=cfg["client_id"])

    def _on_connect(client, userdata, flags, rc):  # noqa: ANN001
        if rc == 0:
            logger.info("MQTT connected to %s:%d", cfg["broker"], cfg["port"])
        else:
            logger.warning("MQTT connect failed with rc=%d", rc)

    def _on_disconnect(client, userdata, rc):  # noqa: ANN001
        if rc != 0:
            logger.warning("MQTT unexpected disconnect (rc=%d) — will reconnect", rc)
            while True:
                try:
                    client.reconnect()
                    logger.info("MQTT reconnected")
                    break
                except Exception as exc:
                    logger.warning("MQTT reconnect failed: %s — retrying in 5 s", exc)
                    time.sleep(5)

    client.on_connect = _on_connect
    client.on_disconnect = _on_disconnect
    return client


def _mqtt_connect(client: mqtt.Client, cfg: dict) -> None:
    client.connect(cfg["broker"], cfg["port"], keepalive=60)
    client.loop_start()


# ---------------------------------------------------------------------------
# Main loop
# ---------------------------------------------------------------------------

def run(config_path: Path) -> None:
    cfg = _load_config(config_path)

    coral_cfg = cfg["coral"]
    meter_cfg = cfg["meter"]
    esp_cfg = cfg["esp32"]
    mqtt_cfg = cfg["mqtt"]
    align_cfg = cfg.get("alignment", {})
    rois: list[tuple] = [tuple(r) for r in cfg.get("rois", [])]

    # Coral interpreter (created once, reused every cycle)
    logger.info("Loading Edge TPU model: %s", coral_cfg["model_path"])
    interpreter = make_interpreter(coral_cfg["model_path"])
    interpreter.allocate_tensors()
    logger.info("Edge TPU interpreter ready")

    # MQTT
    mqtt_client = _build_mqtt_client(mqtt_cfg)
    _mqtt_connect(mqtt_client, mqtt_cfg)

    # Reference image
    ref_path = Path(meter_cfg["reference_image_path"])
    if not ref_path.exists():
        logger.info("No reference image found — fetching one from ESP32")
        try:
            ref_img = _fetch_image(esp_cfg["url"], esp_cfg["timeout_sec"])
            ref_path.parent.mkdir(parents=True, exist_ok=True)
            ref_img.save(ref_path)
            logger.info("Reference image saved to %s — now tune ROIs in config.yaml", ref_path)
        except Exception as exc:
            logger.error("Failed to fetch reference image: %s", exc)

    reference: Image.Image | None = None
    if ref_path.exists():
        reference = Image.open(ref_path).convert("RGB")

    model_type = coral_cfg["model_type"]
    integer_digits = meter_cfg["integer_digits"]
    decimal_digits = meter_cfg["decimal_digits"]
    interval_sec = meter_cfg["interval_sec"]

    logger.info("Starting main loop (interval=%d s)", interval_sec)

    while True:
        cycle_start = time.monotonic()
        try:
            # 1. Fetch image
            img = _fetch_image(esp_cfg["url"], esp_cfg["timeout_sec"])

            # 2. Align
            if align_cfg.get("enabled", False) and reference is not None:
                img = align.align(
                    img,
                    reference,
                    method=align_cfg.get("method", "ecc"),
                    max_shift_px=align_cfg.get("max_shift_px", 30),
                )

            # 3. Extract ROIs
            crops = roi.extract_rois(img, rois)

            # 4. Inference per digit
            digits: list[float] = []
            for idx, crop in enumerate(crops):
                try:
                    value = _run_inference(interpreter, crop, model_type)
                    digits.append(value)
                    logger.debug("ROI %d → %.1f", idx, value)
                except Exception as exc:
                    logger.error("Inference error on ROI %d: %s", idx, exc, exc_info=True)
                    digits.append(float("nan"))

            # 5. Assemble reading
            reading = _assemble_reading(digits, integer_digits, decimal_digits)
            logger.info("Reading: %s", reading)

            # 6. Publish
            try:
                mqtt_client.publish(mqtt_cfg["topic"], reading)
                mqtt_client.publish(mqtt_cfg["topic_raw"], json.dumps(digits))
            except Exception as exc:
                logger.warning("MQTT publish error: %s", exc)

        except requests.RequestException as exc:
            logger.warning("Network error fetching image: %s — skipping cycle", exc)
        except Exception as exc:
            logger.error("Unexpected error in main loop: %s", exc, exc_info=True)

        elapsed = time.monotonic() - cycle_start
        sleep_time = max(0.0, interval_sec - elapsed)
        time.sleep(sleep_time)


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Water meter reader service")
    parser.add_argument("--debug", action="store_true", help="Enable DEBUG logging")
    parser.add_argument(
        "--config",
        type=Path,
        default=Path(__file__).parent / "config.yaml",
        help="Path to config.yaml (default: ./config.yaml)",
    )
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.DEBUG if args.debug else logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )

    run(args.config)
