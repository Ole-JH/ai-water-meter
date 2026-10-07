#!/bin/sh
# Prints a host port of the Docker stack the way docker compose sees it: the environment first, then server/.env,
# then the default. Run from server/:   sh ../tools/port-of.sh GRAFANA_PORT 7343
name="$1"; default="$2"
value="$(printenv "$name" 2>/dev/null)"
if [ -z "$value" ] && [ -f .env ]; then
  value="$(sed -n "s/^$name=//p" .env | tail -n 1 | tr -d '"\r' | tr -d "'")"
fi
echo "${value:-$default}"
