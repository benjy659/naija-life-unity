// tools/oshodi/oshodi-build-list.js
// Turns docs/specs/oshodi-greybox-layout.json into a flat list of greybox pieces
// that Unity reads with JsonUtility: Assets/Data/Oshodi/oshodi-greybox-build.json.
// Called by tools/oshodi/oshodi-layout.js after its checks pass; can also be run
// on its own: node tools/oshodi/oshodi-build-list.js
//
// Every piece: id (unique GameObject name), scene ("core" | "approach"), group
// (hierarchy path under the generated root), shape (box | cyl | sphere | blocker),
// centre x,y,z, size sx,sy,sz (metres, full size), rotation rx,ry,rz (degrees),
// mat (palette key), layer, collider (none | box | primitive | meshConvex; primitive = the shape's own collider, capsule for cyl), isStatic.

'use strict';
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..', '..');
const IN = path.join(ROOT, 'docs/specs/oshodi-greybox-layout.json');
const OUT = path.join(ROOT, 'Assets/Data/Oshodi/oshodi-greybox-build.json');
// Items with a "merge" key are combined by the Unity builder into one mesh per
// key (one draw call); "night" pieces only show when TimeOfDay says it is night.

const r3 = (v) => Math.round(v * 1000) / 1000;
const DEG = 180 / Math.PI;

function make(L) {
  const items = [], markers = [], ids = new Set();
  const add = (o) => {
    if (ids.has(o.id)) throw new Error('duplicate id ' + o.id);
    ids.add(o.id);
    items.push({
      id: o.id, scene: o.scene || 'core', group: o.group || '', shape: o.shape || 'box',
      x: r3(o.x), y: r3(o.y), z: r3(o.z), sx: r3(o.sx), sy: r3(o.sy), sz: r3(o.sz),
      rx: r3(o.rx || 0), ry: r3(o.ry || 0), rz: r3(o.rz || 0),
      mat: o.mat || 'concrete', layer: o.layer || 'Default', collider: o.collider || 'box', isStatic: o.isStatic !== false,
      merge: o.merge || '', night: !!o.night,
    });
  };
  const mark = (id, x, y, z, ry, group, scene = 'core') => {
    if (ids.has(id)) throw new Error('duplicate id ' + id);
    ids.add(id); markers.push({ id, scene, group, x: r3(x), y: r3(y), z: r3(z), ry: r3(ry || 0) });
  };
  // slab from a rect: top at yTop, thickness t
  const slab = (id, r, yTop, t, mat, layer, group, extra = {}) => add({ id, group, x: (r.x0 + r.x1) / 2, y: yTop - t / 2, z: (r.z0 + r.z1) / 2, sx: r.x1 - r.x0, sy: t, sz: r.z1 - r.z0, mat, layer, ...extra });
  const T = L.tile;
  const PAVE = L.levels.pavement;

  // ---- base ground (laterite) and tile blockers
  slab('ground-base', { x0: T.x0 - 10, z0: T.z0 - 10, x1: T.x1 + 10, z1: T.z1 + 10 }, -0.02, 0.5, 'laterite', 'Ground', 'Ground');
  const bh = 4, bt = 1;
  add({ id: 'blocker-north', group: 'Blockers', shape: 'blocker', x: 0, y: bh / 2, z: T.z1 + 10 + bt / 2, sx: T.x1 - T.x0 + 22, sy: bh, sz: bt, layer: 'PlayerBlocker' });
  add({ id: 'blocker-east', group: 'Blockers', shape: 'blocker', x: T.x1 + 10 + bt / 2, y: bh / 2, z: 0, sx: bt, sy: bh, sz: T.z1 - T.z0 + 22, layer: 'PlayerBlocker' });
  add({ id: 'blocker-west', group: 'Blockers', shape: 'blocker', x: T.x0 - 10 - bt / 2, y: bh / 2, z: 0, sx: bt, sy: bh, sz: T.z1 - T.z0 + 22, layer: 'PlayerBlocker' });
  // south edge is open to Approach_South along ROAD-A only: blockers either side of the road corridor
  add({ id: 'blocker-south-w', group: 'Blockers', shape: 'blocker', x: (T.x0 - 11 + -11.2) / 2, y: bh / 2, z: T.z0 - 10 - bt / 2, sx: -11.2 - (T.x0 - 11), sy: bh, sz: bt, layer: 'PlayerBlocker' });
  add({ id: 'blocker-south-e', group: 'Blockers', shape: 'blocker', x: (T.x1 + 11 + 11.2) / 2, y: bh / 2, z: T.z0 - 10 - bt / 2, sx: T.x1 + 11 - 11.2, sy: bh, sz: bt, layer: 'PlayerBlocker' });

  // ---- roads (asphalt top y 0.0), extended 10 m past the tile so the edge is not a cliff
  const ext = (r) => ({ x0: r.x0 <= T.x0 ? r.x0 - 10 : r.x0, x1: r.x1 >= T.x1 ? r.x1 + 10 : r.x1, z0: r.z0 <= T.z0 ? r.z0 - 10 : r.z0, z1: r.z1 >= T.z1 ? r.z1 + 10 : r.z1 });
  for (const l of L.lanes) slab(`road-${l.id}`, ext(l.r), 0.0, 0.2, 'asphalt', 'Ground', 'Roads');
  for (const m of L.medians) slab(m.id, ext(m.r), PAVE, 0.32, 'median', 'Ground', 'Roads');
  // lane dashes (ROAD-A lane divider each side, ROAD-B centre line), 3 m paint / 3 m gap
  const A = L.roads[0];
  const half = A.median.width / 2;
  for (const side of [-1, 1]) {
    const x = side * (half + A.laneWidth);
    for (let z = T.z0 + 1.5, k = 0; z < T.z1; z += 6, k++) {
      if (z > -84 && z < -56) continue;   // junction and its crossings
      add({ id: `dash-A-${side < 0 ? 'W' : 'E'}-${k}`, group: 'Roads/Markings', x, y: 0.005, z, sx: 0.15, sy: 0.01, sz: 3, mat: 'paint_white', layer: 'Ground', collider: 'none' });
    }
  }
  for (let x = T.x0 + 1.5, k = 0; x < T.x1; x += 6, k++) {
    if (x > -16 && x < 16) continue;
    add({ id: `dash-B-${k}`, group: 'Roads/Markings', x, y: 0.005, z: L.roads[1].centre, sx: 3, sy: 0.01, sz: 0.15, mat: 'paint_white', layer: 'Ground', collider: 'none' });
  }
  // zebra stripes (0.5 m paint, 0.5 m gap, across the walking direction)
  const zebra = (id, r, group, alongX) => {
    const len = alongX ? r.x1 - r.x0 : r.z1 - r.z0;
    for (let s = 0.25, k = 0; s < len; s += 1.0, k++) {
      if (alongX) add({ id: `${id}-${k}`, group, x: r.x0 + s, y: 0.006, z: (r.z0 + r.z1) / 2, sx: 0.5, sy: 0.01, sz: r.z1 - r.z0, mat: 'paint_white', layer: 'Ground', collider: 'none' });
      else add({ id: `${id}-${k}`, group, x: (r.x0 + r.x1) / 2, y: 0.006, z: r.z0 + s, sx: r.x1 - r.x0, sy: 0.01, sz: 0.5, mat: 'paint_white', layer: 'Ground', collider: 'none' });
    }
  };
  for (const c of L.crossings) zebra(c.id, c.r, 'Roads/Crossings', c.id.includes('-A-'));
  // driveways: level hatched aprons at road height across the pavement
  for (const d of L.driveways) slab(d.id, d.r, 0.0, 0.2, 'driveway', 'Ground', 'Roads/Driveways');

  // ---- kerbs and pavements (top y 0.12). Pavement zones are cut where a driveway crosses.
  const cut = (r, holes) => {
    let parts = [r];
    for (const h of holes) {
      const next = [];
      for (const p of parts) {
        if (!(p.x0 < h.x1 && p.x1 > h.x0 && p.z0 < h.z1 && p.z1 > h.z0)) { next.push(p); continue; }
        if (p.z1 - p.z0 >= p.x1 - p.x0) {   // long in z: cut in z
          if (h.z0 > p.z0) next.push({ ...p, z1: h.z0 });
          if (h.z1 < p.z1) next.push({ ...p, z0: h.z1 });
        } else {
          if (h.x0 > p.x0) next.push({ ...p, x1: h.x0 });
          if (h.x1 < p.x1) next.push({ ...p, x0: h.x1 });
        }
      }
      parts = next;
    }
    return parts;
  };
  const holes = L.driveways.map((d) => d.r);
  for (const p of L.pavements) {
    for (const zone of ['gutter', 'furnishing', 'clear']) {
      cut(ext(p[zone]), holes).forEach((r, i) => slab(`pave-${p.id}-${zone}-${i}`, r, PAVE, 0.32, zone === 'gutter' ? 'gutter_slab' : 'pavement', 'Ground', 'Pavements'));
    }
  }
  for (const c of L.corners) slab(c.id, c.r, PAVE, 0.32, 'pavement', 'Ground', 'Pavements');
  // gutter slab joints every 1 m along the long side (thin dark lines)
  // (visual only; kept cheap by skipping in M2.1: the gutter material carries the joint pattern later)

  // ---- open areas
  for (const a of L.areas) {
    if (a.id.startsWith('under-bridge')) continue;   // market floor below
    slab(`area-${a.id}`, a.r, PAVE, 0.32, a.id === 'yard-mechanic' ? 'laterite' : 'plaza', 'Ground', 'Areas');
  }
  for (const a of L.areas.filter((q) => q.id.startsWith('under-bridge'))) slab(`area-${a.id}`, a.r, PAVE, 0.32, 'plaza_dark', 'Ground', 'Areas');

  // ---- buildings
  const front = (b) => b.front;
  for (const b of L.buildings) {
    const g = `Buildings/${b.id}`;
    const mat = b.kind === 'terminal' ? 'terminal_concrete' : b.kind === 'filler' ? 'filler' : 'shophouse';
    add({ id: b.id, group: g, x: (b.r.x0 + b.r.x1) / 2, y: b.h / 2, z: (b.r.z0 + b.r.z1) / 2, sx: b.r.x1 - b.r.x0, sy: b.h, sz: b.r.z1 - b.r.z0, mat, layer: 'Building' });
    const f = front(b);
    if (!f) continue;
    const fx = f === 'east' ? b.r.x1 : f === 'west' ? b.r.x0 : null;
    const out = f === 'east' ? 1 : -1;
    const zc = (b.r.z0 + b.r.z1) / 2, zl = b.r.z1 - b.r.z0;
    if (b.kind === 'terminal') {
      // curved glass front: 7 segments bowed out, deepest 1.2 m at the middle
      const n = 7, bow = 1.2;
      for (let i = 0; i < n; i++) {
        const t = (i + 0.5) / n - 0.5, za = zc + t * zl, segL = zl / n + 0.05;
        const off = bow * (1 - 4 * t * t);
        const slope = -8 * bow * t / zl;   // d(off)/dz
        add({ id: `${b.id}-glass-${i}`, group: g, x: fx + out * (off / 2 + 0.05), y: (b.h - 1) / 2 + 0.5, z: za, sx: Math.max(0.3, off + 0.1), sy: b.h - 1, sz: segL, ry: out * Math.atan(slope) * DEG, mat: 'glass', layer: 'Building', collider: 'box' });
      }
      add({ id: `${b.id}-sign`, group: g, x: fx + out * 1.35, y: b.h - 0.6, z: zc, sx: 0.2, sy: 1.0, sz: Math.min(18, zl * 0.6), mat: 'sign_green', layer: 'CameraIgnore', collider: 'none' });
    } else if (b.kind === 'shophouse') {
      add({ id: `${b.id}-shopfront`, group: g, x: fx + out * 0.03, y: 1.6, z: zc, sx: 0.08, sy: 3.0, sz: zl - 2.0, mat: 'shopfront_dark', layer: 'Building', collider: 'none' });
      add({ id: `${b.id}-signboard`, group: g, x: fx + out * 0.12, y: 3.6, z: zc, sx: 0.12, sy: 0.9, sz: zl - 3.0, mat: `sign_${['red', 'blue', 'yellow', 'green'][Math.abs(Math.round(zc)) % 4]}`, layer: 'CameraIgnore', collider: 'none' });
    }
  }

  // ---- flyover
  const F = L.flyover;
  const fd = { ...F.deck, x0: F.deck.x0 - 40, x1: F.deck.x1 + 40 };
  add({ id: 'flyover-deck', group: 'Flyover', x: 0, y: (F.undersideY + F.deckTopY) / 2, z: (fd.z0 + fd.z1) / 2, sx: fd.x1 - fd.x0, sy: F.deckTopY - F.undersideY, sz: fd.z1 - fd.z0, mat: 'concrete', layer: 'Building' });
  for (const [k, z] of [['s', fd.z0 + 0.15], ['n', fd.z1 - 0.15]]) add({ id: `flyover-parapet-${k}`, group: 'Flyover', x: 0, y: F.deckTopY + F.parapetHeight / 2, z, sx: fd.x1 - fd.x0, sy: F.parapetHeight, sz: 0.3, mat: 'concrete', layer: 'Building' });
  add({ id: 'flyover-top-blocker', group: 'Flyover', shape: 'blocker', x: 0, y: F.deckTopY + 1.5, z: (fd.z0 + fd.z1) / 2, sx: fd.x1 - fd.x0, sy: 3, sz: fd.z1 - fd.z0 - 0.6, layer: 'PlayerBlocker' });
  for (const p of F.piers) {
    add({ id: p.id, group: 'Flyover/Piers', shape: 'cyl', x: p.x, y: p.height / 2, z: p.z, sx: p.diameter, sy: p.height, sz: p.diameter, mat: 'concrete', layer: 'Building', collider: 'primitive' });
    for (let i = 0; i < p.posters; i++) {
      const ang = i === 0 ? -90 : 90;   // toward the road side faces (+/-x) and the other way
      const dir = (p.x < 0 ? 1 : -1) * (i === 0 ? 1 : -1);
      add({ id: `${p.id}-poster-${i}`, group: 'Flyover/Posters', x: p.x + dir * (p.diameter / 2 + 0.02), y: 1.9, z: p.z, sx: 0.02, sy: 1.2, sz: 0.8, mat: `poster_${i % 2 ? 'a' : 'b'}`, layer: 'CameraIgnore', collider: 'none', ry: 0, rz: 0, rx: 0 });
      void ang;
    }
  }

  // ---- footbridge
  const FB = L.footbridge;
  const fbg = 'Footbridge';
  const zc = (FB.deck.z0 + FB.deck.z1) / 2, zw = FB.deck.z1 - FB.deck.z0;
  const deckT = FB.deckTopY - FB.undersideY;
  slab('fb-deck', FB.deck, FB.deckTopY, deckT, 'steel_green', 'Ground', fbg);
  const parapet = (id, x0, x1, y0, y1) => {   // a parapet pair along a run from (x0,y0) to (x1,y1)
    const dx = x1 - x0, dy = y1 - y0, len = Math.hypot(dx, dy), a = Math.atan2(dy, dx) * DEG;
    for (const [k, z] of [['s', FB.deck.z0 + 0.06], ['n', FB.deck.z1 - 0.06]]) {
      add({ id: `${id}-parapet-${k}`, group: `${fbg}/Parapets`, x: (x0 + x1) / 2, y: (y0 + y1) / 2 + FB.parapetHeight / 2, z, sx: len + 0.05, sy: FB.parapetHeight, sz: 0.12, rz: a, mat: 'steel_green', layer: 'Building' });
    }
  };
  parapet('fb-deck', FB.deck.x0, FB.deck.x1, FB.deckTopY, FB.deckTopY);
  for (const s of FB.stairs) {
    const west = s.id.endsWith('west'), out = west ? -1 : 1, sg = `${fbg}/${west ? 'West' : 'East'}`;
    slab(`${s.id}-top`, s.topLanding.r, s.topLanding.y, deckT, 'steel_green', 'Ground', sg);
    slab(`${s.id}-mid`, s.midLanding.r, s.midLanding.y, 0.3, 'concrete', 'Ground', sg);
    const tl = s.topLanding.r, ml = s.midLanding.r;
    parapet(`${s.id}-top`, tl.x0, tl.x1, s.topLanding.y, s.topLanding.y);
    parapet(`${s.id}-mid`, ml.x0, ml.x1, s.midLanding.y, s.midLanding.y);
    // outer end of the top landing and mid landing are open to the flights; close the far end of the mid landing? no: flights continue outward.
    for (const [fname, f, solid] of [['f1', s.flight1, false], ['f2', s.flight2, true]]) {
      const inner = west ? f.r.x1 : f.r.x0, outer = west ? f.r.x0 : f.r.x1;   // inner = high end (toward the deck)
      const n = f.risers, rise = (f.yTop - f.yBottom) / n, tread = Math.abs(outer - inner) / (n - 1);
      // visual steps (no collider): step k = 1..n-1 at height yBottom + k*rise, from the outer end inward
      for (let k = 1; k < n; k++) {
        const xa = outer - out * (k - 1) * tread, xb = outer - out * k * tread;   // walking inward
        const top = f.yBottom + k * rise;
        const bottom = solid ? 0 : top - 0.2;
        add({ id: `${s.id}-${fname}-step-${k}`, group: `${sg}/Steps`, x: (xa + xb) / 2, y: (top + bottom) / 2, z: zc, sx: Math.abs(xb - xa) + 0.01, sy: top - bottom, sz: zw, mat: solid ? 'concrete' : 'steel_green', layer: solid ? 'Building' : 'Ground', collider: solid ? 'box' : 'none' });
      }
      // smooth walk ramp (collider only) from the outer foot to the inner top, top surface on the nosing line
      // the line through every step nosing, started one tread further out so it meets the floor
      const x0 = outer + out * tread, y0 = f.yBottom, x1 = inner, y1 = f.yTop;
      const dx = x1 - x0, dy = y1 - y0, len = Math.hypot(dx, dy), a = Math.atan2(dy, dx) * DEG, t = 0.2;
      let nx = -Math.sin(a / DEG), ny = Math.cos(a / DEG);
      if (ny < 0) { nx = -nx; ny = -ny; }   // normal must point up
      add({ id: `${s.id}-${fname}-ramp`, group: `${sg}/Ramps`, shape: 'blocker', x: (x0 + x1) / 2 - nx * t / 2, y: (y0 + y1) / 2 - ny * t / 2, z: zc, sx: len + 0.3, sy: t, sz: zw, rz: a, layer: 'Ground' });
      parapet(`${s.id}-${fname}`, x0, x1, y0, y1);
    }
    // under the mid landing: closed block (flight 2's solid steps close the rest)
    const ml2 = s.midLanding.r;
    add({ id: `${s.id}-under-mid`, group: sg, x: (ml2.x0 + ml2.x1) / 2, y: (s.midLanding.y - 0.3) / 2, z: zc, sx: ml2.x1 - ml2.x0, sy: s.midLanding.y - 0.3, sz: zw, mat: 'concrete', layer: 'Building' });
  }
  for (const p of FB.piers) add({ id: p.id, group: `${fbg}/Piers`, x: p.x, y: p.height / 2, z: p.z, sx: p.size, sy: p.height, sz: p.size, mat: 'steel_green', layer: 'Building' });

  // ---- danfo park
  const D = L.danfoPark;
  slab('park-pad', D.pad, 0.0, 0.2, 'asphalt', 'Ground', 'DanfoPark');
  for (const i of D.islands) slab(i.id, i.r, i.y, 0.32, 'island', 'Ground', 'DanfoPark/Islands');
  for (const c of D.walkCrossings) zebra(c.id, c.r, 'DanfoPark/Crossings', false);
  {   // rail on the head-aisle edge with gaps
    let segs = [[D.fence.z0, D.fence.z1]];
    for (const [a, b] of D.fence.gaps) segs = segs.flatMap(([s0, s1]) => (a < s1 && b > s0 ? [[s0, a], [b, s1]].filter(([u, v]) => v - u > 0.3) : [[s0, s1]]));
    segs.forEach(([a, b], k) => add({ id: `park-rail-${k}`, group: 'DanfoPark/Rail', x: D.fence.x, y: PAVE + D.fence.height / 2, z: (a + b) / 2, sx: 0.08, sy: D.fence.height, sz: b - a, mat: 'rail_yellow', layer: 'Prop' }));
  }
  for (const d of D.danfos) {
    const s = d.size;
    add({ id: d.id, group: 'DanfoPark/Danfos', x: d.x, y: 0.35 + (s.H - 0.35) / 2, z: d.z, sx: s.W, sy: s.H - 0.35, sz: s.L, ry: d.ry, mat: 'danfo_yellow', layer: 'Prop' });
    add({ id: `${d.id}-stripe`, group: 'DanfoPark/Danfos', x: d.x, y: 1.1, z: d.z, sx: s.W + 0.02, sy: 0.12, sz: s.L + 0.02, ry: d.ry, mat: 'black', layer: 'Prop', collider: 'none' });
    add({ id: `${d.id}-windows`, group: 'DanfoPark/Danfos', x: d.x, y: 1.85, z: d.z, sx: s.W + 0.02, sy: 0.6, sz: s.L - 0.6, ry: d.ry, mat: 'glass_dark', layer: 'Prop', collider: 'none' });
  }
  for (const b of D.routeBoards) {
    add({ id: `${b.id}-pole`, group: 'DanfoPark/RouteBoards', shape: 'cyl', x: b.x, y: PAVE + b.height / 2, z: b.z, sx: 0.08, sy: b.height, sz: 0.08, mat: 'black', layer: 'Prop', collider: 'primitive' });
    add({ id: b.id, group: 'DanfoPark/RouteBoards', x: b.x, y: PAVE + b.height - 0.25, z: b.z, sx: 0.05, sy: 0.5, sz: 1.6, mat: 'rail_yellow', layer: 'CameraIgnore', collider: 'none' });
  }
  for (const s of D.conductorSlots) mark(s.id, s.x, PAVE, s.z, s.ry, 'Markers/JobSlots');
  for (const b of D.bays) {
    mark(`${b.id}-work`, b.workPoint.x, PAVE, b.workPoint.z, b.workPoint.ry, `Markers/Bays/${b.id}`);
    for (const a of b.asidePoints) mark(`${b.id}-${a.id}`, a.x, PAVE, a.z, 0, `Markers/Bays/${b.id}`);
    b.queue.forEach((q, k) => mark(`${b.id}-queue-${k + 1}`, q.x, PAVE, q.z, 270, `Markers/Bays/${b.id}`));
    mark(`${b.id}-door`, b.door.x, 0, b.door.z, 0, `Markers/Bays/${b.id}`);
  }

  // ---- okada park
  for (const b of L.okadaPark.bikes) add({ id: b.id, group: 'OkadaPark', x: b.x, y: PAVE + b.size.H / 2, z: b.z, sx: b.size.W, sy: b.size.H, sz: b.size.L, ry: b.ry, mat: 'okada_red', layer: 'Prop' });
  for (const s of L.okadaPark.riderSlots) mark(s.id, s.x, PAVE, s.z, s.ry, 'Markers/JobSlots');

  // ---- props
  const yBase = (p) => (p.y !== undefined ? p.y : PAVE);
  const wireTop = 8.6;
  for (const p of L.props) {
    const g = `Props/${p.type}`, y0 = yBase(p);
    switch (p.type) {
      case 'nepa_pole':
        add({ id: p.id, group: g, shape: 'cyl', x: p.x, y: y0 + 4.5, z: p.z, sx: 0.28, sy: 9, sz: 0.28, mat: 'wood', layer: 'Prop', collider: 'primitive' });
        add({ id: `${p.id}-arm`, group: g, x: p.x, y: y0 + 8.7, z: p.z, sx: 1.6, sy: 0.12, sz: 0.12, ry: Math.abs(p.x) < 20 ? 0 : 90, mat: 'wood', layer: 'CameraIgnore', collider: 'none' });
        break;
      case 'street_lamp': {
        const toRoad = Math.abs(p.x) < 20 ? -Math.sign(p.x) : -Math.sign(p.z - L.roads[1].centre);
        const alongX = Math.abs(p.x) < 20;
        add({ id: p.id, group: g, shape: 'cyl', x: p.x, y: y0 + 4.5, z: p.z, sx: 0.22, sy: 9, sz: 0.22, mat: 'steel_grey', layer: 'Prop', collider: 'primitive' });
        add({ id: `${p.id}-arm`, group: g, x: p.x + (alongX ? toRoad * 1.25 : 0), y: y0 + 8.9, z: p.z + (alongX ? 0 : toRoad * 1.25), sx: alongX ? 2.5 : 0.1, sy: 0.1, sz: alongX ? 0.1 : 2.5, mat: 'steel_grey', layer: 'CameraIgnore', collider: 'none' });
        add({ id: `${p.id}-head`, group: g, x: p.x + (alongX ? toRoad * 2.4 : 0), y: y0 + 8.8, z: p.z + (alongX ? 0 : toRoad * 2.4), sx: 0.6, sy: 0.15, sz: 0.3, mat: 'lamp_emissive', layer: 'CameraIgnore', collider: 'none' });
        break;
      }
      case 'soffit_lamp':
        add({ id: p.id, group: g, x: p.x, y: 5.85, z: p.z, sx: 0.6, sy: 0.15, sz: 0.6, mat: 'lamp_emissive', layer: 'CameraIgnore', collider: 'none' });
        break;
      case 'guard_rail':
        add({ id: p.id, group: g, x: 0, y: PAVE + 0.6, z: (p.from[1] + p.to[1]) / 2, sx: 0.08, sy: 1.2, sz: p.to[1] - p.from[1], mat: 'rail_yellow', layer: 'Prop' });
        break;
      case 'stall': {
        add({ id: p.id, group: g, x: p.x, y: y0 + 0.45, z: p.z, sx: 1.8, sy: 0.9, sz: 1.2, ry: p.ry, mat: 'wood', layer: 'Prop' });
        add({ id: `${p.id}-awning`, group: g, x: p.x, y: y0 + 2.25, z: p.z, sx: 2.0, sy: 0.08, sz: 2.0, ry: p.ry, mat: ['awning_blue', 'awning_red', 'awning_yellow'][Math.abs(Math.round(p.x * 7 + p.z)) % 3], layer: 'CameraIgnore', collider: 'none' });
        add({ id: `${p.id}-post`, group: g, shape: 'cyl', x: p.x, y: y0 + 1.1, z: p.z, sx: 0.06, sy: 2.2, sz: 0.06, mat: 'steel_grey', layer: 'Prop', collider: 'none' });
        break;
      }
      case 'umbrella':
        add({ id: p.id, group: g, shape: 'cyl', x: p.x, y: y0 + 1.15, z: p.z, sx: 0.05, sy: 2.3, sz: 0.05, mat: 'steel_grey', layer: 'Prop', collider: 'primitive' });
        add({ id: `${p.id}-canopy`, group: g, shape: 'cyl', x: p.x, y: y0 + 2.3, z: p.z, sx: 2.2, sy: 0.1, sz: 2.2, mat: ['awning_red', 'awning_blue', 'awning_yellow'][p.id.length % 3], layer: 'CameraIgnore', collider: 'none' });
        break;
      case 'bus_shelter':
        add({ id: `${p.id}-roof`, group: g, x: p.x, y: y0 + 2.6, z: p.z, sx: 1.8, sy: 0.08, sz: 4.0, mat: 'steel_blue', layer: 'CameraIgnore', collider: 'none' });
        add({ id: `${p.id}-back`, group: g, x: p.x - 0.75, y: y0 + 1.3, z: p.z, sx: 0.06, sy: 2.5, sz: 4.0, mat: 'steel_blue', layer: 'Prop' });
        add({ id: `${p.id}-bench`, group: g, x: p.x - 0.45, y: y0 + 0.45, z: p.z, sx: 0.45, sy: 0.08, sz: 3.0, mat: 'steel_grey', layer: 'Prop' });
        add({ id: `${p.id}-name`, group: g, x: p.x, y: y0 + 2.85, z: p.z, sx: 0.05, sy: 0.45, sz: 3.6, mat: 'sign_green', layer: 'CameraIgnore', collider: 'none' });
        break;
      case 'bus_stop_pole':
      case 'street_sign':
        add({ id: p.id, group: g, shape: 'cyl', x: p.x, y: y0 + 1.4, z: p.z, sx: 0.07, sy: 2.8, sz: 0.07, mat: 'steel_grey', layer: 'Prop', collider: 'primitive' });
        add({ id: `${p.id}-board`, group: g, x: p.x, y: y0 + 2.75, z: p.z, sx: 0.04, sy: 0.4, sz: 1.2, ry: p.type === 'street_sign' ? 0 : 90, mat: 'sign_green', layer: 'CameraIgnore', collider: 'none' });
        break;
      case 'generator':
        add({ id: p.id, group: g, x: p.x, y: y0 + 0.5, z: p.z, sx: p.w || 1.2, sy: 1.0, sz: p.d || 0.8, ry: p.ry, mat: 'generator', layer: 'Prop' });
        break;
      case 'pos_kiosk':
        add({ id: p.id, group: g, x: p.x, y: y0 + 1.25, z: p.z, sx: 2, sy: 2.5, sz: 2, mat: 'steel_blue', layer: 'Prop' });
        break;
      case 'car_shell':
        add({ id: p.id, group: g, x: p.x, y: y0 + 0.72, z: p.z, sx: 1.8, sy: 1.45, sz: 4.5, ry: p.ry, mat: 'rust', layer: 'Prop' });
        break;
      case 'tyre_stack':
        add({ id: p.id, group: g, shape: 'cyl', x: p.x, y: y0 + 0.5, z: p.z, sx: 0.8, sy: 1.0, sz: 0.8, mat: 'black', layer: 'Prop', collider: 'primitive' });
        break;
      case 'tree_almond':
        add({ id: p.id, group: g, shape: 'cyl', x: p.x, y: y0 + 2.0, z: p.z, sx: 0.4, sy: 4.0, sz: 0.4, mat: 'wood', layer: 'Prop', collider: 'primitive' });
        add({ id: `${p.id}-canopy`, group: g, shape: 'sphere', x: p.x, y: y0 + 5.2, z: p.z, sx: 5, sy: 3, sz: 5, mat: 'leaf', layer: 'CameraIgnore', collider: 'none' });
        break;
      case 'hoarding': {
        const r = (p.ry || 0) / DEG, fx = Math.sin(r), fz = Math.cos(r);
        add({ id: p.id, group: g, x: p.x, y: p.y + 2, z: p.z, sx: 8, sy: 4, sz: 0.3, ry: p.ry, mat: 'hoarding', layer: 'CameraIgnore', collider: 'none' });
        void fx; void fz;
        break;
      }
      case 'hawker_marker':
      case 'warden_marker':
        mark(p.id, p.x, PAVE, p.z, p.ry, 'Markers/JobSlots');
        break;
      default:
        throw new Error('no greybox shape for prop type ' + p.type);
    }
  }
  // NEPA wires: 3 strands per span, each as 4 straight pieces along a sag curve
  for (const w of L.wires) {
    const [ax, az] = w.from, [bx, bz] = w.to;
    const along = Math.abs(bz - az) >= Math.abs(bx - ax);
    for (let s = 0; s < 3; s++) {
      const off = (s - 1) * 0.5;
      const P = (t) => {
        const x = ax + (bx - ax) * t + (along ? off : 0), z = az + (bz - az) * t + (along ? 0 : off);
        const y = wireTop - s * 0.15 - (wireTop - w.sagY) * 4 * t * (1 - t);
        return [x, y, z];
      };
      for (let k = 0; k < 4; k++) {
        const [x0, y0, z0] = P(k / 4), [x1, y1, z1] = P((k + 1) / 4);
        const len = Math.hypot(x1 - x0, y1 - y0, z1 - z0), horiz = Math.hypot(x1 - x0, z1 - z0);
        const pitch = Math.atan2(y1 - y0, horiz) * DEG;
        const yaw = Math.atan2(x1 - x0, z1 - z0) * DEG;
        add({ id: `${w.id}-s${s}-${k}`, group: 'Props/wires', x: (x0 + x1) / 2, y: (y0 + y1) / 2, z: (z0 + z1) / 2, sx: 0.03, sy: 0.03, sz: len, rx: -pitch, ry: yaw, mat: 'black', layer: 'CameraIgnore', collider: 'none' });
      }
    }
  }

  // ---- skyline shell (spec 6.5): the builder merges each key into one mesh, no colliders
  const SK = L.skyline;
  if (SK) {
    for (const b of SK.blocks) add({ id: b.id, group: 'Skyline', x: b.x, y: b.h / 2, z: b.z, sx: b.w, sy: b.h, sz: b.d, ry: b.ry, mat: 'skyline', layer: SK.layer, collider: 'none', merge: SK.mergeShell });
    for (const w of SK.windows) add({ id: w.id, group: 'Skyline', x: w.x, y: w.y, z: w.z, sx: w.w, sy: w.h, sz: w.d, ry: w.ry, mat: 'window_lit', layer: SK.layer, collider: 'none', merge: SK.mergeWindows, night: true });
  }

  // ---- spawn and viewpoints
  mark('spawn', L.spawn.x, L.spawn.y, L.spawn.z, L.spawn.ry, 'Markers');
  for (const v of L.viewpoints) mark(v.id, v.x, v.y, v.z, v.ry, 'Markers/Viewpoints');

  // ---- approach road (scene "approach")
  const AP = L.approach, sc = 'approach';
  const addA = (o) => add({ ...o, scene: sc });
  const slabA = (id, r, yTop, t, mat, layer, group, extra = {}) => addA({ id, group, x: (r.x0 + r.x1) / 2, y: yTop - t / 2, z: (r.z0 + r.z1) / 2, sx: r.x1 - r.x0, sy: t, sz: r.z1 - r.z0, mat, layer, ...extra });
  const zTop = AP.from, zBot = AP.to;
  const RBz = AP.roundabout.centre[1], RBo = AP.roundabout.ringOuterRadius, RBi = AP.roundabout.islandRadius;
  slabA('app-ground', { x0: -60, z0: zBot - 5, x1: 60, z1: zTop - 10 }, -0.02, 0.5, 'laterite', 'Ground', 'Approach');
  const aw = { x0: -7.55, x1: -1.05 }, ae = { x0: 1.05, x1: 7.55 };
  const ly = AP.dropStop.layBy;
  const north = RBz + RBo, south = RBz - RBo;
  for (const [k, r] of [['w-n', { ...aw, z0: north - 2, z1: zTop - 10 }], ['e-n', { ...ae, z0: north - 2, z1: zTop - 10 }], ['stub', { x0: -7.55, x1: 7.55, z0: zBot, z1: south + 2 }]]) slabA(`app-road-${k}`, r, 0, 0.2, 'asphalt', 'Ground', 'Approach/Roads');
  slabA('app-median', { x0: -1.05, x1: 1.05, z0: north + 3, z1: zTop - 10 }, PAVE, 0.32, 'median', 'Ground', 'Approach/Roads');
  addA({ id: 'app-median-rail', group: 'Approach/Roads', x: 0, y: PAVE + 0.6, z: (north + 3 + zTop - 10) / 2, sx: 0.08, sy: 1.2, sz: zTop - 10 - north - 3, mat: 'rail_yellow', layer: 'Prop' });
  slabA('app-layby', ly, 0, 0.2, 'asphalt', 'Ground', 'Approach/DropStop');
  // pavements: east straight; west steps out around the lay-by
  slabA('app-pave-e', { x0: 7.55, x1: 11.2, z0: north + 1, z1: zTop - 10 }, PAVE, 0.32, 'pavement', 'Ground', 'Approach/Pavements');
  slabA('app-pave-w-n', { x0: -11.2, x1: -7.55, z0: ly.z1, z1: zTop - 10 }, PAVE, 0.32, 'pavement', 'Ground', 'Approach/Pavements');
  slabA('app-pave-w-s', { x0: -11.2, x1: -7.55, z0: north + 1, z1: ly.z0 }, PAVE, 0.32, 'pavement', 'Ground', 'Approach/Pavements');
  slabA('app-pave-w-layby', { x0: ly.x0 - 3.65, x1: ly.x0, z0: ly.z0, z1: ly.z1 }, PAVE, 0.32, 'pavement', 'Ground', 'Approach/Pavements');
  addA({ id: 'app-roundabout-ring', group: 'Approach/Roundabout', shape: 'cyl', x: 0, y: -0.1, z: RBz, sx: RBo * 2, sy: 0.2, sz: RBo * 2, mat: 'asphalt', layer: 'Ground', collider: 'box' });
  addA({ id: 'app-roundabout-island', group: 'Approach/Roundabout', shape: 'cyl', x: 0, y: 0.15, z: RBz, sx: RBi * 2, sy: 0.3, sz: RBi * 2, mat: 'island', layer: 'Building', collider: 'meshConvex' });
  const sh = AP.dropStop.shelter;
  addA({ id: 'app-shelter-roof', group: 'Approach/DropStop', x: sh.x, y: PAVE + 2.6, z: sh.z, sx: 1.8, sy: 0.08, sz: 4, mat: 'steel_blue', layer: 'CameraIgnore', collider: 'none' });
  addA({ id: 'app-shelter-back', group: 'Approach/DropStop', x: sh.x - 0.75, y: PAVE + 1.3, z: sh.z, sx: 0.06, sy: 2.5, sz: 4, mat: 'steel_blue', layer: 'Prop' });
  slabA('app-shelter-pad', { x0: sh.x - 1.2, x1: ly.x0 - 3.6, z0: sh.z - 2.5, z1: sh.z + 2.5 }, PAVE, 0.32, 'pavement', 'Ground', 'Approach/DropStop');
  addA({ id: 'app-end-barrier', group: 'Approach', x: 0, y: 0.6, z: zBot + 1, sx: 30, sy: 1.2, sz: 0.6, mat: 'rail_yellow', layer: 'Building' });
  addA({ id: 'app-blocker-end', group: 'Approach', shape: 'blocker', x: 0, y: 2, z: zBot - 1, sx: 120, sy: 4, sz: 1, layer: 'PlayerBlocker' });
  addA({ id: 'app-blocker-w', group: 'Approach', shape: 'blocker', x: -60, y: 2, z: (zBot + zTop) / 2, sx: 1, sy: 4, sz: zTop - zBot, layer: 'PlayerBlocker' });
  addA({ id: 'app-blocker-e', group: 'Approach', shape: 'blocker', x: 60, y: 2, z: (zBot + zTop) / 2, sx: 1, sy: 4, sz: zTop - zBot, layer: 'PlayerBlocker' });
  for (const [k, p] of [['stop', AP.dropStop.stopPoint], ['alight', AP.dropStop.alightPoint]]) markers.push({ id: `drop-${k}`, scene: sc, group: 'Markers/DropStop', x: p.x, y: k === 'stop' ? 0 : PAVE, z: p.z, ry: p.ry || 0 });
  markers.push({ id: 'roundabout-centre', scene: sc, group: 'Markers', x: 0, y: 0, z: RBz, ry: 0 });
  markers.push({ id: 'streaming-test-start', scene: sc, group: 'Markers', x: AP.streamingTestStart.x, y: PAVE, z: AP.streamingTestStart.z, ry: 0 });

  // ---- checks the Unity tests run
  const laneLines = [];
  const A2 = L.roads[0];
  for (const side of [-1, 1]) for (const k of [0.5, 1.5]) {
    const x = side * (half + k * A2.laneWidth);
    laneLines.push({ id: `A-${side < 0 ? 'W' : 'E'}-lane${k + 0.5}`, x0: x, z0: T.z0 + 1, x1: x, z1: T.z1 - 1, y: 0 });
  }
  for (const k of [-0.5, 0.5]) laneLines.push({ id: `B-lane${k < 0 ? 'S' : 'N'}`, x0: T.x0 + 1, z0: L.roads[1].centre + k * A2.laneWidth, x1: T.x1 - 1, z1: L.roads[1].centre + k * A2.laneWidth, y: 0 });
  const clearLines = L.pavements.map((p) => {
    const c = p.clear, alongZ = c.z1 - c.z0 > c.x1 - c.x0;
    return alongZ ? { id: p.id, x0: (c.x0 + c.x1) / 2, z0: c.z0 + 0.5, x1: (c.x0 + c.x1) / 2, z1: c.z1 - 0.5, y: PAVE } : { id: p.id, x0: c.x0 + 0.5, z0: (c.z0 + c.z1) / 2, x1: c.x1 - 0.5, z1: (c.z0 + c.z1) / 2, y: PAVE };
  });
  const ws = FB.stairs[0].footClear, es = FB.stairs[1].footClear;
  const footbridgeWalk = { id: 'footbridge', x0: ws.x0 + 1, z0: zc, x1: es.x1 - 1, z1: zc, y: FB.deckTopY };

  return {
    version: 1, generatedBy: 'tools/oshodi/oshodi-build-list.js from docs/specs/oshodi-greybox-layout.json',
    note: 'Do not edit by hand. Change tools/oshodi/oshodi-layout.js and run it.',
    materials: MATERIALS, items, markers,
    checks: { overheadMin: 5.5, stepMax: 0.3, laneLines, clearLines, footbridgeWalk },
  };
}

// Greybox palette (URP Lit). e = emission strength (0 = none).
const MATERIALS = [
  ['laterite', 0.62, 0.38, 0.24], ['asphalt', 0.17, 0.17, 0.18], ['median', 0.85, 0.85, 0.82], ['paint_white', 0.92, 0.92, 0.9],
  ['driveway', 0.32, 0.32, 0.32], ['pavement', 0.72, 0.71, 0.68], ['gutter_slab', 0.55, 0.55, 0.53], ['plaza', 0.78, 0.75, 0.68],
  ['plaza_dark', 0.48, 0.46, 0.43], ['island', 0.66, 0.66, 0.64], ['terminal_concrete', 0.8, 0.8, 0.78], ['glass', 0.35, 0.55, 0.65],
  ['glass_dark', 0.08, 0.1, 0.12], ['shophouse', 0.76, 0.56, 0.42], ['filler', 0.6, 0.57, 0.52], ['shopfront_dark', 0.1, 0.1, 0.1],
  ['concrete', 0.62, 0.62, 0.6], ['steel_green', 0.16, 0.42, 0.24], ['steel_grey', 0.45, 0.46, 0.48], ['steel_blue', 0.12, 0.33, 0.62],
  ['rail_yellow', 0.95, 0.76, 0.0], ['danfo_yellow', 0.98, 0.78, 0.0], ['okada_red', 0.75, 0.12, 0.12], ['black', 0.05, 0.05, 0.05],
  ['wood', 0.36, 0.24, 0.13], ['leaf', 0.2, 0.48, 0.2], ['rust', 0.45, 0.25, 0.15], ['generator', 0.25, 0.3, 0.28],
  ['awning_red', 0.82, 0.22, 0.16], ['awning_blue', 0.16, 0.36, 0.72], ['awning_yellow', 0.95, 0.8, 0.2],
  ['sign_red', 0.78, 0.16, 0.14], ['sign_blue', 0.12, 0.3, 0.66], ['sign_yellow', 0.95, 0.82, 0.15], ['sign_green', 0.05, 0.5, 0.22],
  ['poster_a', 0.9, 0.85, 0.2], ['poster_b', 0.2, 0.6, 0.85], ['hoarding', 0.9, 0.3, 0.55],
  ['lamp_emissive', 1.0, 0.85, 0.55, 2.5], ['skyline', 0.46, 0.49, 0.55], ['window_lit', 1.0, 0.86, 0.6, 1.6],
].map(([key, r, g, b, e]) => ({ key, r, g, b, emission: e || 0 }));

function write() {
  const L = JSON.parse(fs.readFileSync(IN, 'utf8'));
  const out = make(L);
  fs.mkdirSync(path.dirname(OUT), { recursive: true });
  fs.writeFileSync(OUT, JSON.stringify(out) + '\n');
  const core = out.items.filter((i) => i.scene === 'core').length, app = out.items.length - core;
  console.log(`  build list: ${out.items.length} pieces (core ${core}, approach ${app}), ${out.markers.length} markers -> ${path.relative(ROOT, OUT)}`);
  return out;
}
module.exports = { make, write };
if (require.main === module) write();
