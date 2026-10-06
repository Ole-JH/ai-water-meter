"use strict";
// Dungeon generator: rooms connected by corridors on a tile grid, generated on the server and sent to the
// clients (who build the visuals from it). Deterministic for a given seed.

const W = 72, H = 72;

function mulberry32(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/**
 * Generates one dungeon level.
 * @returns {{w,h,blocked:Uint8Array,rooms:{x,y,w,h}[],start:number[],exit:number[],stairs:number[]|null,boss:number[]|null,chests:number[][],packs:{x,z,n}[]}}
 */
function generate(seed, depth, lastDepth) {
  const rnd = mulberry32(seed);
  const ri = (a, b) => a + Math.floor(rnd() * (b - a + 1));
  const blocked = new Uint8Array(W * H).fill(1);
  const carve = (x, y) => { if (x > 0 && y > 0 && x < W - 1 && y < H - 1) blocked[y * W + x] = 0; };

  // Rooms
  const rooms = [];
  for (let attempt = 0; attempt < 400 && rooms.length < 11; attempt++) {
    const w = ri(6, 12), h = ri(6, 11), x = ri(2, W - w - 3), y = ri(2, H - h - 3);
    if (rooms.some((r) => x < r.x + r.w + 3 && r.x < x + w + 3 && y < r.y + r.h + 3 && r.y < y + h + 3)) continue;
    rooms.push({ x, y, w, h });
  }
  for (const r of rooms) for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) carve(x, y);
  const center = (r) => [Math.floor(r.x + r.w / 2), Math.floor(r.y + r.h / 2)];

  // Corridors (2 wide, L-shaped): each room links to its nearest earlier room, plus a couple of loops.
  const corridor = (a, b) => {
    const [ax, ay] = center(a), [bx, by] = center(b);
    const horizontalFirst = rnd() < 0.5;
    const hLine = (y, x0, x1) => { for (let x = Math.min(x0, x1); x <= Math.max(x0, x1); x++) { carve(x, y); carve(x, y + 1); } };
    const vLine = (x, y0, y1) => { for (let y = Math.min(y0, y1); y <= Math.max(y0, y1); y++) { carve(x, y); carve(x + 1, y); } };
    if (horizontalFirst) { hLine(ay, ax, bx); vLine(bx, ay, by); } else { vLine(ax, ay, by); hLine(by, ax, bx); }
  };
  const d2 = (a, b) => { const [ax, ay] = center(a), [bx, by] = center(b); return (ax - bx) ** 2 + (ay - by) ** 2; };
  for (let i = 1; i < rooms.length; i++) {
    let best = 0;
    for (let j = 1; j < i; j++) if (d2(rooms[i], rooms[j]) < d2(rooms[i], rooms[best])) best = j;
    corridor(rooms[i], rooms[best]);
  }
  for (let k = 0; k < 2 && rooms.length > 3; k++) corridor(rooms[ri(0, rooms.length - 1)], rooms[ri(0, rooms.length - 1)]);

  // Start = the room nearest the west edge; goal = the room farthest away by walking distance.
  const start = rooms.reduce((a, b) => (b.x < a.x ? b : a));
  const dist = bfs(blocked, center(start));
  let goal = start;
  for (const r of rooms) { const [cx, cy] = center(r); if (dist[cy * W + cx] > dist[center(goal)[1] * W + center(goal)[0]]) goal = r; }

  const [sx, sy] = center(start), [gx, gy] = center(goal);
  const chests = [];
  const packs = [];
  for (const r of rooms) {
    if (r === start) continue;
    if (r !== goal && rnd() < 0.35) chests.push([r.x + 1.5, r.y + 1.5]);
    const n = Math.min(7, ri(3, 5) + Math.floor(depth / 2));
    packs.push({ x: r.x + r.w / 2, z: r.y + r.h / 2, n, room: r });
  }
  return {
    w: W, h: H, blocked, rooms,
    start: [sx + 0.5, sy + 1.5],
    exit: [sx + 0.5, sy - 0.5 >= start.y ? sy - 0.5 : sy + 0.5],
    stairs: depth < lastDepth ? [gx + 0.5, gy + 0.5] : null,
    boss: depth >= lastDepth ? [gx + 0.5, gy + 0.5] : null,
    chests, packs,
  };
}

/**
 * Natural caverns: cellular-automaton caves, keeping only the largest connected cave. "Rooms" are clearings
 * picked far apart from each other (for monster packs, chests and decorations); start, stairs and boss work
 * like the room generator (start in the west, the goal farthest away by walking distance).
 */
function generateCaves(seed, depth, lastDepth) {
  const rnd = mulberry32(seed);
  const ri = (a, b) => a + Math.floor(rnd() * (b - a + 1));
  let blocked = new Uint8Array(W * H);
  for (let y = 0; y < H; y++)
    for (let x = 0; x < W; x++)
      blocked[y * W + x] = x < 2 || y < 2 || x >= W - 2 || y >= H - 2 || rnd() < 0.45 ? 1 : 0;
  const walls = (b, x, y) => {
    let n = 0;
    for (let dy = -1; dy <= 1; dy++)
      for (let dx = -1; dx <= 1; dx++) {
        const nx = x + dx, ny = y + dy;
        if (nx < 0 || ny < 0 || nx >= W || ny >= H || b[ny * W + nx]) n++;
      }
    return n;
  };
  for (let it = 0; it < 5; it++) {
    const next = new Uint8Array(W * H);
    for (let y = 0; y < H; y++)
      for (let x = 0; x < W; x++)
        next[y * W + x] = x < 1 || y < 1 || x >= W - 1 || y >= H - 1 ? 1 : walls(blocked, x, y) >= 5 ? 1 : 0;
    blocked = next;
  }
  // Keep the largest cave.
  const label = new Int32Array(W * H).fill(-1);
  let best = -1, bestSize = 0;
  for (let i = 0; i < W * H; i++) {
    if (blocked[i] || label[i] >= 0) continue;
    const id = i, q = [i];
    label[i] = id;
    for (let h = 0; h < q.length; h++) {
      const c = q[h], x = c % W, y = (c / W) | 0;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const nx = x + dx, ny = y + dy, n = ny * W + nx;
        if (nx < 0 || ny < 0 || nx >= W || ny >= H || blocked[n] || label[n] >= 0) continue;
        label[n] = id;
        q.push(n);
      }
    }
    if (q.length > bestSize) { bestSize = q.length; best = id; }
  }
  for (let i = 0; i < W * H; i++) if (!blocked[i] && label[i] !== best) blocked[i] = 1;

  // Clearings: open cells with room around them, picked far apart (farthest-point sampling).
  const open = [];
  for (let y = 3; y < H - 3; y++)
    for (let x = 3; x < W - 3; x++) {
      let ok = true;
      for (let dy = -2; dy <= 2 && ok; dy++) for (let dx = -2; dx <= 2 && ok; dx++) if (blocked[(y + dy) * W + x + dx]) ok = false;
      if (ok) open.push([x, y]);
    }
  if (open.length === 0) return generate(seed, depth, lastDepth); // degenerate cave: fall back to rooms
  const clearings = [open[ri(0, open.length - 1)]];
  while (clearings.length < 11 && clearings.length < open.length) {
    let far = null, farD = -1;
    for (const p of open) {
      let d = Infinity;
      for (const c of clearings) d = Math.min(d, (p[0] - c[0]) ** 2 + (p[1] - c[1]) ** 2);
      if (d > farD) { farD = d; far = p; }
    }
    if (farD < 64) break;
    clearings.push(far);
  }
  const rooms = clearings.map(([x, y]) => ({ x: x - 3, y: y - 3, w: 7, h: 7 }));
  const centerOf = (r) => [r.x + 3, r.y + 3];
  const start = rooms.reduce((a, b) => (b.x < a.x ? b : a));
  const dist = bfs(blocked, centerOf(start));
  let goal = start;
  for (const r of rooms) { const [cx, cy] = centerOf(r); const [gx, gy] = centerOf(goal); if (dist[cy * W + cx] > dist[gy * W + gx]) goal = r; }
  const [sx, sy] = centerOf(start), [gx, gy] = centerOf(goal);
  const chests = [], packs = [];
  for (const r of rooms) {
    if (r === start) continue;
    const [cx, cy] = centerOf(r);
    if (r !== goal && rnd() < 0.35) chests.push([cx + 1.5, cy + 1.5]);
    packs.push({ x: cx + 0.5, z: cy + 0.5, n: Math.min(7, ri(3, 5) + Math.floor(depth / 2)), room: r });
  }
  return {
    w: W, h: H, blocked, rooms,
    start: [sx + 0.5, sy + 1.5], exit: [sx + 0.5, sy - 0.5],
    stairs: depth < lastDepth ? [gx + 0.5, gy + 0.5] : null,
    boss: depth >= lastDepth ? [gx + 0.5, gy + 0.5] : null,
    chests, packs,
  };
}

function bfs(blocked, [sx, sy]) {
  const d = new Int32Array(W * H).fill(-1);
  const q = [sy * W + sx];
  d[q[0]] = 0;
  for (let h = 0; h < q.length; h++) {
    const c = q[h], x = c % W, y = (c / W) | 0;
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
      const nx = x + dx, ny = y + dy, n = ny * W + nx;
      if (nx < 0 || ny < 0 || nx >= W || ny >= H || blocked[n] || d[n] >= 0) continue;
      d[n] = d[c] + 1;
      q.push(n);
    }
  }
  return d;
}

/** Packs the walkability bitmap like the world map (bit set = blocked, LSB first, row-major). */
function pack(blocked) {
  const bytes = Buffer.alloc(Math.ceil(blocked.length / 8));
  for (let i = 0; i < blocked.length; i++) if (blocked[i]) bytes[i >> 3] |= 1 << (i & 7);
  return bytes.toString("base64");
}

module.exports = { generate, generateCaves, pack, W, H };
