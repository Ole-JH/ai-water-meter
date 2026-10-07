#!/usr/bin/env node
// Account administration from the command line (works while the server runs).
//
//   node admin-cli.js reset-code <account or character>   one-time password reset code (24 h)
//   node admin-cli.js admin <account> on|off               give or take admin rights
//   node admin-cli.js accounts [filter]                    list accounts and their characters
//
// In Docker:  docker compose exec shadowfall node admin-cli.js reset-code Alice   (or: task account:reset -- Alice)
"use strict";
const path = require("path");
const { createStore } = require("./store");
const A = require("./accounts");

const DATA_DIR = path.resolve(process.env.DATA_DIR || path.join(__dirname, "data"));

async function main() {
  const [cmd, arg, arg2] = process.argv.slice(2);
  if (!cmd) {
    console.log(require("fs").readFileSync(__filename, "utf8").split("\n").slice(1, 9).map((l) => l.replace(/^\/\/ ?/, "")).join("\n"));
    process.exit(2);
  }
  const store = createStore({ databaseUrl: process.env.DATABASE_URL || "", dataDir: DATA_DIR, log: () => {} });
  await store.init();
  const accountFor = async (name) => {
    let acc = await store.findAccount(name);
    if (!acc) { const ch = await store.findCharacter(name); if (ch) acc = await store.findAccountById(ch.accountId); }
    if (!acc) throw new Error(`No account or character called ${name}.`);
    return acc;
  };
  try {
    switch (cmd) {
      case "reset-code": {
        const acc = await accountFor(arg || "");
        const code = A.randomCode(2);
        await store.createResetToken(acc.id, A.tokenHash(code), "admin", new Date(Date.now() + 24 * 3600000));
        await store.logEvent(acc.id, acc.username, "admin_reset_code:console", null);
        console.log(`Reset code for account ${acc.username}: ${code}`);
        console.log(`It works once, for 24 hours: on the login screen choose "Forgot password?" > "I have a code".`);
        break;
      }
      case "admin": {
        const acc = await accountFor(arg || "");
        const on = arg2 !== "off";
        await store.updateAccount(acc.id, { admin: on });
        console.log(`${acc.username} is ${on ? "now" : "no longer"} an admin (takes effect at their next login).`);
        break;
      }
      case "accounts": {
        if (store.kind !== "postgres") {
          const fs = require("fs");
          for (const f of fs.readdirSync(path.join(DATA_DIR, "accounts")).sort()) {
            const a = JSON.parse(fs.readFileSync(path.join(DATA_DIR, "accounts", f), "utf8"));
            if (!arg || a.username.toLowerCase().includes(arg.toLowerCase())) console.log(`${a.username}${a.email ? ` <${a.email}>` : ""}: ${(a.characters || []).join(", ") || "-"}`);
          }
          break;
        }
        const { rows } = await store.pool.query(
          `SELECT a.username, a.email, a.last_login, string_agg(c.name || ' (' || c.level || ')', ', ' ORDER BY c.name) AS chars
           FROM accounts a LEFT JOIN characters c ON c.account_id = a.id AND c.deleted_at IS NULL
           WHERE $1 = '' OR a.username ILIKE '%' || $1 || '%' GROUP BY a.id ORDER BY a.username`, [arg || ""]);
        for (const r of rows) console.log(`${r.username}${r.email ? ` <${r.email}>` : ""}: ${r.chars || "-"}${r.last_login ? `  (last login ${r.last_login.toISOString().slice(0, 16)})` : ""}`);
        break;
      }
      default:
        throw new Error(`Unknown command ${cmd}. Commands: reset-code, admin, accounts`);
    }
  } finally {
    await store.close();
  }
}

main().catch((e) => { console.error(e.message); process.exit(1); });
