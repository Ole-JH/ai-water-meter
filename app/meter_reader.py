#!/usr/bin/env python3
"""
Meter Reader - Polls ESP-CAM snapshot, runs TFLite inference,
publishes result to MQTT for Home Assistant.
"""

import io
import json
import logging
import os
import time
from pathlib import Path

import numpy as np
import paho.mqtt.client as mqtt
import requests
import yaml
from PIL import Image

# Optional TFLite - graceful fallback for development
try:
    import tflite_runtime.interpreter as tflite
    TFLITE_AVAILABLE = True
except ImportError:
    TFLITE_AVAILABLE = False
    logging.warning("tflite_runtime not available - inference disabled")

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
)
log = logging.getLogger(__name__)


# ── Config ────────────────────────────────────────────────────────────────────

def load_config(path: str = "/config/config.yaml") -> dict:
    with open(path) as f:
        cfg = yaml.safe_load(f)
    # Allow env overrides for secrets
    cfg["camera"]["url"] = os.getenv("CAMERA_URL", cfg["camera"]["url"])
    cfg["mqtt"]["host"] = os.getenv("MQTT_HOST", cfg["mqtt"]["host"])
    cfg["mqtt"]["port"] = int(os.getenv("MQTT_PORT", cfg["mqtt"]["port"]))
    cfg["mqtt"]["username"] = os.getenv("MQTT_USER", cfg["mqtt"].get("username"))
    cfg["mqtt"]["password"] = os.getenv("MQTT_PASS", cfg["mqtt"].get("password"))
    return cfg


# ── Camera ────────────────────────────────────────────────────────────────────

def grab_snapshot(url: str, timeout: int = 10) -> Image.Image:
    """Fetch JPEG snapshot from ESPHome camera endpoint."""
    r = requests.get(url, timeout=timeout)
    r.raise_for_status()
    img = Image.open(io.BytesIO(r.content)).convert("RGB")
    log.debug("Snapshot fetched: %dx%d", img.width, img.height)
    return img


def save_snapshot(img: Image.Image, path: str = "/snapshots/latest.jpg"):
    """Persist latest snapshot for debugging via mounted volume."""
    img.save(path, "JPEG", quality=85)


# ── Inference ─────────────────────────────────────────────────────────────────

class DigitModel:
    """Wraps a jomjol-style dig-class TFLite model."""

    # jomjol dig models: input 20x32, output 11 classes (0-9 + NaN)
    INPUT_W = 20
    INPUT_H = 32
    NAN_CLASS = 10

    def __init__(self, model_path: str):
        if not TFLITE_AVAILABLE:
            raise RuntimeError("tflite_runtime is required for inference")
        self.interpreter = tflite.Interpreter(model_path=model_path)
        self.interpreter.allocate_tensors()
        self.input_details = self.interpreter.get_input_details()
        self.output_details = self.interpreter.get_output_details()
        log.info("Loaded digit model: %s", model_path)

    def predict(self, roi: Image.Image) -> int | None:
        """Return digit 0-9, or None for NaN/unreadable."""
        img = roi.resize((self.INPUT_W, self.INPUT_H), Image.LANCZOS)
        arr = np.array(img, dtype=np.float32) / 255.0
        arr = np.expand_dims(arr, axis=0)  # (1, H, W, 3)
        self.interpreter.set_tensor(self.input_details[0]["index"], arr)
        self.interpreter.invoke()
        output = self.interpreter.get_tensor(self.output_details[0]["index"])[0]
        cls = int(np.argmax(output))
        confidence = float(output[cls])
        log.debug("Digit class=%d confidence=%.3f", cls, confidence)
        if cls == self.NAN_CLASS or confidence < 0.5:
            return None
        return cls


class AnalogModel:
    """Wraps a jomjol-style ana-class TFLite model (pointer angle → value 0.0-0.9)."""

    INPUT_W = 32
    INPUT_H = 32

    def __init__(self, model_path: str):
        if not TFLITE_AVAILABLE:
            raise RuntimeError("tflite_runtime is required for inference")
        self.interpreter = tflite.Interpreter(model_path=model_path)
        self.interpreter.allocate_tensors()
        self.input_details = self.interpreter.get_input_details()
        self.output_details = self.interpreter.get_output_details()
        log.info("Loaded analog model: %s", model_path)

    def predict(self, roi: Image.Image) -> float | None:
        img = roi.resize((self.INPUT_W, self.INPUT_H), Image.LANCZOS)
        arr = np.array(img, dtype=np.float32) / 255.0
        arr = np.expand_dims(arr, axis=0)
        self.interpreter.set_tensor(self.input_details[0]["index"], arr)
        self.interpreter.invoke()
        output = self.interpreter.get_tensor(self.output_details[0]["index"])[0]
        return float(output[0])  # 0.0–0.9


# ── ROI extraction ─────────────────────────────────────────────────────────────

def crop_roi(img: Image.Image, roi: dict) -> Image.Image:
    """
    Crop a region of interest from image.
    roi: {x: int, y: int, w: int, h: int}  (pixels, top-left origin)
    """
    x, y, w, h = roi["x"], roi["y"], roi["w"], roi["h"]
    return img.crop((x, y, x + w, y + h))


# ── Reading assembly ───────────────────────────────────────────────────────────

def read_meter(img: Image.Image, meter_cfg: dict, models: dict) -> str | None:
    """
    Process all ROIs for a meter definition, return assembled value string.
    Example meter_cfg ROIs:
      digits: list of {x,y,w,h} left→right
      analogs: list of {x,y,w,h} decimal positions
    """
    parts = []

    digit_model = models.get("digit")
    analog_model = models.get("analog")

    for roi_cfg in meter_cfg.get("digits", []):
        if digit_model is None:
            log.error("Digit ROI defined but no digit model loaded")
            return None
        roi = crop_roi(img, roi_cfg)
        digit = digit_model.predict(roi)
        if digit is None:
            log.warning("Unreadable digit ROI at %s", roi_cfg)
            return None
        parts.append(str(digit))

    decimal_parts = []
    for roi_cfg in meter_cfg.get("analogs", []):
        if analog_model is None:
            log.error("Analog ROI defined but no analog model loaded")
            return None
        roi = crop_roi(img, roi_cfg)
        val = analog_model.predict(roi)
        if val is None:
            return None
        decimal_parts.append(str(round(val, 1)))

    if parts and decimal_parts:
        return ".".join(["".join(parts), "".join(d[-1] for d in decimal_parts)])
    elif parts:
        return "".join(parts)
    return None


# ── MQTT ──────────────────────────────────────────────────────────────────────

def build_mqtt_client(cfg: dict) -> mqtt.Client:
    client = mqtt.Client(client_id="meter-reader")
    if cfg.get("username"):
        client.username_pw_set(cfg["username"], cfg.get("password"))
    if cfg.get("tls", False):
        client.tls_set()
    client.connect(cfg["host"], cfg["port"], keepalive=60)
    client.loop_start()
    return client


def publish_value(client: mqtt.Client, topic: str, value: str, retain: bool = True):
    client.publish(topic, value, retain=retain)
    log.info("Published %s → %s", topic, value)


def publish_ha_discovery(client: mqtt.Client, meter_cfg: dict):
    """Publish Home Assistant MQTT discovery payload (run once on start)."""
    name = meter_cfg["name"]
    unique_id = name.lower().replace(" ", "_")
    topic = f"homeassistant/sensor/{unique_id}/config"
    payload = {
        "name": name,
        "unique_id": f"meter_reader_{unique_id}",
        "state_topic": meter_cfg["mqtt_topic"],
        "unit_of_measurement": meter_cfg.get("unit", ""),
        "device_class": meter_cfg.get("device_class", ""),
        "state_class": meter_cfg.get("state_class", "total_increasing"),
        "device": {
            "identifiers": ["meter_reader"],
            "name": "Meter Reader",
            "model": "ESPCam + Pi TFLite",
            "manufacturer": "DIY",
        },
    }
    client.publish(topic, json.dumps(payload), retain=True)
    log.info("HA discovery published for: %s", name)


# ── Main loop ─────────────────────────────────────────────────────────────────

def main():
    cfg = load_config()

    # Load models (optional - skip if no models directory populated yet)
    models = {}
    model_dir = Path("/models")
    digit_path = cfg.get("models", {}).get("digit")
    analog_path = cfg.get("models", {}).get("analog")

    if digit_path and (model_dir / digit_path).exists():
        models["digit"] = DigitModel(str(model_dir / digit_path))
    else:
        log.warning("No digit model found - running in snapshot-only mode")

    if analog_path and (model_dir / analog_path).exists():
        models["analog"] = AnalogModel(str(model_dir / analog_path))

    # MQTT
    mqtt_client = build_mqtt_client(cfg["mqtt"])

    # HA auto-discovery
    for meter in cfg["meters"]:
        publish_ha_discovery(mqtt_client, meter)

    interval = cfg.get("interval_seconds", 60)
    camera_url = cfg["camera"]["url"]
    log.info("Starting poll loop every %ds — camera: %s", interval, camera_url)

    while True:
        try:
            img = grab_snapshot(camera_url)
            save_snapshot(img)

            for meter in cfg["meters"]:
                if not models:
                    log.info("No models loaded — snapshot saved, skipping inference")
                    break
                value = read_meter(img, meter, models)
                if value is not None:
                    publish_value(mqtt_client, meter["mqtt_topic"], value)
                else:
                    log.warning("Could not read meter: %s", meter["name"])

        except requests.RequestException as e:
            log.error("Camera fetch failed: %s", e)
        except Exception as e:
            log.exception("Unexpected error: %s", e)

        time.sleep(interval)


if __name__ == "__main__":
    main()
