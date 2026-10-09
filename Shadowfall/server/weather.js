"use strict";
// Seasons and weather, shared by every player. The server decides; clients draw it (Weather.cs).
//
// Seasons follow the clock: each lasts SEASON_MINUTES (default 120) real minutes, spring -> summer -> autumn -> winter.
// Weather changes every 6-16 minutes, picked from what fits the season. "rain" and "storm" fall as snow where it's
// cold: everywhere in winter, and in the north (Whisperwood) in spring and autumn. The client decides that by region.

const SEASONS = ["spring", "summer", "autumn", "winter"];
const KINDS = ["clear", "cloudy", "rain", "storm", "fog"];

// Chances of each kind per season (clear, cloudy, rain, storm, fog).
const ODDS = {
  spring: [0.35, 0.25, 0.25, 0.05, 0.1],
  summer: [0.55, 0.15, 0.1, 0.15, 0.05],
  autumn: [0.2, 0.25, 0.3, 0.1, 0.15],
  winter: [0.2, 0.2, 0.4, 0.05, 0.15], // rain = snowfall, storm = blizzard
};

class Weather {
  /**
   * @param {{seasonMinutes?: number, now?: () => number, random?: () => number}} opts
   */
  constructor(opts = {}) {
    this.seasonMs = Math.max(1, opts.seasonMinutes || 120) * 60000;
    this.now = opts.now || Date.now;
    this.random = opts.random || Math.random;
    this.seasonOffset = 0;
    this.kind = "clear";
    this.intensity = 0.5;
    this.until = 0;
    this.lastSeason = this.season();
    this.roll();
  }

  /** Index 0..3 of the current season. */
  season() {
    const year = this.seasonMs * 4, t = this.now() + this.seasonOffset;
    return Math.floor((((t % year) + year) % year) / this.seasonMs);
  }

  /** Seconds until the next season. */
  seasonLeft() {
    const t = this.now() + this.seasonOffset;
    return Math.round((this.seasonMs - (((t % this.seasonMs) + this.seasonMs) % this.seasonMs)) / 1000);
  }

  /** New weather for the season. */
  roll() {
    const odds = ODDS[SEASONS[this.season()]];
    let r = this.random(), i = 0;
    while (i < odds.length - 1 && r >= odds[i]) r -= odds[i++];
    this.set(KINDS[i], 6 + this.random() * 10, 0.4 + this.random() * 0.6);
  }

  set(kind, minutes, intensity) {
    this.kind = kind;
    this.intensity = Math.round(Math.max(0.2, Math.min(1, intensity)) * 100) / 100;
    this.until = this.now() + minutes * 60000;
  }

  /** Called every few seconds: true when something changed (tell everyone). */
  tick() {
    const s = this.season();
    if (s !== this.lastSeason) { this.lastSeason = s; this.roll(); return true; }
    if (this.now() >= this.until) { this.roll(); return true; }
    return false;
  }

  /** Jumps to the start of a season (admin). */
  setSeason(index) {
    const t = this.now() + this.seasonOffset, year = this.seasonMs * 4;
    const pos = ((t % year) + year) % year;
    this.seasonOffset += index * this.seasonMs - pos + 1000; // just after it begins
    this.lastSeason = this.season();
    this.roll();
  }

  message() {
    return { t: "weather", s: this.season(), sky: this.kind, i: this.intensity, left: this.seasonLeft() };
  }
}

module.exports = { Weather, SEASONS, KINDS };
