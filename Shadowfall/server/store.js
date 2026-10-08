// Account and character storage.
//
// Two interchangeable backends with the same async API:
//   - PgStore   (DATABASE_URL set): PostgreSQL. What Docker Compose runs.
//   - FileStore (no DATABASE_URL):  JSON files under DATA_DIR. For `task server:dev` and the smoke test.
//
// An account (username + password, optional email) owns any number of characters. Both stores import the
// old one-file-per-character format (data/characters/<name>.json with its own password) on first start:
// each old character becomes an account with the same name and password, plus that character.
"use strict";
const fs = require("fs");
const path = require("path");

class Taken extends Error {
  constructor(what) { super(`${what} is already taken`); this.code = "taken"; this.what = what; }
}

/** The account shape both stores return. */
function accountFrom(r) {
  return {
    id: r.id, username: r.username, email: r.email || null,
    salt: r.salt, hash: r.hash, recoverySalt: r.recoverySalt || null, recoveryHash: r.recoveryHash || null,
    admin: !!r.admin, createdAt: r.createdAt || null, lastLogin: r.lastLogin || null, sessionVersion: r.sessionVersion | 0,
  };
}

// =====================================================================================
// Old character files
// =====================================================================================

/** Old-format character files (they carry their own password hash). */
function legacyCharacters(dataDir) {
  const dir = path.join(dataDir, "characters");
  let files = [];
  try { files = fs.readdirSync(dir).filter((f) => f.endsWith(".json")); } catch { return []; }
  const out = [];
  for (const f of files) {
    try {
      const c = JSON.parse(fs.readFileSync(path.join(dir, f), "utf8"));
      if (c && c.name && c.hash && c.salt) out.push({ file: path.join(dir, f), c });
    } catch { /* unreadable: leave it alone */ }
  }
  return out;
}

const lookOf = (save) => (save && typeof save.look === "string" && save.look) || "Knight";
const levelOf = (save) => Math.max(1, Math.min(100, parseInt(save && save.level, 10) || 1));

// =====================================================================================
// File store
// =====================================================================================

class FileStore {
  constructor(dataDir, log) {
    this.kind = "files";
    this.dir = dataDir;
    this.log = log || (() => {});
    this.accDir = path.join(dataDir, "accounts");
    this.charDir = path.join(dataDir, "characters");
    this.resetFile = path.join(dataDir, "resets.json");
    this.auditFile = path.join(dataDir, "account-events.log");
    this.guildFile = path.join(dataDir, "guilds.json");
    this.writes = new Map(); // file -> promise chain, so writes to one file never interleave
  }

  async init() {
    fs.mkdirSync(this.accDir, { recursive: true });
    fs.mkdirSync(this.charDir, { recursive: true });
    const old = legacyCharacters(this.dir);
    for (const { file, c } of old) {
      if (this._read(this._accFile(c.name))) { this.log(`import: account ${c.name} already exists, keeping ${file}`); continue; }
      const now = new Date().toISOString();
      this._writeSync(this._accFile(c.name), {
        id: c.name.toLowerCase(), username: c.name, email: null, salt: c.salt, hash: c.hash, admin: c.admin === true,
        createdAt: c.created || now, lastLogin: c.lastLogin || null, sessionVersion: 0, characters: [c.name],
      });
      this._writeSync(file, {
        id: c.name.toLowerCase(), accountId: c.name.toLowerCase(), name: c.name, look: lookOf(c.save), level: levelOf(c.save),
        save: c.save || null, createdAt: c.created || now, lastPlayed: c.lastLogin || null,
      });
    }
    if (old.length) this.log(`Imported ${old.length} character(s) from the old format into accounts`);
  }

  _accFile(username) { return path.join(this.accDir, username.toLowerCase() + ".json"); }
  _charFile(name) { return path.join(this.charDir, name.toLowerCase() + ".json"); }
  _read(file) { try { return JSON.parse(fs.readFileSync(file, "utf8")); } catch { return null; } }
  _writeSync(file, obj) { fs.writeFileSync(file + ".tmp", JSON.stringify(obj)); fs.renameSync(file + ".tmp", file); }
  _write(file, obj) {
    const data = JSON.stringify(obj);
    const prev = this.writes.get(file) || Promise.resolve();
    const next = prev.then(() => fs.promises.writeFile(file + ".tmp", data)).then(() => fs.promises.rename(file + ".tmp", file));
    this.writes.set(file, next.catch(() => {}));
    return next;
  }

  async findAccount(username) {
    const a = this._read(this._accFile(String(username)));
    return a ? accountFrom(a) : null;
  }

  async findAccountById(id) { return this.findAccount(id); }

  async findAccountByEmail(email) {
    email = String(email || "").toLowerCase();
    if (!email) return null;
    for (const f of fs.readdirSync(this.accDir)) {
      const a = this._read(path.join(this.accDir, f));
      if (a && a.email && a.email.toLowerCase() === email) return accountFrom(a);
    }
    return null;
  }

  async createAccount({ username, salt, hash, recoverySalt, recoveryHash, email }) {
    const file = this._accFile(username);
    if (fs.existsSync(file)) throw new Taken("username");
    if (email && await this.findAccountByEmail(email)) throw new Taken("email");
    const a = { id: username.toLowerCase(), username, email: email || null, salt, hash, recoverySalt, recoveryHash, admin: false,
      createdAt: new Date().toISOString(), lastLogin: null, sessionVersion: 0, characters: [] };
    this._writeSync(file, a);
    return accountFrom(a);
  }

  async updateAccount(id, patch) {
    const file = this._accFile(id);
    const a = this._read(file);
    if (!a) return;
    if (patch.email && patch.email !== a.email) {
      const other = await this.findAccountByEmail(patch.email);
      if (other && other.id !== a.id) throw new Taken("email");
    }
    Object.assign(a, patch);
    await this._write(file, a);
  }

  async listCharacters(accountId) {
    const a = this._read(this._accFile(accountId));
    if (!a) return [];
    return (a.characters || []).map((n) => this._read(this._charFile(n))).filter(Boolean)
      .map((c) => ({ id: c.id, name: c.name, look: c.look, level: c.level, lastPlayed: c.lastPlayed }));
  }

  async findCharacter(name) {
    const c = this._read(this._charFile(String(name)));
    return c ? { id: c.id, accountId: c.accountId, name: c.name, look: c.look, level: c.level, save: c.save || null } : null;
  }

  async createCharacter(accountId, name, look) {
    const file = this._charFile(name);
    if (fs.existsSync(file)) throw new Taken("name");
    const c = { id: name.toLowerCase(), accountId, name, look, level: 1, save: null, createdAt: new Date().toISOString(), lastPlayed: null };
    this._writeSync(file, c);
    const accFile = this._accFile(accountId);
    const a = this._read(accFile);
    a.characters = [...(a.characters || []), name];
    this._writeSync(accFile, a);
    return { id: c.id, accountId, name, look, level: 1, save: null };
  }

  async saveCharacter(id, save) {
    const file = this._charFile(id);
    const c = this._read(file);
    if (!c) return;
    c.save = save;
    c.level = levelOf(save);
    c.lastPlayed = new Date().toISOString();
    await this._write(file, c);
  }

  async deleteCharacter(id) {
    const c = this._read(this._charFile(id));
    if (!c) return;
    const graveyard = path.join(this.dir, "deleted-characters");
    fs.mkdirSync(graveyard, { recursive: true });
    fs.renameSync(this._charFile(id), path.join(graveyard, `${c.id}-${Date.now()}.json`));
    const accFile = this._accFile(c.accountId);
    const a = this._read(accFile);
    if (a) { a.characters = (a.characters || []).filter((n) => n.toLowerCase() !== c.id); this._writeSync(accFile, a); }
  }

  _resets() { return this._read(this.resetFile) || {}; }

  async createResetToken(accountId, tokenHash, kind, expiresAt) {
    const all = this._resets();
    const now = Date.now();
    for (const [k, v] of Object.entries(all)) if (v.expiresAt < now || v.usedAt) delete all[k];
    all[tokenHash] = { accountId, kind, expiresAt: expiresAt.getTime(), usedAt: null };
    this._writeSync(this.resetFile, all);
  }

  async useResetToken(tokenHash, accountId) {
    const all = this._resets();
    const t = all[tokenHash];
    if (!t || t.usedAt || t.expiresAt < Date.now() || t.accountId !== accountId) return false;
    t.usedAt = Date.now();
    this._writeSync(this.resetFile, all);
    return true;
  }

  async logEvent(accountId, username, event, ip) {
    fs.appendFile(this.auditFile, JSON.stringify({ at: new Date().toISOString(), accountId, username, event, ip }) + "\n", () => {});
  }

  // Guilds (guild.js): one file with all of them, keyed by lower-case name.
  async loadGuilds() { return Object.values(this._read(this.guildFile) || {}); }
  async saveGuild(g) {
    const all = this._read(this.guildFile) || {};
    all[g.name.toLowerCase()] = g;
    await this._write(this.guildFile, all);
  }
  async deleteGuild(name) {
    const all = this._read(this.guildFile) || {};
    delete all[String(name).toLowerCase()];
    await this._write(this.guildFile, all);
  }

  async counts() {
    const n = (d) => { try { return fs.readdirSync(d).filter((f) => f.endsWith(".json")).length; } catch { return 0; } };
    return { accounts: n(this.accDir), characters: n(this.charDir) };
  }

  async flush() { await Promise.all([...this.writes.values()]); }
  async close() { await this.flush(); }
}

// =====================================================================================
// PostgreSQL store
// =====================================================================================

const MIGRATIONS = [
  // 1: accounts, characters, password resets, an audit log
  `CREATE TABLE accounts (
     id              bigserial PRIMARY KEY,
     username        text NOT NULL,
     email           text,
     pass_salt       text NOT NULL,
     pass_hash       text NOT NULL,
     recovery_salt   text,
     recovery_hash   text,
     admin           boolean NOT NULL DEFAULT false,
     created_at      timestamptz NOT NULL DEFAULT now(),
     last_login      timestamptz,
     session_version integer NOT NULL DEFAULT 0
   );
   CREATE UNIQUE INDEX accounts_username_key ON accounts (lower(username));
   CREATE UNIQUE INDEX accounts_email_key ON accounts (lower(email)) WHERE email IS NOT NULL;

   CREATE TABLE characters (
     id          bigserial PRIMARY KEY,
     account_id  bigint NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
     name        text NOT NULL,
     look        text NOT NULL,
     level       integer NOT NULL DEFAULT 1,
     save        jsonb,
     created_at  timestamptz NOT NULL DEFAULT now(),
     last_played timestamptz,
     deleted_at  timestamptz
   );
   CREATE UNIQUE INDEX characters_name_key ON characters (lower(name)) WHERE deleted_at IS NULL;
   CREATE INDEX characters_account_idx ON characters (account_id);

   CREATE TABLE password_resets (
     token_hash  text PRIMARY KEY,
     account_id  bigint NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
     kind        text NOT NULL,
     created_at  timestamptz NOT NULL DEFAULT now(),
     expires_at  timestamptz NOT NULL,
     used_at     timestamptz
   );

   CREATE TABLE account_events (
     id          bigserial PRIMARY KEY,
     account_id  bigint REFERENCES accounts(id) ON DELETE SET NULL,
     username    text,
     event       text NOT NULL,
     ip          text,
     at          timestamptz NOT NULL DEFAULT now()
   );
   CREATE INDEX account_events_account_idx ON account_events (account_id, at);

   CREATE TABLE meta (key text PRIMARY KEY, value text);`,
  // 2: guilds (guild.js): name, tag, members and ranks as one JSON document each
  `CREATE TABLE guilds (
     key         text PRIMARY KEY,
     data        jsonb NOT NULL,
     updated_at  timestamptz NOT NULL DEFAULT now()
   );`,
];

const ACCOUNT_COLS = `id::text AS id, username, email, pass_salt AS salt, pass_hash AS hash, recovery_salt AS "recoverySalt",
  recovery_hash AS "recoveryHash", admin, created_at AS "createdAt", last_login AS "lastLogin", session_version AS "sessionVersion"`;
const ACCOUNT_FIELDS = { email: "email", salt: "pass_salt", hash: "pass_hash", recoverySalt: "recovery_salt", recoveryHash: "recovery_hash",
  lastLogin: "last_login", sessionVersion: "session_version", admin: "admin" };

class PgStore {
  constructor(url, dataDir, log) {
    this.kind = "postgres";
    const { Pool } = require("pg");
    this.pool = new Pool({ connectionString: url, max: 10 });
    this.pool.on("error", (e) => log && log("postgres error", e.message));
    this.dir = dataDir;
    this.log = log || (() => {});
    this.pending = new Set();
  }

  async init() {
    // Postgres may still be starting (docker compose): retry for a while.
    for (let i = 0; ; i++) {
      try { await this.pool.query("SELECT 1"); break; } catch (e) {
        if (i >= 30) throw e;
        if (i === 0) this.log(`Waiting for PostgreSQL... (${e.message})`);
        await new Promise((r) => setTimeout(r, 1000));
      }
    }
    const client = await this.pool.connect();
    try {
      // One server migrates at a time.
      await client.query("SELECT pg_advisory_lock(7341)");
      await client.query("CREATE TABLE IF NOT EXISTS schema_migrations (version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now())");
      const { rows } = await client.query("SELECT coalesce(max(version), 0) AS v FROM schema_migrations");
      for (let v = rows[0].v + 1; v <= MIGRATIONS.length; v++) {
        await client.query("BEGIN");
        await client.query(MIGRATIONS[v - 1]);
        await client.query("INSERT INTO schema_migrations (version) VALUES ($1)", [v]);
        await client.query("COMMIT");
        this.log(`Database migrated to version ${v}`);
      }
      await this._importFiles(client);
    } catch (e) {
      try { await client.query("ROLLBACK"); } catch { /* not in a transaction */ }
      throw e;
    } finally {
      try { await client.query("SELECT pg_advisory_unlock(7341)"); } catch { /* ignore */ }
      client.release();
    }
  }

  /** Imports characters from files (the old format, or a FileStore's accounts) once, into an empty database. */
  async _importFiles(client) {
    const done = await client.query("SELECT value FROM meta WHERE key = 'files_imported'");
    if (done.rows.length) return;
    const legacy = legacyCharacters(this.dir);
    const accDir = path.join(this.dir, "accounts");
    let fileAccounts = [];
    try { fileAccounts = fs.readdirSync(accDir).filter((f) => f.endsWith(".json")).map((f) => JSON.parse(fs.readFileSync(path.join(accDir, f), "utf8"))); } catch { /* none */ }
    await client.query("BEGIN");
    let accounts = 0, characters = 0;
    const charFile = (n) => path.join(this.dir, "characters", n.toLowerCase() + ".json");
    const insertAccount = async (a) => {
      const r = await client.query(
        `INSERT INTO accounts (username, email, pass_salt, pass_hash, recovery_salt, recovery_hash, admin, created_at, last_login)
         VALUES ($1, $2, $3, $4, $5, $6, $7, coalesce($8::timestamptz, now()), $9) ON CONFLICT DO NOTHING RETURNING id`,
        [a.username, a.email || null, a.salt, a.hash, a.recoverySalt || null, a.recoveryHash || null, a.admin === true, a.createdAt || null, a.lastLogin || null]);
      if (r.rows.length) accounts++;
      return r.rows.length ? r.rows[0].id : null;
    };
    const insertChar = async (accountId, name, save, createdAt, lastPlayed) => {
      const r = await client.query(
        `INSERT INTO characters (account_id, name, look, level, save, created_at, last_played)
         VALUES ($1, $2, $3, $4, $5, coalesce($6::timestamptz, now()), $7) ON CONFLICT DO NOTHING RETURNING id`,
        [accountId, name, lookOf(save), levelOf(save), save ? JSON.stringify(save) : null, createdAt || null, lastPlayed || null]);
      if (r.rows.length) characters++;
    };
    for (const { c } of legacy) {
      const id = await insertAccount({ username: c.name, salt: c.salt, hash: c.hash, admin: c.admin, createdAt: c.created, lastLogin: c.lastLogin });
      if (id) await insertChar(id, c.name, c.save || null, c.created, c.lastLogin);
    }
    for (const a of fileAccounts) {
      const id = await insertAccount(a);
      if (!id) continue;
      for (const n of a.characters || []) {
        let c = null;
        try { c = JSON.parse(fs.readFileSync(charFile(n), "utf8")); } catch { continue; }
        if (c && !c.hash) await insertChar(id, c.name, c.save || null, c.createdAt, c.lastPlayed);
      }
    }
    await client.query("INSERT INTO meta (key, value) VALUES ('files_imported', now()::text)");
    await client.query("COMMIT");
    if (accounts || characters) this.log(`Imported ${accounts} account(s) and ${characters} character(s) from ${this.dir} into PostgreSQL (the files are kept as a backup)`);
  }

  _track(p) { this.pending.add(p); p.finally(() => this.pending.delete(p)).catch(() => {}); return p; }

  async findAccount(username) {
    const { rows } = await this.pool.query(`SELECT ${ACCOUNT_COLS} FROM accounts WHERE lower(username) = lower($1)`, [String(username)]);
    return rows.length ? accountFrom(rows[0]) : null;
  }

  async findAccountById(id) {
    const { rows } = await this.pool.query(`SELECT ${ACCOUNT_COLS} FROM accounts WHERE id = $1`, [id]);
    return rows.length ? accountFrom(rows[0]) : null;
  }

  async findAccountByEmail(email) {
    const { rows } = await this.pool.query(`SELECT ${ACCOUNT_COLS} FROM accounts WHERE lower(email) = lower($1)`, [String(email || "")]);
    return rows.length ? accountFrom(rows[0]) : null;
  }

  async createAccount({ username, salt, hash, recoverySalt, recoveryHash, email }) {
    try {
      const { rows } = await this.pool.query(
        `INSERT INTO accounts (username, email, pass_salt, pass_hash, recovery_salt, recovery_hash)
         VALUES ($1, $2, $3, $4, $5, $6) RETURNING ${ACCOUNT_COLS}`,
        [username, email || null, salt, hash, recoverySalt || null, recoveryHash || null]);
      return accountFrom(rows[0]);
    } catch (e) {
      if (e.code === "23505") throw new Taken(/email/.test(e.constraint || "") ? "email" : "username");
      throw e;
    }
  }

  async updateAccount(id, patch) {
    const sets = [], vals = [];
    for (const [k, v] of Object.entries(patch)) {
      if (!ACCOUNT_FIELDS[k]) continue;
      vals.push(v);
      sets.push(`${ACCOUNT_FIELDS[k]} = $${vals.length}`);
    }
    if (!sets.length) return;
    vals.push(id);
    try {
      await this._track(this.pool.query(`UPDATE accounts SET ${sets.join(", ")} WHERE id = $${vals.length}`, vals));
    } catch (e) {
      if (e.code === "23505") throw new Taken("email");
      throw e;
    }
  }

  async listCharacters(accountId) {
    const { rows } = await this.pool.query(
      `SELECT id::text AS id, name, look, level, last_played AS "lastPlayed" FROM characters
       WHERE account_id = $1 AND deleted_at IS NULL ORDER BY last_played DESC NULLS LAST, id`, [accountId]);
    return rows;
  }

  async findCharacter(name) {
    const { rows } = await this.pool.query(
      `SELECT id::text AS id, account_id::text AS "accountId", name, look, level, save FROM characters
       WHERE lower(name) = lower($1) AND deleted_at IS NULL`, [String(name)]);
    return rows[0] || null;
  }

  async createCharacter(accountId, name, look) {
    try {
      const { rows } = await this.pool.query(
        `INSERT INTO characters (account_id, name, look) VALUES ($1, $2, $3)
         RETURNING id::text AS id, account_id::text AS "accountId", name, look, level, save`, [accountId, name, look]);
      return rows[0];
    } catch (e) {
      if (e.code === "23505") throw new Taken("name");
      throw e;
    }
  }

  async saveCharacter(id, save) {
    await this._track(this.pool.query("UPDATE characters SET save = $2, level = $3, last_played = now() WHERE id = $1",
      [id, JSON.stringify(save), levelOf(save)]));
  }

  async deleteCharacter(id) {
    await this.pool.query("UPDATE characters SET deleted_at = now() WHERE id = $1", [id]);
  }

  async createResetToken(accountId, tokenHash, kind, expiresAt) {
    await this.pool.query("DELETE FROM password_resets WHERE expires_at < now() - interval '7 days'");
    await this.pool.query("INSERT INTO password_resets (token_hash, account_id, kind, expires_at) VALUES ($1, $2, $3, $4)",
      [tokenHash, accountId, kind, expiresAt]);
  }

  async useResetToken(tokenHash, accountId) {
    const { rows } = await this.pool.query(
      `UPDATE password_resets SET used_at = now()
       WHERE token_hash = $1 AND account_id = $2 AND used_at IS NULL AND expires_at > now() RETURNING kind`, [tokenHash, accountId]);
    return rows.length > 0;
  }

  async logEvent(accountId, username, event, ip) {
    this._track(this.pool.query("INSERT INTO account_events (account_id, username, event, ip) VALUES ($1, $2, $3, $4)",
      [accountId || null, username || null, event, ip || null])).catch((e) => this.log("audit log failed", e.message));
  }

  async loadGuilds() {
    const { rows } = await this.pool.query("SELECT data FROM guilds");
    return rows.map((r) => r.data);
  }
  async saveGuild(g) {
    await this._track(this.pool.query(
      "INSERT INTO guilds (key, data) VALUES ($1, $2) ON CONFLICT (key) DO UPDATE SET data = $2, updated_at = now()",
      [g.name.toLowerCase(), JSON.stringify(g)]));
  }
  async deleteGuild(name) {
    await this._track(this.pool.query("DELETE FROM guilds WHERE key = $1", [String(name).toLowerCase()]));
  }

  async counts() {
    const { rows } = await this.pool.query(
      "SELECT (SELECT count(*) FROM accounts)::int AS accounts, (SELECT count(*) FROM characters WHERE deleted_at IS NULL)::int AS characters");
    return rows[0];
  }

  async flush() { await Promise.allSettled([...this.pending]); }
  async close() { await this.flush(); await this.pool.end(); }
}

function createStore({ databaseUrl, dataDir, log }) {
  return databaseUrl ? new PgStore(databaseUrl, dataDir, log) : new FileStore(dataDir, log);
}

module.exports = { createStore, Taken, MIGRATIONS };
