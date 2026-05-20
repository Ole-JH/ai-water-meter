# Water Meter Reader

Reads an analog water meter using an ESP32-CAM as an image source and a Raspberry Pi with a Google Coral USB/PCIe accelerator for AI inference. Digit images are classified by a TFLite CNN running on the Edge TPU and the assembled reading is published to MQTT.

---

## Prerequisites

### System packages

```bash
# Edge TPU runtime (standard clock speed)
echo "deb https://packages.cloud.google.com/apt coral-edgetpu-stable main" \
  | sudo tee /etc/apt/sources.list.d/coral-edgetpu.list
curl https://packages.cloud.google.com/apt/doc/apt-key.gpg | sudo apt-key add -
sudo apt update
sudo apt install libedgetpu1-std

# OpenCV native libs (headless wheels need these)
sudo apt install libgl1 libglib2.0-0
```

### Python dependencies

```bash
pip3 install -r requirements.txt
```

`pycoral` and `tflite-runtime` ship as pre-built wheels for `aarch64` from the Coral team. If `pip` cannot find them automatically, install them from the Coral release page:

```bash
pip3 install \
  "https://github.com/google-coral/pycoral/releases/latest/download/pycoral-2.0.0-cp311-cp311-linux_aarch64.whl" \
  "https://github.com/google-coral/pycoral/releases/latest/download/tflite_runtime-2.5.0.post1-cp311-cp311-linux_aarch64.whl"
```

---

## ESP32-CAM setup

1. Open **Arduino IDE** and install the **ESP32 board package** (Boards Manager → search "esp32" → install Espressif).
2. Open `File → Examples → ESP32 → Camera → CameraWebServer`.
3. At the top of the sketch, uncomment:
   ```cpp
   #define CAMERA_MODEL_AI_THINKER
   ```
4. Fill in your WiFi credentials:
   ```cpp
   const char* ssid = "YourSSID";
   const char* password = "YourPassword";
   ```
5. Select **Board: AI Thinker ESP32-CAM** and the correct COM/tty port.
6. Flash the sketch. Open the Serial Monitor at 115200 baud and note the IP address printed after boot.
7. Confirm the snapshot endpoint works: open `http://<esp32-ip>/capture` in a browser — you should see a JPEG.
8. Set `esp32.url` in `config.yaml` to `http://<esp32-ip>/capture`.

---

## Model setup

### Download pre-trained models

Models are published by the [AI-on-the-Edge-Device](https://github.com/jomjol/AI-on-the-edge-device) project:

- **Digital digit wheels**: `dig-cont_0168_s2_q_edgetpu.tflite`
- **Analog rotating pointer dials**: `ana-cont_1105_s2_q_edgetpu.tflite`

Download them from the project's GitHub Releases page and place them on the Pi, e.g.:

```bash
mkdir -p /home/pi/models
# (copy the .tflite files to /home/pi/models/)
```

### Compiling for Edge TPU

The downloaded models are already compiled for the Edge TPU (filename contains `_edgetpu`). If you need to compile a custom model:

> **Note:** The Edge TPU compiler does **not** run on ARM64 (Raspberry Pi). Compilation must happen on an x86 Debian/Ubuntu machine or via Google Colab.

**Option A — x86 machine:**

```bash
curl https://packages.cloud.google.com/apt/doc/apt-key.gpg | sudo apt-key add -
echo "deb https://packages.cloud.google.com/apt coral-edgetpu-stable main" \
  | sudo tee /etc/apt/sources.list.d/coral-edgetpu.list
sudo apt update && sudo apt install edgetpu-compiler
edgetpu_compiler your_model_quant.tflite
```

**Option B — Google Colab:**

Follow the notebook at [github.com/google-coral/tutorials](https://github.com/google-coral/tutorials) → `compile_for_edgetpu.ipynb`.

---

## Calibration

### 1. Set the ESP32-CAM URL

Edit `config.yaml`:

```yaml
esp32:
  url: "http://192.168.1.x/capture"
```

### 2. Find ROI coordinates

```bash
python3 roi.py --capture
```

This fetches a live snapshot, saves it as `calibration_capture.jpg`, and opens an OpenCV window. Move the mouse over each digit to read its pixel coordinates from the status bar. Press `q` to exit — the current ROI list is printed in YAML format, ready to paste into `config.yaml`.

### 3. Update `rois` in `config.yaml`

Each entry is `[x, y, width, height]` (top-left corner, then size). List digits left to right. The first `meter.integer_digits` entries are the integer part; the remaining `meter.decimal_digits` entries are the decimal part.

### 4. Verify the boxes

```bash
python3 roi.py --test
```

Opens `roi_test_output.jpg` with green boxes drawn over each digit. Confirm every box is correctly centred.

### 5. Generate the reference image

Delete any stale reference image, then start the service once so it captures a fresh one:

```bash
rm -f /home/pi/meter_reference.jpg
python3 meter_reader.py
# Wait for "Reference image saved" log line, then Ctrl-C
```

### 6. Start the service

```bash
sudo cp meter-reader.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now meter-reader
journalctl -u meter-reader -f
```

---

## Home Assistant integration

Add to your HA `configuration.yaml`:

```yaml
mqtt:
  sensor:
    - name: "Water Meter"
      state_topic: "watermeter/value"
      unit_of_measurement: "m³"
      device_class: water
      state_class: total_increasing
```

The raw digit array is also published to `watermeter/raw` as a JSON array, which can be useful for debugging.

---

## Running as a service

```bash
sudo cp meter-reader.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now meter-reader
journalctl -u meter-reader -f
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `libedgetpu.so.1 not found` | Runtime library missing | `sudo apt install libedgetpu1-std` |
| `Encountered unresolved custom op: edgetpu-custom-op` | Coral device not detected | Check USB cable/port; try `lsusb` to confirm `Global Unichip Corp.` appears; try a different USB 3.0 port |
| Readings wildly wrong | ROIs are misaligned | Re-run `python3 roi.py --capture` and update `rois` in `config.yaml` |
| Readings drift over time | Alignment failing | Switch `alignment.method` from `ecc` to `orb` in `config.yaml`, or re-capture the reference image |
| MQTT not receiving data | Wrong broker or topic | Verify `mqtt.broker` IP, port 1883 is open, and `state_topic` in HA matches `mqtt.topic` in `config.yaml` |
| Service won't start | Wrong Python path | Check `ExecStart` path; confirm `which python3` on the Pi |
| `ImportError: No module named 'pycoral'` | pycoral not installed for system Python | Run `pip3 install --break-system-packages pycoral` or use a venv |
