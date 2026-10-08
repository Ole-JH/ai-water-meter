// Pre-deploy browser check. Opens the game with ?sfcheck=1 in headless Chromium; the game (GameCheck.cs) then registers
// a throwaway account, creates a hero and walks to the well and the waystone, reporting each step in window.sfCheck.
// We take a screenshot at every "shot:" step, and write everything to OUT_DIR:
//   result.json  { ok, stage, detail, steps, errors, console, seconds }     NN-name.png   screenshots
// Exit code 0 = the build works, 1 = it doesn't (the reason is in result.json and on stdout).
//
//   CHECK_URL   the server to test (default http://localhost:7341/)
//   OUT_DIR     where to write (default ./out)
//   TIMEOUT_S   the whole check (default 480: software WebGL is slow)
const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright");

const url = new URL(process.env.CHECK_URL || "http://localhost:7341/");
url.searchParams.set("sfcheck", "1");
const outDir = process.env.OUT_DIR || path.join(__dirname, "out");
const timeoutMs = (parseInt(process.env.TIMEOUT_S || "480", 10) || 480) * 1000;
const loadTimeoutMs = Math.min(timeoutMs, 300 * 1000);

fs.mkdirSync(outDir, { recursive: true });
for (const f of fs.readdirSync(outDir)) if (/\.(png|json)$/.test(f)) fs.rmSync(path.join(outDir, f));

const started = Date.now();
const consoleLines = [];
let shots = 0;

function log(msg) { console.log(`[${((Date.now() - started) / 1000).toFixed(0).padStart(3)}s] ${msg}`); }

async function shot(page, name) {
  const file = `${String(++shots).padStart(2, "0")}-${name.replace(/[^a-z0-9-]/gi, "_")}.png`;
  // software rendering can take many seconds a frame in a busy scene: give the screenshot time
  try { await page.screenshot({ path: path.join(outDir, file), timeout: 90000 }); log(`screenshot ${file}`); } catch (e) { log(`screenshot ${file} failed: ${e.message}`); }
  return file;
}

async function main() {
  const browser = await chromium.launch({
    // Software WebGL (no GPU in a container).
    args: ["--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist", "--autoplay-policy=no-user-gesture-required"],
  });
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on("console", (m) => { if (m.type() === "error" || m.type() === "warning") consoleLines.push(`${m.type()}: ${m.text()}`.slice(0, 500)); });
  page.on("pageerror", (e) => consoleLines.push(`pageerror: ${e.message}`.slice(0, 500)));

  const result = { ok: false, url: url.toString(), stage: "load", detail: "", steps: [], errors: [], console: consoleLines, screenshots: [] };
  const finish = async (ok, stage, detail) => {
    result.ok = ok; result.stage = stage; result.detail = detail;
    result.seconds = Math.round((Date.now() - started) / 1000);
    if (!ok) result.screenshots.push(await shot(page, "fail"));
    fs.writeFileSync(path.join(outDir, "result.json"), JSON.stringify(result, null, 2));
    log(`${ok ? "PASS" : "FAIL"} at ${stage}: ${detail}`);
    await browser.close();
    process.exit(ok ? 0 : 1);
  };

  log(`opening ${url}`);
  try { await page.goto(url.toString(), { waitUntil: "load", timeout: 60000 }); }
  catch (e) { return finish(false, "load", `the page didn't load: ${e.message}`); }

  let lastStage = "", lastLog = 0;
  while (true) {
    const elapsed = Date.now() - started;
    let s = null;
    try {
      s = await page.evaluate(() => {
        const err = document.getElementById("error");
        const loadingError = err && getComputedStyle(err).display !== "none" ? (document.getElementById("errmsg") || {}).textContent : "";
        return { check: window.sfCheck || null, loadingError, loadingStage: (document.getElementById("stage") || {}).textContent || "" };
      });
    } catch (e) { /* the page is reloading (a new build); poll again */ }

    if (s && s.loadingError) return finish(false, "load", `the loading screen shows an error: ${s.loadingError.trim()}`);
    const c = s && s.check;
    if (c) {
      result.errors = c.errors || [];
      result.steps = (c.log || []).map((l) => `${l.stage}${l.detail ? ": " + l.detail : ""}`);
      if (c.stage && c.stage !== lastStage) {
        lastStage = c.stage;
        log(`${c.stage}${c.detail ? ": " + c.detail : ""}`);
        if (c.stage.startsWith("shot:")) {
          result.screenshots.push(await shot(page, c.stage.slice(5)));
          await page.evaluate((st) => { window.sfCheck.acked = st; }, c.stage).catch(() => {});
        }
        if (c.stage === "done") { result.screenshots.push(await shot(page, "done")); return finish(true, "done", c.detail); }
        if (c.stage === "fail") return finish(false, "game", c.detail);
      }
    } else if (elapsed > loadTimeoutMs) {
      return finish(false, "load", `the game didn't start within ${loadTimeoutMs / 1000} s (loading screen: "${s ? s.loadingStage : "?"}")`);
    } else if (elapsed - lastLog > 15000) {
      lastLog = elapsed;
      log(`loading... ${s ? s.loadingStage : ""}`);
    }
    if (elapsed > timeoutMs) return finish(false, lastStage || "load", `timed out after ${timeoutMs / 1000} s (last step: ${lastStage || "none"})`);
    await new Promise((r) => setTimeout(r, 250));
  }
}

main().catch((e) => { console.error(e); process.exit(2); });
