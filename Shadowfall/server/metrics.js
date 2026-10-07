// Minimal Prometheus metrics (text exposition format 0.0.4), no dependencies.
// Counters and histograms are updated by the game; gauges are collected at scrape time.
"use strict";
const http = require("http");
const { monitorEventLoopDelay } = require("perf_hooks");

const families = new Map(); // name -> { type, help, series: Map(labelKey -> {labels, value | buckets}) , buckets }

function esc(v) { return String(v).replace(/\\/g, "\\\\").replace(/\n/g, "\\n").replace(/"/g, '\\"'); }
function key(labels) { return labels ? Object.keys(labels).sort().map((k) => `${k}="${esc(labels[k])}"`).join(",") : ""; }

function family(name, type, help, buckets) {
  let f = families.get(name);
  if (!f) { f = { type, help, series: new Map(), buckets }; families.set(name, f); }
  return f;
}

function counter(name, help) {
  const f = family(name, "counter", help);
  return {
    inc(labels, n = 1) {
      const k = key(labels);
      const s = f.series.get(k) || { labels: k, value: 0 };
      s.value += n;
      f.series.set(k, s);
    },
  };
}

function histogram(name, help, buckets) {
  const f = family(name, "histogram", help, buckets);
  return {
    observe(v, labels) {
      const k = key(labels);
      let s = f.series.get(k);
      if (!s) { s = { labels: k, counts: buckets.map(() => 0), sum: 0, count: 0 }; f.series.set(k, s); }
      for (let i = 0; i < buckets.length; i++) if (v <= buckets[i]) s.counts[i]++;
      s.sum += v;
      s.count++;
    },
  };
}

// Gauges: name -> { help, fn: () => number | [[labels, value], ...] }
const gauges = new Map();
function gauge(name, help, fn) { gauges.set(name, { help, fn }); }

const loopDelay = monitorEventLoopDelay({ resolution: 10 });
loopDelay.enable();
const started = Date.now() / 1000;
let lastCpu = process.cpuUsage();

gauge("process_start_time_seconds", "Start time of the process since unix epoch in seconds.", () => started);
gauge("process_resident_memory_bytes", "Resident memory size in bytes.", () => process.memoryUsage().rss);
gauge("nodejs_heap_used_bytes", "Process heap used in bytes.", () => process.memoryUsage().heapUsed);
gauge("nodejs_heap_total_bytes", "Process heap total in bytes.", () => process.memoryUsage().heapTotal);
gauge("process_cpu_seconds_total", "Total user and system CPU time spent in seconds.", () => {
  lastCpu = process.cpuUsage();
  return (lastCpu.user + lastCpu.system) / 1e6;
});
gauge("nodejs_eventloop_lag_p99_seconds", "Event loop delay, 99th percentile over the last scrape interval.", () => {
  const v = loopDelay.percentile(99) / 1e9;
  loopDelay.reset();
  return v;
});
gauge("nodejs_version_info", "Node.js version.", () => [[{ version: process.version }, 1]]);

function render() {
  const out = [];
  for (const [name, f] of families) {
    out.push(`# HELP ${name} ${f.help}`, `# TYPE ${name} ${f.type}`);
    for (const s of f.series.values()) {
      if (f.type === "counter") out.push(`${name}${s.labels ? `{${s.labels}}` : ""} ${s.value}`);
      else {
        const pre = s.labels ? `${s.labels},` : "";
        f.buckets.forEach((b, i) => out.push(`${name}_bucket{${pre}le="${b}"} ${s.counts[i]}`));
        out.push(`${name}_bucket{${pre}le="+Inf"} ${s.count}`);
        out.push(`${name}_sum${s.labels ? `{${s.labels}}` : ""} ${s.sum}`);
        out.push(`${name}_count${s.labels ? `{${s.labels}}` : ""} ${s.count}`);
      }
    }
  }
  for (const [name, g] of gauges) {
    let v;
    try { v = g.fn(); } catch { continue; }
    const type = name.endsWith("_total") ? "counter" : "gauge";
    out.push(`# HELP ${name} ${g.help}`, `# TYPE ${name} ${type}`);
    if (Array.isArray(v)) for (const [labels, val] of v) out.push(`${name}{${key(labels)}} ${val}`);
    else out.push(`${name} ${v}`);
  }
  return out.join("\n") + "\n";
}

/** Serves /metrics on its own port (kept off the public game port). Returns the http server, or null when disabled. */
function serve(port, log) {
  if (!port) return null;
  const srv = http.createServer((req, res) => {
    if (req.url.split("?")[0] !== "/metrics") { res.writeHead(404); return res.end(); }
    res.writeHead(200, { "Content-Type": "text/plain; version=0.0.4; charset=utf-8" });
    res.end(render());
  });
  srv.on("error", (e) => log && log("metrics server error", e.message));
  srv.listen(port, () => log && log(`Metrics on :${port}/metrics`));
  return srv;
}

module.exports = { counter, histogram, gauge, render, serve };
