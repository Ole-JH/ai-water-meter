#!/bin/sh
# Starts Alertmanager with a config built from the environment (server/.env), so webhook URLs and keys never live in git.
#
#   DISCORD_WEBHOOK_URL   a Discord channel webhook (Channel settings > Integrations > Webhooks > New webhook > Copy URL)
#   PUSHOVER_USER_KEY     your Pushover user key (pushover.net, top of the dashboard)
#   PUSHOVER_TOKEN        a Pushover application API token (pushover.net > Create an Application)
#
# Set either or both. Critical alerts go out right away; warnings are grouped. With neither set, alerts are only shown in
# Grafana (Alerting) and the Alertmanager UI. Template: alertmanager.yml (the route and grouping), this file (receivers).
set -eu

dir=/tmp/alertmanager
mkdir -p "$dir"
chmod 700 "$dir"
cfg="$dir/alertmanager.yml"

receiver=null
discord=""
pushover=""
if [ -n "${DISCORD_WEBHOOK_URL:-}" ]; then
  printf '%s' "$DISCORD_WEBHOOK_URL" > "$dir/discord_webhook"
  receiver=notify
  discord="    discord_configs:
      - webhook_url_file: $dir/discord_webhook
        send_resolved: true
        title: '{{ if eq .Status \"firing\" }}{{ if eq .CommonLabels.severity \"critical\" }}:rotating_light:{{ else }}:warning:{{ end }}{{ else }}:white_check_mark:{{ end }} Shadowfall: {{ .CommonLabels.alertname }} ({{ .Status }})'
        message: '{{ range .Alerts }}{{ .Annotations.summary }}{{ if .Annotations.description }} - {{ .Annotations.description }}{{ end }}
          {{ end }}'"
fi
if [ -n "${PUSHOVER_USER_KEY:-}" ] && [ -n "${PUSHOVER_TOKEN:-}" ]; then
  printf '%s' "$PUSHOVER_USER_KEY" > "$dir/pushover_user"
  printf '%s' "$PUSHOVER_TOKEN" > "$dir/pushover_token"
  receiver=notify
  pushover="    pushover_configs:
      - user_key_file: $dir/pushover_user
        token_file: $dir/pushover_token
        send_resolved: true
        title: 'Shadowfall: {{ .CommonLabels.alertname }} ({{ .Status }})'
        message: '{{ range .Alerts }}{{ .Annotations.summary }}{{ if .Annotations.description }} - {{ .Annotations.description }}{{ end }}
          {{ end }}'
        priority: '{{ if and (eq .Status \"firing\") (eq .CommonLabels.severity \"critical\") }}1{{ else }}0{{ end }}'"
elif [ -n "${PUSHOVER_USER_KEY:-}${PUSHOVER_TOKEN:-}" ]; then
  echo "start.sh: Pushover needs both PUSHOVER_USER_KEY and PUSHOVER_TOKEN; ignoring it" >&2
fi

# The route from the template, with the receiver filled in, then the receivers.
sed "s/__RECEIVER__/$receiver/" /etc/alertmanager/alertmanager.yml > "$cfg"
{
  echo ""
  echo "receivers:"
  echo "  - name: \"null\""
  if [ "$receiver" = notify ]; then
    echo "  - name: notify"
    [ -n "$discord" ] && echo "$discord"
    [ -n "$pushover" ] && echo "$pushover"
  fi
} >> "$cfg"
chmod 600 "$dir"/* 2>/dev/null || true

echo "start.sh: alerts go to: $( [ -n "$discord" ] && printf 'Discord ' )$( [ -n "$pushover" ] && printf 'Pushover ' )$( [ "$receiver" = null ] && printf 'nowhere (only Grafana)' )" >&2
exec /bin/alertmanager --config.file="$cfg" --storage.path=/alertmanager "$@"
