# syntax=docker/dockerfile:1
FROM python:3.11-slim-bookworm

ARG TARGETPLATFORM
ARG TARGETARCH

WORKDIR /app

# System deps for Pillow + numpy
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1 \
    libglib2.0-0 \
    libgomp1 \
    && rm -rf /var/lib/apt/lists/*

# Python deps (everything except tflite-runtime)
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# tflite-runtime: official wheel works on amd64/arm64/armv7
# https://google-coral.github.io/py-repo/
RUN pip install --no-cache-dir \
    --extra-index-url https://google-coral.github.io/py-repo/ \
    tflite-runtime

COPY app/ .

# Volumes expected at runtime (see docker-compose.yml)
VOLUME ["/config", "/models", "/snapshots"]

ENV PYTHONUNBUFFERED=1

CMD ["python", "-u", "meter_reader.py"]
