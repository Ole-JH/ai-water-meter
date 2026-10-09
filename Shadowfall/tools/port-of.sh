#!/bin/sh
# Prints a host port of the Docker stack the way docker compose sees it: the environment first, then server/.env,
# then the default. Run from server/:   sh ../tools/port-of.sh GRAFANA_PORT 7343
# With --why first, also says where it came from:   sh ../tools/port-of.sh --why GRAFANA_PORT 7343  ->  7343 (default)
why=""
[ "$1" = "--why" ] && { why=1; shift; }
name="$1"; default="$2"; from="environment"
value="$(printenv "$name" 2>/dev/null)"
if [ -z "$value" ] && [ -f .env ]; then
  value="$(sed -n "s/^$name=//p" .env | tail -n 1 | tr -d '"\r' | tr -d "'")"
  from="server/.env"
fi
[ -z "$value" ] && { value="$default"; from="default"; }
if [ -n "$why" ]; then echo "$value ($from)"; else echo "$value"; fi
