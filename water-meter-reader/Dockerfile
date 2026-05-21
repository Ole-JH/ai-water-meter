# syntax=docker/dockerfile:1
FROM python:3.11-slim-bookworm

# Install Edge TPU runtime and OpenCV native deps
RUN apt-get update && apt-get install -y --no-install-recommends \
        curl \
        gnupg \
        libgl1 \
        libglib2.0-0 \
    && echo "deb https://packages.cloud.google.com/apt coral-edgetpu-stable main" \
        > /etc/apt/sources.list.d/coral-edgetpu.list \
    && curl -fsSL https://packages.cloud.google.com/apt/doc/apt-key.gpg \
        | gpg --dearmor -o /etc/apt/trusted.gpg.d/coral.gpg \
    && apt-get update \
    && apt-get install -y --no-install-recommends libedgetpu1-std \
    && apt-get purge -y curl gnupg \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Install Python deps before copying app code so this layer is cached
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY align.py roi.py meter_reader.py config.yaml ./

# Honour PYTHONUNBUFFERED so logs appear in `docker logs` immediately
ENV PYTHONUNBUFFERED=1

CMD ["python3", "meter_reader.py"]
