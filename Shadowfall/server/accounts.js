// Account security helpers: password hashing, recovery and reset codes, login rate limits, optional email.
"use strict";
const crypto = require("crypto");
const { promisify } = require("util");

const scrypt = promisify(crypto.scrypt);

const USERNAME_RE = /^[A-Za-z][A-Za-z0-9_]{2,15}$/;
const NAME_RULE = "3-16 letters, digits or _, starting with a letter";
const MIN_PASSWORD = 6, MAX_PASSWORD = 128;
const EMAIL_RE = /^[^\s@<>"]{1,64}@[^\s@<>"]{1,190}\.[A-Za-z]{2,24}$/;

// Codes are shown to players and typed back in: no 0/O or 1/I/L.
const CODE_ALPHABET = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

function randomCode(groups, size = 4) {
  const out = [];
  for (let g = 0; g < groups; g++) {
    let s = "";
    for (let i = 0; i < size; i++) s += CODE_ALPHABET[crypto.randomInt(CODE_ALPHABET.length)];
    out.push(s);
  }
  return out.join("-");
}

/** Upper-case, without spaces or dashes, so "abcd efgh" matches "ABCD-EFGH". */
const normalizeCode = (code) => String(code || "").toUpperCase().replace(/[^A-Z0-9]/g, "");

async function hashSecret(secret, salt) {
  salt = salt || crypto.randomBytes(16).toString("hex");
  const hash = (await scrypt(secret, salt, 32)).toString("hex");
  return { salt, hash };
}

async function checkSecret(secret, salt, hash) {
  if (!salt || !hash) return false;
  const a = await scrypt(secret, salt, 32), b = Buffer.from(hash, "hex");
  return a.length === b.length && crypto.timingSafeEqual(a, b);
}

const hashPassword = (pass, salt) => hashSecret(pass, salt);
const checkPassword = (acc, pass) => checkSecret(pass, acc.salt, acc.hash);
/** Recovery codes are stored like passwords. */
const hashRecovery = (code) => hashSecret(normalizeCode(code));
const checkRecovery = (acc, code) => checkSecret(normalizeCode(code), acc.recoverySalt, acc.recoveryHash);
/** One-time reset codes (email, admin) are looked up by hash, so a plain SHA-256 is enough (they're random and short-lived). */
const tokenHash = (code) => crypto.createHash("sha256").update(normalizeCode(code)).digest("hex");

function passwordProblem(pass) {
  if (typeof pass !== "string" || pass.length < MIN_PASSWORD) return `Passwords must be at least ${MIN_PASSWORD} characters.`;
  if (pass.length > MAX_PASSWORD) return `Passwords can be at most ${MAX_PASSWORD} characters.`;
  return null;
}

// =====================================================================================
// Rate limits: a few wrong passwords lock an account briefly; many from one address block that address.
// =====================================================================================

class Limiter {
  constructor({ max, windowMs, lockMs }) {
    this.max = max; this.windowMs = windowMs; this.lockMs = lockMs;
    this.hits = new Map(); // key -> { n, first, lockedUntil }
  }
  /** Seconds until the key may try again, or 0. */
  blocked(key) {
    const h = this.hits.get(key);
    if (!h) return 0;
    const now = Date.now();
    if (h.lockedUntil > now) return Math.ceil((h.lockedUntil - now) / 1000);
    if (now - h.first > this.windowMs) this.hits.delete(key);
    return 0;
  }
  fail(key) {
    const now = Date.now();
    let h = this.hits.get(key);
    if (!h || now - h.first > this.windowMs) { h = { n: 0, first: now, lockedUntil: 0 }; this.hits.set(key, h); }
    if (++h.n >= this.max) { h.lockedUntil = now + this.lockMs; h.n = 0; h.first = now; }
  }
  clear(key) { this.hits.delete(key); }
  prune() { const now = Date.now(); for (const [k, h] of this.hits) if (h.lockedUntil < now && now - h.first > this.windowMs) this.hits.delete(k); }
}

// =====================================================================================
// Email (optional): set SMTP_URL, e.g. smtps://user:app-password@smtp.gmail.com:465, and MAIL_FROM.
// =====================================================================================

function createMailer({ smtpUrl, from, publicUrl, log }) {
  if (!smtpUrl) return null;
  const nodemailer = require("nodemailer");
  const transport = nodemailer.createTransport(smtpUrl);
  return {
    async sendReset(to, username, code, minutes) {
      const link = publicUrl ? `${publicUrl.replace(/\/+$/, "")}/?reset=${encodeURIComponent(code)}&user=${encodeURIComponent(username)}` : null;
      const text = [
        `Hello ${username},`,
        "",
        "Someone (hopefully you) asked to reset the password of your Shadowfall account.",
        "",
        `Your reset code: ${code}`,
        "",
        link ? `Open ${link} or enter the code on the login screen (Forgot password? > I have a code).`
             : "Enter it on the login screen: Forgot password? > I have a code.",
        `The code works once and expires in ${minutes} minutes.`,
        "",
        "If you didn't ask for this, ignore this email: your password stays the same.",
      ].join("\n");
      await transport.sendMail({ from: from || "Shadowfall <no-reply@localhost>", to, subject: "Your Shadowfall password reset code", text });
      log && log(`Sent a password reset email for ${username}`);
    },
  };
}

module.exports = {
  USERNAME_RE, NAME_RULE, EMAIL_RE, MIN_PASSWORD,
  randomCode, normalizeCode, hashPassword, checkPassword, hashRecovery, checkRecovery, tokenHash, passwordProblem,
  Limiter, createMailer,
};
