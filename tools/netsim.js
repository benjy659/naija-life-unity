#!/usr/bin/env node
// Maths check for the M5 netcode core (Assets/Scripts/Network/Core) while no
// Unity Editor is available. A line-by-line JS port of MoveCodec, SendPolicy,
// SnapshotBuffer and CellInterest runs the same scenarios as
// Assets/Tests/EditMode/Network/NetCoreTests.cs. Run: node tools/netsim.js
// If you change the C# maths, change this port too.

const L = {
  DriveSendInterval: 0.25, WalkSendInterval: 0.5, KeepAliveInterval: 5,
  StillPos: 0.05, StillHeading: 1, WalkerDelay: 0.6, DriverDelay: 0.3, Slide: 0.3,
  MaxExtrap: 0.25, MaxFoot: 9, MaxDrive: 45, Snap: 30, BadToHide: 5, BadWindow: 10,
  Cell: 200, CellLeave: 3,
};
const F = { Moving: 1, Driving: 2, Running: 4, Indoors: 8 };
const walk = (x, z, h = 0) => ({ x, y: 0, z, h, speed: 1.4, flags: F.Moving, veh: 0, anim: 0 });
const drive = (x, z) => ({ x, y: 0, z, h: 0, speed: 12, flags: F.Moving | F.Driving, veh: 3, anim: 0 });
const still = (x, z) => ({ x, y: 0, z, h: 0, speed: 0, flags: 0, veh: 0, anim: 0 });
const f32 = v => Math.fround(v);

// ---- MoveCodec ----
function encode(p, seq) {
  const b = Buffer.alloc(20);
  b[0] = 1; b[1] = (p.flags & 15) | ((p.anim & 15) << 4);
  b.writeUInt16LE(seq, 2); b.writeFloatLE(p.x, 4); b.writeFloatLE(p.z, 8);
  b.writeInt16LE(Math.max(-32768, Math.min(32767, Math.round(p.y * 100))), 12);
  let d = p.h % 360; if (d < 0) d += 360;
  b.writeUInt16LE(Math.round(d / 360 * 65536) & 0xffff, 14);
  b.writeUInt16LE(Math.max(0, Math.min(65535, Math.round(p.speed * 100))), 16);
  b.writeUInt16LE(p.veh, 18);
  return b;
}
function decode(b) {
  if (b.length !== 20 || b[0] !== 1) return null;
  const x = b.readFloatLE(4), z = b.readFloatLE(8);
  if (!isFinite(x) || !isFinite(z)) return null;
  return { seq: b.readUInt16LE(2), p: { x, z, y: b.readInt16LE(12) / 100, h: b.readUInt16LE(14) * 360 / 65536,
    speed: b.readUInt16LE(16) / 100, flags: b[1] & 15, anim: b[1] >> 4, veh: b.readUInt16LE(18) } };
}
const isNewer = (s, last) => (((s - last) << 16) >> 16) > 0;

// ---- SendPolicy ----
const deltaAngle = (a, b) => { let d = (b - a) % 360; if (d > 180) d -= 360; if (d <= -180) d += 360; return d; };
const changed = (a, b) => {
  const dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
  return dx * dx + dy * dy + dz * dz >= L.StillPos * L.StillPos || Math.abs(deltaAngle(a.h, b.h)) >= L.StillHeading;
};
class SendPolicy {
  constructor() { this.has = false; this.paused = false; this.last = 0; this.pose = null; }
  setPaused(p) { if (this.paused && !p) this.has = false; this.paused = p; }
  tryConsume(p, now) {
    if (this.paused) return false;
    const mask = F.Moving | F.Driving | F.Indoors;
    const mode = this.has && ((p.flags & mask) !== (this.pose.flags & mask) || p.veh !== this.pose.veh);
    if (!this.has || mode) return this.mark(p, now);
    const el = now - this.last;
    if (el >= L.KeepAliveInterval) return this.mark(p, now);
    const iv = p.flags & F.Driving ? L.DriveSendInterval : L.WalkSendInterval;
    if (el >= iv && changed(p, this.pose)) return this.mark(p, now);
    return false;
  }
  mark(p, now) { this.has = true; this.last = now; this.pose = p; return true; }
}

// ---- SnapshotBuffer ----
const lerp = (a, b, t) => {
  t = Math.max(0, Math.min(1, t));
  const p = { ...(t < 0.5 ? a : b) };
  p.x = a.x + (b.x - a.x) * t; p.y = a.y + (b.y - a.y) * t; p.z = a.z + (b.z - a.z) * t;
  p.speed = a.speed + (b.speed - a.speed) * t;
  let h = (a.h + deltaAngle(a.h, b.h) * t) % 360; if (h < 0) h += 360; p.h = h;
  return p;
};
class SnapshotBuffer {
  constructor() { this.s = []; this.bad = []; this.hasSeq = false; this.seq = 0; this.hidden = false; this.delay = -1; this.lastSample = -1; this.lastRecv = -Infinity; }
  add(p, seq, time) {
    if (this.hasSeq && !isNewer(seq, this.seq)) return false;
    if (![p.x, p.y, p.z].every(isFinite)) return false;
    this.hasSeq = true; this.seq = seq; this.lastRecv = time;
    if (this.s.length) {
      const last = this.s[this.s.length - 1];
      const dist = Math.hypot(last.p.x - p.x, last.p.y - p.y, last.p.z - p.z);
      if (dist > L.Snap) { this.s = [{ p, t: time }]; this.hidden = false; return true; }
      const driving = (last.p.flags | p.flags) & F.Driving;
      const minDt = (driving ? L.DriveSendInterval : L.WalkSendInterval) * 0.8;
      const dt = Math.max(time - last.t, minDt);
      if (dist / dt > (driving ? L.MaxDrive : L.MaxFoot)) {
        this.bad.push(time);
        while (this.bad.length && time - this.bad[0] > L.BadWindow) this.bad.shift();
        if (this.bad.length > L.BadToHide) this.hidden = true;
        return false;
      }
      if (time <= last.t) time = last.t + 1e-3;
    }
    if (this.s.length === 8) this.s.shift();
    this.s.push({ p, t: time }); this.hidden = false; return true;
  }
  sample(now) {
    if (!this.s.length || this.hidden || now - this.lastRecv > 15) return null;
    const newest = this.s[this.s.length - 1];
    const target = newest.p.flags & F.Driving ? L.DriverDelay : L.WalkerDelay;
    if (this.delay < 0) this.delay = target;
    else {
      const step = Math.max(0, now - this.lastSample) * L.Slide;
      this.delay = this.delay < target ? Math.min(target, this.delay + step) : Math.max(target, this.delay - step);
    }
    this.lastSample = now;
    const rt = now - this.delay;
    if (rt <= this.s[0].t) return this.s[0].p;
    for (let i = 0; i < this.s.length - 1; i++) {
      const a = this.s[i], b = this.s[i + 1];
      if (rt >= a.t && rt < b.t) return lerp(a.p, b.p, (rt - a.t) / (b.t - a.t));
    }
    const p = { ...newest.p };
    if (this.s.length >= 2 && newest.p.flags & F.Moving) {
      const prev = this.s[this.s.length - 2], span = newest.t - prev.t;
      if (span > 1e-4) {
        const k = Math.min(rt - newest.t, L.MaxExtrap) / span;
        p.x += (newest.p.x - prev.p.x) * k; p.y += (newest.p.y - prev.p.y) * k; p.z += (newest.p.z - prev.p.z) * k;
      }
    }
    return p;
  }
}

// ---- CellInterest ----
class CellInterest {
  constructor() { this.c = new Map(); this.center = null; }
  update(x, z, now) {
    const cx = Math.floor(x / L.Cell), cz = Math.floor(z / L.Cell);
    if (!this.center || this.center[0] !== cx || this.center[1] !== cz) {
      for (const [k, v] of this.c) {
        const [kx, kz] = k.split(':').map(Number);
        if (v === Infinity && !(Math.abs(kx - cx) <= 1 && Math.abs(kz - cz) <= 1)) this.c.set(k, now + L.CellLeave);
      }
      for (let dx = -1; dx <= 1; dx++) for (let dz = -1; dz <= 1; dz++) this.c.set(`${cx + dx}:${cz + dz}`, Infinity);
      this.center = [cx, cz];
    }
    for (const [k, v] of [...this.c]) if (v <= now) this.c.delete(k);
  }
}

// ---- checks ----
let fails = 0, passes = 0;
const ok = (cond, name) => { if (cond) passes++; else { fails++; console.log('FAIL', name); } };
const near = (a, b, eps) => Math.abs(a - b) <= eps;

{ const p = { x: -1062.37, y: 12.34, z: -822.5, h: 271.3, speed: 6.45, flags: F.Moving | F.Running, anim: 9, veh: 0 };
  const d = decode(encode(p, 65535));
  ok(d && d.seq === 65535 && near(d.p.x, f32(p.x), 1e-4) && near(d.p.y, p.y, 0.006) && near(d.p.h, p.h, 0.01)
     && near(d.p.speed, p.speed, 0.006) && d.p.flags === p.flags && d.p.anim === 9, 'codec round trip');
  const bad = encode(walk(1, 2), 1); bad[0] = 2; ok(decode(bad) === null, 'codec rejects version');
  ok(decode(encode({ ...walk(0, 0), x: NaN }, 1)) === null, 'codec rejects NaN');
  ok(isNewer(0, 65535) && isNewer(5, 2) && !isNewer(2, 5) && !isNewer(7, 7), 'sequence wrap'); }

const count = (fn, secs, fps = 60) => { const sp = new SendPolicy(); let n = 0;
  for (let i = 0; i <= secs * fps; i++) { const t = i / fps; if (sp.tryConsume(fn(t), t)) n++; } return n; };
{ const w = count(t => walk(t * 1.4, 0), 10); ok(w >= 19 && w <= 22, `walking ~2 Hz (${w} in 10 s)`);
  const d = count(t => drive(t * 12, 0), 10); ok(d >= 39 && d <= 42, `driving ~4 Hz (${d} in 10 s)`);
  ok(count(() => still(5, 5), 20) === 5, 'standing keep-alive every 5 s');
  ok(count(() => walk(5, 5), 20) === 5, 'moving flag without change = standing');
  const sp = new SendPolicy();
  ok(sp.tryConsume(walk(0, 0), 0) && !sp.tryConsume(walk(0.1, 0), 0.1) && sp.tryConsume(still(0.15, 0), 0.15)
     && sp.tryConsume(walk(0.16, 0), 0.2) && sp.tryConsume(drive(0.2, 0), 0.25), 'start/stop/vehicle at once');
  const sp2 = new SendPolicy(); sp2.tryConsume(walk(0, 0), 0); sp2.setPaused(true);
  ok(!sp2.tryConsume(walk(5, 0), 10), 'paused sends nothing'); sp2.setPaused(false);
  ok(sp2.tryConsume(walk(5, 0), 10.01), 'resend after pause');
  // Bandwidth per player: 20 bytes payload at the measured rates.
  console.log(`  walking ${(w / 10).toFixed(1)} msg/s, driving ${(d / 10).toFixed(1)} msg/s, standing 0.2 msg/s`); }

{ const b = new SnapshotBuffer(); for (let i = 0; i <= 10; i++) b.add(walk(i * 0.7, 0), i + 1, i * 0.5);
  ok(near(b.sample(5).x, 4.4 * 1.4, 0.02), 'walker drawn 600 ms behind'); }
{ const b = new SnapshotBuffer(); b.add(walk(0, 0), 1, 0); b.sample(0.1); const d0 = b.delay;
  b.add(drive(1, 0), 2, 0.25); b.sample(0.6); const d1 = b.delay; b.sample(5); const d2 = b.delay;
  ok(near(d0, 0.6, 1e-9) && near(d1, 0.45, 1e-6) && near(d2, 0.3, 1e-9), 'delay slides 0.6 -> 0.3 at 0.3/s'); }
{ const b = new SnapshotBuffer(); b.add(walk(0, 0), 1, 0); let dropped = 0;
  for (let i = 0; i < 6; i++) if (!b.add(walk(20 + i, 0), 2 + i, 0.5 + i * 0.1)) dropped++;
  ok(dropped === 6 && b.hidden && b.sample(4) === null, 'impossible speed dropped, hidden after 6');
  ok(b.add(walk(0.5, 0), 20, 4) && !b.hidden, 'next good snapshot unhides'); }
{ const b = new SnapshotBuffer(); b.add(walk(0, 0), 1, 0); ok(b.add(walk(3.25, 0), 2, 0.001), 'bunched sprint packet kept'); }
{ const b = new SnapshotBuffer(); b.add(walk(0, 0), 1, 0); b.add(walk(0.7, 0), 2, 0.5);
  ok(b.add(drive(100, 0), 3, 1.0) && near(b.sample(1.1).x, 100, 1e-3), 'gap over 30 m snaps'); }
{ const b = new SnapshotBuffer(); b.add(walk(0, 0), 1, 0); b.add(walk(0.7, 0), 2, 0.5);
  ok(near(b.sample(3).x, 0.7 + 1.4 * 0.25, 0.01), 'extrapolate at most 250 ms'); }
{ const b = new SnapshotBuffer(); b.add(walk(0, 0), 10, 0); ok(!b.add(walk(0.1, 0), 9, 0.5) && !b.hidden, 'old sequence ignored'); }
ok(near(lerp(walk(0, 0, 350), walk(0, 0, 10), 0.5).h % 360, 0, 0.01), 'heading short arc');

{ const ci = new CellInterest(); ci.update(100, 100, 0); const a = ci.c.size; ci.update(250, 100, 1); const b = ci.c.size;
  ci.update(250, 100, 3.9); const c = ci.c.size; ci.update(250, 100, 4.0); const d = ci.c.size;
  ok(a === 9 && b === 12 && c === 12 && d === 9, `cells 9 -> 12 -> 9 after 3 s (${a},${b},${c},${d})`);
  const ci2 = new CellInterest(); ci2.update(199, 100, 0); ci2.update(201, 100, 0.5); ci2.update(199, 100, 1); ci2.update(201, 100, 1.5);
  ok(ci2.c.size === 12, 'border back-and-forth does not churn'); }

// Smoothness: a walker at 1.4 m/s with ±80 ms network jitter, sampled at 60 fps.
{ const b = new SnapshotBuffer(); let seed = 7; const rnd = () => (seed = (seed * 16807) % 2147483647) / 2147483647;
  const sp = new SendPolicy(); const arrivals = []; let seq = 0;
  for (let i = 0; i <= 20 * 60; i++) { const t = i / 60, p = walk(t * 1.4, 0); if (sp.tryConsume(p, t)) arrivals.push({ p, seq: ++seq, at: t + 0.05 + rnd() * 0.08 }); }
  arrivals.sort((a, b) => a.at - b.at);
  let k = 0, maxErr = 0, maxStep = 0, prev = null;
  for (let i = 0; i <= 20 * 60; i++) { const now = i / 60;
    while (k < arrivals.length && arrivals[k].at <= now) { b.add(arrivals[k].p, arrivals[k].seq, arrivals[k].at); k++; }
    const s = b.sample(now); if (!s || now < 2) { prev = s; continue; }
    maxErr = Math.max(maxErr, Math.abs(s.x - (now - 0.6) * 1.4));
    if (prev) maxStep = Math.max(maxStep, Math.abs(s.x - prev.x)); prev = s; }
  console.log(`  jitter run: max frame step ${(maxStep * 100).toFixed(1)} cm (ideal ${(1.4 / 60 * 100).toFixed(1)}), max distance behind the 600 ms target ${(maxErr * 100).toFixed(0)} cm (network delay included)`);
  ok(maxStep < 0.06, 'smooth under ±80 ms jitter (no frame jumps over 6 cm)'); }

console.log(`${passes} passed, ${fails} failed`);
process.exit(fails ? 1 : 0);
