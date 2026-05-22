# Meter Reader

Polls an ESPHome ESP32-CAM, runs jomjol TFLite digit/analog models,
publishes meter values to MQTT for Home Assistant.

## Directory layout

```
meter-reader/
├── Dockerfile
├── docker-compose.yml
├── requirements.txt
├── .env.example           ← copy to .env and fill in
├── app/
│   └── meter_reader.py
├── config/
│   └── config.yaml        ← ROIs, topics, model names
├── models/                ← drop .tflite files here
└── snapshots/             ← latest.jpg written here (for calibration)
```

## Quick start

### 1. Get TFLite models

Download from the jomjol repo:
https://github.com/jomjol/AI-on-the-edge-device/tree/main/sd-card/config

Place `.tflite` files in `./models/`. You need at minimum:
- `dig-cont_11.tflite` (or whichever version you download)

### 2. Configure

```bash
cp .env.example .env
# Edit .env with your camera URL and MQTT broker details
```

Edit `config/config.yaml` — set your MQTT topic and placeholder ROIs.
Leave ROIs approximate for now; you'll calibrate in step 4.

### 3. Start

```bash
docker compose up -d
```

On first run it starts in **snapshot-only mode** if models aren't loaded yet.
It will still write `snapshots/latest.jpg` every 60 seconds.

### 4. Calibrate ROIs

Open `snapshots/latest.jpg` in any image editor (GIMP, Photoshop,
even Preview/Paint) and note the pixel coordinates of each digit.

Each digit ROI should be roughly **20×32 px** and tightly crop one digit.

Update `config/config.yaml` with real coordinates, then restart:
```bash
docker compose restart
```

### 5. Check logs

```bash
docker compose logs -f
```

Expected output once running:
```
2024-01-15 10:00:00 [INFO] Snapshot fetched: 800x600
2024-01-15 10:00:00 [INFO] Published meter/gas/value → 1234.5
```

## Home Assistant

The container publishes MQTT discovery on startup — HA will auto-create
a sensor entity. No manual YAML needed if you have MQTT integration enabled.

Manual sensor (if you prefer explicit config):
```yaml
mqtt:
  sensor:
    - name: "Gas Meter"
      state_topic: "meter/gas/value"
      unit_of_measurement: "m³"
      device_class: gas
      state_class: total_increasing
```

## Multi-arch

The image builds for `linux/amd64` and `linux/arm64` (Pi 4/5).
For Pi 3 (armv7) you may need to use an older tflite-runtime wheel.

## Troubleshooting

**Camera unreachable**: Check `CAMERA_URL` and that the Pi can reach the
ESP-CAM IP. Try `curl http://<ip>/camera/snapshot` from the host.

**Wrong digits**: ROIs are off. Inspect `snapshots/latest.jpg` and recalibrate.

**NaN readings**: Lighting issue — enable the flash LED from HA, or increase
JPEG quality in your ESPHome config.
