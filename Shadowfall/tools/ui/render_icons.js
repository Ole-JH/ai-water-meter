// Renders game-icons.net SVGs (CC BY 3.0) into styled 128px PNGs for the in-game UI.
//   node tools/ui/render_icons.js <path-to-game-icons-repo>
// Needs playwright-core and a Chromium (set CHROMIUM_PATH). Output: Assets/Resources/UI/Icons/*.png
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright-core');

const SRC = process.argv[2];
const OUT = path.join(__dirname, '..', '..', 'Assets', 'Resources', 'UI', 'Icons');

// key: [svg path in repo, background color (null = transparent item icon), glyph tint]
const ICONS = {
  // abilities
  cleave: ['lorc/sword-slice', '#7a2a22', '#fff2e0'],
  fireball: ['lorc/fireball', '#b4470f', '#fff1c4'],
  frostnova: ['lorc/frozen-orb', '#1f5f8f', '#e6f8ff'],
  heal: ['delapouite/healing', '#8c7418', '#fffbe0'],
  meteor: ['lorc/meteor-impact', '#6e1810', '#ffd9b0'],
  // Knight
  shield_bash: ['delapouite/shield-bash', '#3b4a66', '#eef2ff'],
  holy_bolt: ['lorc/sun-radiations', '#8c7418', '#fffbe0'],
  consecration: ['lorc/sun', '#9a6a10', '#fff4c8'],
  divine_shield: ['lorc/bell-shield', '#2f5a8a', '#f0f8ff'],
  judgement: ['lorc/hammer-drop', '#7a5a10', '#fff2c0'],
  // Barbarian
  throwing_axe: ['lorc/axe-swing', '#6a3420', '#ffeede'],
  whirlwind: ['lorc/whirlwind', '#7a2a22', '#fff2e0'],
  leap: ['delapouite/jump-across', '#5a3a20', '#ffe6c8'],
  war_cry: ['lorc/shouting', '#8a2a18', '#ffe0c8'],
  // Mage
  chain_lightning: ['lorc/lightning-arc', '#2a3a8a', '#e6f0ff'],
  teleport: ['lorc/teleport', '#3e2c6e', '#efe6ff'],
  // Rogue
  twin_strike: ['lorc/crossed-swords', '#2c4a34', '#e6ffe9'],
  multishot: ['lorc/striking-arrows', '#3a4a2a', '#f0ffe0'],
  fan_of_knives: ['lorc/spinning-blades', '#2a3a3a', '#e6fbff'],
  smoke_bomb: ['lorc/hood', '#2a2a32', '#e6e6f0'],
  rain_of_arrows: ['lorc/arrow-scope', '#3a3a20', '#fffbe0'],
  // consumables
  health_potion: ['delapouite/health-potion', null, '#ff6a5c'],
  mana_potion: ['delapouite/magic-potion', null, '#6fa8ff'],
  raw_fish: ['delapouite/tropical-fish', null, '#c9d6e0'],
  cooked_fish: ['darkzaitzev/fried-fish', null, '#e8b46a'],
  burnt_fish: ['darkzaitzev/fried-fish', null, '#5a4434'],
  // equipment
  sword: ['lorc/broadsword', null, '#e8edf2'],
  axe: ['lorc/battle-axe', null, '#e8edf2'],
  mace: ['delapouite/flanged-mace', null, '#e8edf2'],
  dagger: ['lorc/plain-dagger', null, '#e8edf2'],
  helm: ['lorc/visored-helm', null, '#e3e6ea'],
  chest: ['lorc/breastplate', null, '#e3e6ea'],
  gloves: ['delapouite/gauntlet', null, '#e3e6ea'],
  legs: ['delapouite/leg-armor', null, '#e3e6ea'],
  boots: ['lorc/boots', null, '#e3d2b8'],
  ring: ['delapouite/ring', null, '#ffd76a'],
  amulet: ['lorc/gem-pendant', null, '#9fe0ff'],
  // materials
  logs: ['delapouite/log', null, '#d29a5c'],
  ore: ['faithtoken/ore', null, '#d9c1a5'],
  gold: ['delapouite/two-coins', null, '#ffd24a'],
  gem: ['lorc/emerald', null, '#ffffff'],
  // skills
  woodcutting: ['lorc/wood-axe', '#3f6a2a', '#eaffd8'],
  mining: ['lorc/mining', '#5e5348', '#f0e6da'],
  fishing: ['delapouite/fishing-pole', '#2a5878', '#e0f2ff'],
  smithing: ['lorc/anvil', '#7a4a20', '#ffe6c8'],
  cooking: ['delapouite/cooking-pot', '#7a2f22', '#ffe0d0'],
  // menu buttons
  bags: ['lorc/knapsack', '#4a3524', '#f3e3c8'],
  character: ['delapouite/person', '#4a3524', '#f3e3c8'],
  skills: ['delapouite/skills', '#4a3524', '#f3e3c8'],
  quests: ['lorc/scroll-unfurled', '#4a3524', '#f3e3c8'],
  map: ['lorc/treasure-map', '#4a3524', '#f3e3c8'],
  help: ['sbed/help', '#4a3524', '#f3e3c8'],
  talents: ['delapouite/upgrade', '#4a3524', '#f3e3c8'],
  stash: ['lorc/locked-chest', '#4a3524', '#f3e3c8'],
  trade: ['delapouite/shaking-hands', '#4a3524', '#f3e3c8'],
  companions: ['lorc/paw', '#4a3524', '#f3e3c8'],
  menu: ['lorc/cog', '#4a3524', '#f3e3c8'],
  // companions
  companion_hound: ['lorc/wolf-head', '#5a3a20', '#ffe6c8'],
  companion_squire: ['lorc/visored-helm', '#3b4a66', '#eef2ff'],
  companion_witch: ['lorc/witch-flight', '#2c4a2a', '#eaffe0'],
  companion_ranger: ['lorc/bowman', '#3a4a2a', '#f0ffe0'],
  companion_acolyte: ['lorc/prayer', '#8c7418', '#fffbe0'],
  companion_golem: ['delapouite/golem-head', '#3a3f4e', '#e6ecff'],
  achievements: ['lorc/trophy', '#4a3524', '#f3e3c8'],
  // achievements
  ach_trophy: ['lorc/trophy', '#6a5418', '#ffe9a0'],
  ach_kills: ['lorc/skull-crossed-bones', '#5a1a14', '#ffe0d6'],
  ach_wolf: ['lorc/wolf-howl', '#3a4048', '#e8eef5'],
  ach_goblin: ['delapouite/goblin-head', '#2f4a22', '#e8ffd8'],
  ach_bones: ['lorc/crossed-bones', '#4a4440', '#f5efe6'],
  ach_elite: ['lorc/medal', '#2a3e6e', '#e6eeff'],
  ach_boss: ['lorc/crowned-skull', '#4a1a4a', '#ffe6ff'],
  ach_lich: ['lorc/ghost', '#1f3e5a', '#e0f6ff'],
  ach_bandit: ['lorc/hood', '#3a2a20', '#ffeede'],
  ach_golem: ['delapouite/rock-golem', '#4a4238', '#f0ebe0'],
  ach_level: ['lorc/laurel-crown', '#6a5418', '#fff2c0'],
  ach_wealth: ['delapouite/two-coins', '#6a5418', '#ffe08a'],
  ach_legendary: ['lorc/sparkling-sabre', '#7a4410', '#ffe6c0'],
  ach_set: ['lorc/bolt-shield', '#1f5a3a', '#e0ffe8'],
  ach_gem: ['lorc/crystal-cluster', '#3a2a6e', '#efe6ff'],
  ach_death: ['lorc/tombstone', '#2a2a30', '#e6e6ee'],
  ach_explore: ['lorc/treasure-map', '#5a4a2a', '#fff0d0'],
  ach_compass: ['lorc/compass', '#2a4a5a', '#e0f4ff'],
  ach_footprint: ['lorc/footprint', '#4a3a2a', '#ffeedd'],
  ach_dungeon: ['delapouite/dungeon-gate', '#3a3030', '#f0e6e6'],
  ach_stairs: ['delapouite/stairs-goal', '#30304a', '#e6e6ff'],
  ach_hell: ['lorc/dragon-head', '#7a1a10', '#ffd9c0'],
  ach_anvil: ['lorc/anvil', '#4a4a52', '#eef0f5'],
  ach_cook: ['lorc/cauldron', '#6a3a10', '#ffe6c8'],
  ach_axe: ['lorc/wood-axe', '#4a3a20', '#fff0d8'],
  ach_mine: ['lorc/mining', '#4a4040', '#f5ebe6'],
  ach_fish: ['delapouite/circling-fish', '#1f4a5a', '#e0f6ff'],
  ach_party: ['delapouite/three-friends', '#2a4a3a', '#e0ffee'],
  ach_trade: ['delapouite/shaking-hands', '#4a3a2a', '#fff0dd'],
  ach_emote: ['lorc/two-shadows', '#4a2a4a', '#ffe6ff'],
  ach_companion: ['lorc/wolf-head', '#3a3a2a', '#fffbe0'],
  ach_quest: ['lorc/scroll-unfurled', '#5a4a2a', '#fff0d0'],
  ach_castle: ['lorc/castle', '#4a3a30', '#fff0e6'],
  ach_grave: ['lorc/coffin', '#2a2a2a', '#eeeeee'],
  // hero classes
  knight: ['delapouite/knight-banner', '#3b4a66', '#eef2ff'],
  barbarian: ['delapouite/barbarian', '#6a3420', '#ffeede'],
  mage: ['lorc/wizard-staff', '#3e2c6e', '#efe6ff'],
  rogue: ['darkzaitzev/hooded-assassin', '#2c4a34', '#e6ffe9'],
};

function glyph(svgText) {
  // game-icons SVGs: a black background square followed by the white glyph path(s)
  const paths = [...svgText.matchAll(/<path[^>]*d="([^"]+)"[^>]*\/>/g)].map(m => m[1]);
  return paths.filter(d => !/^M0 0h512v512H0z$/.test(d)).map(d => `<path d="${d}"/>`).join('');
}

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  let html = '<html><body style="margin:0;background:transparent">';
  for (const [key, [src, bg, tint]] of Object.entries(ICONS)) {
    const svg = fs.readFileSync(path.join(SRC, src + '.svg'), 'utf8');
    const back = bg
      ? `background: radial-gradient(circle at 35% 28%, ${bg}ee, ${bg}88 55%, #120c08 100%); border-radius: 22px;
         box-shadow: inset 0 0 0 4px #00000055, inset 0 3px 6px #ffffff33;`
      : 'background: transparent;';
    html += `<div id="${key}" style="width:128px;height:128px;display:inline-block;${back}">
      <svg viewBox="0 0 512 512" width="128" height="128" style="padding:${bg ? 18 : 6}px;box-sizing:border-box">
        <defs><linearGradient id="g_${key}" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stop-color="#ffffff"/><stop offset="1" stop-color="${tint}"/></linearGradient>
          <filter id="s_${key}" x="-10%" y="-10%" width="130%" height="130%">
          <feDropShadow dx="0" dy="10" stdDeviation="10" flood-color="#000" flood-opacity="0.65"/></filter></defs>
        <g fill="url(#g_${key})" filter="url(#s_${key})">${glyph(svg)}</g></svg></div>`;
  }
  html += '</body></html>';
  const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH, args: ['--no-sandbox'] });
  const page = await browser.newPage({ viewport: { width: 1024, height: 1024 } });
  await page.setContent(html);
  for (const key of Object.keys(ICONS)) {
    await page.locator('#' + key).screenshot({ path: path.join(OUT, key + '.png'), omitBackground: true });
  }
  await browser.close();
  console.log(`Rendered ${Object.keys(ICONS).length} icons to ${OUT}`);
})();
