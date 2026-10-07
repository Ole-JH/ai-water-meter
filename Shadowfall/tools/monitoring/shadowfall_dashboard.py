#!/usr/bin/env python3
"""Generates the Grafana "Shadowfall" dashboard from server/metrics.js's metrics.

    python3 tools/monitoring/shadowfall_dashboard.py

Writes server/monitoring/grafana/dashboards/shadowfall.json (Grafana provisions it on start).
"""
import json
import os

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "server", "monitoring", "grafana", "dashboards", "shadowfall.json")
PROM = {"type": "prometheus", "uid": "prometheus"}
LOKI = {"type": "loki", "uid": "loki"}

panels = []
y = 0
pid = 0


def nid():
    global pid
    pid += 1
    return pid


def row(title):
    global y
    panels.append({"type": "row", "title": title, "id": nid(), "collapsed": False, "gridPos": {"h": 1, "w": 24, "x": 0, "y": y}, "panels": []})
    y += 1


def stat(title, expr, x, w=4, unit="short", color="blue", thresholds=None, desc=""):
    steps = thresholds or [{"color": color, "value": None}]
    panels.append({
        "type": "stat", "title": title, "id": nid(), "datasource": PROM, "description": desc,
        "gridPos": {"h": 4, "w": w, "x": x, "y": y},
        "targets": [{"refId": "A", "expr": expr, "datasource": PROM, "instant": True}],
        "fieldConfig": {"defaults": {"unit": unit, "color": {"mode": "thresholds"}, "thresholds": {"mode": "absolute", "steps": steps}}, "overrides": []},
        "options": {"reduceOptions": {"calcs": ["lastNotNull"], "fields": "", "values": False}, "colorMode": "background", "graphMode": "area", "textMode": "value", "justifyMode": "center"},
    })


def ts(title, targets, x, w=12, h=8, unit="short", stack=False, desc="", bars=False):
    panels.append({
        "type": "timeseries", "title": title, "id": nid(), "datasource": PROM, "description": desc,
        "gridPos": {"h": h, "w": w, "x": x, "y": y},
        "targets": [{"refId": chr(65 + i), "expr": e, "legendFormat": l, "datasource": PROM} for i, (e, l) in enumerate(targets)],
        "fieldConfig": {"defaults": {"unit": unit, "custom": {
            "drawStyle": "bars" if bars else "line", "fillOpacity": 60 if bars else (35 if stack else 12), "lineWidth": 2, "showPoints": "never",
            "stacking": {"mode": "normal" if stack else "none", "group": "A"}, "gradientMode": "opacity"}}, "overrides": []},
        "options": {"legend": {"displayMode": "list", "placement": "bottom", "showLegend": True}, "tooltip": {"mode": "multi", "sort": "desc"}},
    })


def table(title, expr, x, w=12, h=8, desc=""):
    panels.append({
        "type": "bargauge", "title": title, "id": nid(), "datasource": PROM, "description": desc,
        "gridPos": {"h": h, "w": w, "x": x, "y": y},
        "targets": [{"refId": "A", "expr": expr, "legendFormat": "{{monster}}{{boss}}{{dungeon}}{{difficulty}}", "datasource": PROM, "instant": True, "format": "time_series"}],
        "fieldConfig": {"defaults": {"unit": "short", "color": {"mode": "continuous-YlRd"}, "min": 0}, "overrides": []},
        "options": {"orientation": "horizontal", "displayMode": "gradient", "showUnfilled": True, "reduceOptions": {"calcs": ["lastNotNull"], "fields": "", "values": False}},
    })


# ---------------------------------------------------------------- overview
row("Overview")
stat("Server", 'up{job="shadowfall"}', 0, color="green",
     thresholds=[{"color": "red", "value": None}, {"color": "green", "value": 1}], desc="1 = the game server answers Prometheus.")
stat("Players online", "shadowfall_players_online", 4, color="purple")
stat("In dungeons", "shadowfall_players_in_dungeons", 8, color="dark-orange")
stat("Monsters alive", "sum(shadowfall_monsters_alive)", 12, color="red")
stat("Accounts", "shadowfall_accounts", 16, color="blue", desc="Player accounts (each can have several characters).")
stat("Uptime", 'time() - process_start_time_seconds{job="shadowfall"}', 20, unit="s", color="text")
y += 4

# ---------------------------------------------------------------- players
row("Players")
ts("Players", [("shadowfall_players_online", "online"), ("shadowfall_players_in_dungeons", "in dungeons"),
               ("shadowfall_connections", "connections"), ("shadowfall_parties", "parties")], 0)
ts("Players by class", [("shadowfall_players_by_class", "{{class}}")], 12, stack=True)
y += 8
ts("Players by level", [("shadowfall_players_by_level", "{{band}}")], 0, stack=True)
ts("Logins (per hour)", [("sum by (result) (increase(shadowfall_logins_total[1h]))", "{{result}}")], 12, bars=True,
   desc="ok = logged in, new = account created, bad_password = wrong password, locked = refused after too many wrong tries.")
y += 8

# ---------------------------------------------------------------- accounts
row("Accounts")
ts("Accounts and characters", [("shadowfall_accounts", "accounts"), ("shadowfall_characters", "characters")], 0, w=8)
ts("Failed and blocked logins (per 10 min)", [('sum by (result) (increase(shadowfall_logins_total{result=~"bad_password|locked"}[10m]))', "{{result}}")], 8, w=8, bars=True,
   desc="Wrong passwords, and attempts refused because an account or address was locked out.")
ts("Password resets (per hour)", [("sum by (method) (increase(shadowfall_password_resets_total[1h]))", "{{method}}")], 16, w=8, bars=True,
   desc="recovery = with the recovery code, code = with an email or admin code, admin = codes issued by admins, change = changed in game.")
y += 8

# ---------------------------------------------------------------- combat
row("World & combat")
ts("Monsters alive", [("shadowfall_monsters_alive", "{{zone}}"), ("shadowfall_elites_alive", "elites")], 0)
ts("Kills per minute", [("sum by (monster) (rate(shadowfall_monsters_killed_total[5m])) * 60", "{{monster}}")], 12, stack=True)
y += 8
table("Most killed (selected range)", "sort_desc(sum by (monster) (increase(shadowfall_monsters_killed_total[$__range])))", 0, w=8)
table("Bosses slain (selected range)", "sort_desc(sum by (boss) (increase(shadowfall_bosses_killed_total[$__range])))", 8, w=8)
ts("Player deaths & elite kills (per 10 min)", [("sum(increase(shadowfall_player_deaths_total[10m]))", "player deaths"),
                                                 ('sum(increase(shadowfall_monsters_killed_total{elite="yes"}[10m]))', "elites slain")], 16, w=8, bars=True)
y += 8
ts("Open dungeon instances", [("shadowfall_dungeon_instances", "{{dungeon}}")], 0, stack=True)
table("Dungeon runs by difficulty (selected range)",
      'sort_desc(sum by (dungeon, difficulty) (increase(shadowfall_dungeon_entries_total[$__range])))', 12)
y += 8
table("Town invasions (selected range)", 'sort_desc(sum by (town, result) (increase(shadowfall_invasions_total[$__range])))', 0,
      desc="won = beaten off by the players, lost = the gate fell or nobody came")
y += 8

# ---------------------------------------------------------------- economy & social
row("Economy & social")
ts("Trades", [("sum(increase(shadowfall_trades_completed_total[1h]))", "completed per hour"), ("shadowfall_trades_open", "open now")], 0, w=8)
ts("Chat messages per minute", [('sum(rate(shadowfall_messages_received_total{type="chat"}[5m])) * 60', "chat")], 8, w=8)
ts("Admin commands (per hour)", [("sum by (cmd) (increase(shadowfall_admin_commands_total[1h]))", "{{cmd}}")], 16, w=8, bars=True)
y += 8

# ---------------------------------------------------------------- server health
row("Server health")
ts("Tick duration", [
    ("histogram_quantile(0.5, sum by (le) (rate(shadowfall_tick_duration_seconds_bucket[5m])))", "p50"),
    ("histogram_quantile(0.95, sum by (le) (rate(shadowfall_tick_duration_seconds_bucket[5m])))", "p95"),
    ("histogram_quantile(0.99, sum by (le) (rate(shadowfall_tick_duration_seconds_bucket[5m])))", "p99")], 0, w=8, unit="s",
   desc="Time spent simulating one 100 ms world tick. Above ~50 ms the world starts to lag.")
ts("Event loop lag (p99)", [('nodejs_eventloop_lag_p99_seconds{job="shadowfall"}', "lag")], 8, w=8, unit="s")
ts("Errors", [("sum by (type) (increase(shadowfall_handler_errors_total[10m]))", "handler: {{type}}"),
              ('sum(increase(shadowfall_character_saves_total{result="error"}[10m]))', "save errors")], 16, w=8, bars=True)
y += 8
ts("Memory", [('process_resident_memory_bytes{job="shadowfall"}', "resident"), ('nodejs_heap_used_bytes{job="shadowfall"}', "heap used"),
              ('nodejs_heap_total_bytes{job="shadowfall"}', "heap total")], 0, w=8, unit="bytes")
ts("CPU", [('rate(process_cpu_seconds_total{job="shadowfall"}[2m])', "game server")], 8, w=8, unit="percentunit")
ts("Network", [("rate(shadowfall_sent_bytes_total[2m])", "sent"), ("rate(shadowfall_received_bytes_total[2m])", "received")], 16, w=8, unit="Bps")
y += 8
ts("Messages received per second", [("sum by (type) (rate(shadowfall_messages_received_total[2m]))", "{{type}}")], 0, w=12, stack=True)
ts("Messages sent per second", [("rate(shadowfall_messages_sent_total[2m])", "sent")], 12, w=12)
y += 8

# ---------------------------------------------------------------- database
row("Database")
stat("PostgreSQL", "pg_up", 0, w=4, thresholds=[{"color": "red", "value": None}, {"color": "green", "value": 1}], desc="1 = the exporter can reach the database.")
stat("Database size", 'pg_database_size_bytes{datname="shadowfall"}', 4, w=4, unit="bytes", color="blue")
y += 4
ts("Database connections", [('pg_stat_database_numbackends{datname="shadowfall"}', "connections")], 0, w=8)
ts("Transactions per second", [('rate(pg_stat_database_xact_commit{datname="shadowfall"}[2m])', "commits"),
                               ('rate(pg_stat_database_xact_rollback{datname="shadowfall"}[2m])', "rollbacks")], 8, w=8)
ts("Rows written per second", [('rate(pg_stat_database_tup_inserted{datname="shadowfall"}[2m])', "inserted"),
                               ('rate(pg_stat_database_tup_updated{datname="shadowfall"}[2m])', "updated"),
                               ('rate(pg_stat_database_tup_deleted{datname="shadowfall"}[2m])', "deleted")], 16, w=8)
y += 8

# ---------------------------------------------------------------- logs
row("Logs")
panels.append({
    "type": "logs", "title": "Server log", "id": nid(), "datasource": LOKI,
    "gridPos": {"h": 12, "w": 24, "x": 0, "y": y},
    "targets": [{"refId": "A", "expr": '{service="shadowfall"} |~ "(?i)$search"', "datasource": LOKI}],
    "options": {"showTime": True, "wrapLogMessage": True, "sortOrder": "Descending", "enableLogDetails": True},
})
y += 12

dashboard = {
    "uid": "shadowfall", "title": "Shadowfall", "tags": ["shadowfall", "game"], "timezone": "browser", "schemaVersion": 39,
    "editable": True, "graphTooltip": 1, "refresh": "30s", "time": {"from": "now-6h", "to": "now"},
    "templating": {"list": [{"name": "search", "label": "Log search", "type": "textbox", "query": "", "current": {"text": "", "value": ""}}]},
    "annotations": {"list": [{"builtIn": 1, "datasource": {"type": "grafana", "uid": "-- Grafana --"}, "enable": True, "hide": True,
                              "iconColor": "rgba(0, 211, 255, 1)", "name": "Annotations & Alerts", "type": "dashboard"},
                             {"name": "Server restarts", "datasource": PROM, "enable": True, "iconColor": "orange",
                              "expr": 'changes(process_start_time_seconds{job="shadowfall"}[2m]) > 0', "step": "1m", "titleFormat": "Server restarted"}]},
    "links": [{"title": "Host", "type": "link", "url": "/d/node-exporter-full"}, {"title": "Containers", "type": "link", "url": "/d/cadvisor-containers"}],
    "panels": panels,
}

with open(OUT, "w") as f:
    json.dump(dashboard, f, indent=1)
    f.write("\n")
print("wrote", os.path.normpath(OUT), "with", len(panels), "panels")
