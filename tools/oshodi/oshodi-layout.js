// tools/oshodi/oshodi-layout.js
// Source of truth for the M2 Oshodi greybox layout (docs/specs/oshodi-greybox.md).
//
// Run:  node tools/oshodi/oshodi-layout.js
// It (1) builds the layout from the numbers below, (2) runs the placement rules
// (nothing in a lane, clear pavements, walkable footbridge, aisles wide enough),
// (3) writes docs/specs/oshodi-greybox-layout.json (the Unity builder reads this)
// and docs/specs/oshodi-greybox-plan.svg (top-down plan). Exits 1 if a rule fails.
//
// Change positions HERE, never by hand in the JSON or only in the Unity scene.
// Units: metres. Axes: X east, Z north, Y up (Unity). Origin: centre of the
// Agege Motor Road / footbridge area. Ground: road top y 0.00, pavement and
// plazas y 0.12.

'use strict';
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..', '..');
const OUT_JSON = path.join(ROOT, 'docs/specs/oshodi-greybox-layout.json');
const OUT_SVG = path.join(ROOT, 'docs/specs/oshodi-greybox-plan.svg');

// ---------------------------------------------------------------- constants
const Y_ROAD = 0.0;
const Y_PAVE = 0.12;            // kerb height 0.12
const LANE = 3.25;
const KERB = 0.15;
const PAVE = { gutter: 0.6, furnishing: 0.9, clear: 2.0 };   // 3.5 m commercial pavement
const PAVE_W = PAVE.gutter + PAVE.furnishing + PAVE.clear;
const OVERHEAD_MIN = 5.5;       // anything over a lane must have its underside at or above this
const LANE_CLEAR = 0.75;        // props stay this far from a lane edge
const AISLE_MIN = 2.4;          // market aisles
const HEADROOM = 2.1;           // below this, space under a stair is closed off
const TILE = { x0: -120, z0: -150, x1: 120, z1: 150 };

const r2 = (v) => Math.round(v * 100) / 100;
const rect = (x0, z0, x1, z1) => ({ x0: r2(Math.min(x0, x1)), z0: r2(Math.min(z0, z1)), x1: r2(Math.max(x0, x1)), z1: r2(Math.max(z0, z1)) });
const overlap = (a, b, m = 0) => a.x0 < b.x1 + m && a.x1 > b.x0 - m && a.z0 < b.z1 + m && a.z1 > b.z0 - m;
const inside = (a, b) => a.x0 >= b.x0 - 1e-6 && a.x1 <= b.x1 + 1e-6 && a.z0 >= b.z0 - 1e-6 && a.z1 <= b.z1 + 1e-6;
const ptIn = (x, z, b) => x >= b.x0 && x <= b.x1 && z >= b.z0 && z <= b.z1;

// ---------------------------------------------------------------- roads
// ROAD-A: Agege Motor Road, runs north-south along x = 0. 2 lanes each way,
// 2.1 m raised median with a pedestrian guard rail (forces people onto the
// footbridge, like the real thing). Right-hand traffic: northbound on the east.
const MEDIAN = 2.1;
const A = (() => {
  const m = MEDIAN / 2, cw = 2 * LANE;
  const eCw = [m, m + cw], wCw = [-(m + cw), -m];
  const kerbE = [eCw[1], eCw[1] + KERB], kerbW = [wCw[0] - KERB, wCw[0]];
  const paveE = [kerbE[1], kerbE[1] + PAVE_W], paveW = [kerbW[0] - PAVE_W, kerbW[0]];
  return { m, eCw, wCw, kerbE, kerbW, paveE, paveW };
})();
// ROAD-B: side street, runs east-west along z = -70. 1 lane each way, no median.
// OSM name to be confirmed by Ted's team (lesson L-14: check names against the real thing).
const B_Z = -70;
const B = (() => {
  const cw = [B_Z - LANE, B_Z + LANE];
  const kerbS = [cw[0] - KERB, cw[0]], kerbN = [cw[1], cw[1] + KERB];
  const paveS = [kerbS[0] - PAVE_W, kerbS[0]], paveN = [kerbN[1], kerbN[1] + PAVE_W];
  return { cw, kerbS, kerbN, paveS, paveN };
})();

const BLD_LINE = A.paveE[1];     // 11.2: building/plaza line both sides of ROAD-A

const roads = [
  {
    id: 'ROAD-A', name: 'Agege Motor Road', axis: 'z', centre: 0, from: TILE.z0, to: TILE.z1,
    lanesEachWay: 2, laneWidth: LANE, median: { width: MEDIAN, kerbHeight: 0.12, guardRail: true },
    kerb: { width: KERB, height: 0.12 }, pavement: { ...PAVE, width: PAVE_W, order: 'kerb | gutter slabs | furnishing (poles, lamps, signs) | clear zone | building line' },
  },
  {
    id: 'ROAD-B', name: 'Side street (OSM name to confirm)', axis: 'x', centre: B_Z, from: TILE.x0, to: TILE.x1,
    lanesEachWay: 1, laneWidth: LANE, median: null,
    kerb: { width: KERB, height: 0.12 }, pavement: { ...PAVE, width: PAVE_W, order: 'kerb | gutter slabs | furnishing | clear zone | building line' },
  },
];

// Carriageway rects (where vehicles drive). Nothing may stand in these except
// overhead structures whose underside is >= 5.5 m.
const junction = rect(A.wCw[0], B.cw[0], A.eCw[1], B.cw[1]);
const lanes = [
  { id: 'A-west-sb', road: 'ROAD-A', dir: 'southbound', r: rect(A.wCw[0], TILE.z0, A.wCw[1], TILE.z1) },
  { id: 'A-east-nb', road: 'ROAD-A', dir: 'northbound', r: rect(A.eCw[0], TILE.z0, A.eCw[1], TILE.z1) },
  { id: 'B-south-wb', road: 'ROAD-B', dir: 'westbound', r: rect(TILE.x0, B.cw[0], TILE.x1, B_Z) },
  { id: 'B-north-eb', road: 'ROAD-B', dir: 'eastbound', r: rect(TILE.x0, B_Z, TILE.x1, B.cw[1]) },
  { id: 'junction', road: 'ROAD-A x ROAD-B', dir: 'all', r: junction },
];
// Median of ROAD-A, cut at the junction and its crossings.
const CROSS_N = rect(A.wCw[0], -62.5, A.eCw[1], -59.5);   // zebra across ROAD-A north of the junction
const CROSS_S = rect(A.wCw[0], -80.5, A.eCw[1], -77.5);   // zebra across ROAD-A south of the junction
const CROSS_W = rect(-14.7, B.cw[0], -11.7, B.cw[1]);       // zebra across ROAD-B west arm
const CROSS_E = rect(11.7, B.cw[0], 14.7, B.cw[1]);         // zebra across ROAD-B east arm
const medians = [
  { id: 'median-north', r: rect(-A.m, CROSS_N.z1, A.m, TILE.z1) },
  { id: 'median-south', r: rect(-A.m, TILE.z0, A.m, CROSS_S.z0) },
];
const crossings = [
  { id: 'zebra-A-north', r: CROSS_N, width: 3.0 },
  { id: 'zebra-A-south', r: CROSS_S, width: 3.0 },
  { id: 'zebra-B-west', r: CROSS_W, width: 3.0 },
  { id: 'zebra-B-east', r: CROSS_E, width: 3.0 },
];

// Pavements, split into zones. ROAD-A pavements are cut where ROAD-B crosses; ROAD-B
// pavements are cut where ROAD-A crosses. Corner squares count as pavement (clear).
function zonesAlongZ(side, z0, z1) {   // side -1 west, +1 east
  const k = side > 0 ? A.kerbE[1] : A.kerbW[0];
  const g1 = k + side * PAVE.gutter, f1 = g1 + side * PAVE.furnishing, c1 = f1 + side * PAVE.clear;
  return {
    gutter: rect(k, z0, g1, z1), furnishing: rect(g1, z0, f1, z1), clear: rect(f1, z0, c1, z1),
  };
}
function zonesAlongX(side, x0, x1) {   // side -1 south, +1 north
  const k = side > 0 ? B.kerbN[1] : B.kerbS[0];
  const g1 = k + side * PAVE.gutter, f1 = g1 + side * PAVE.furnishing, c1 = f1 + side * PAVE.clear;
  return {
    gutter: rect(x0, k, x1, g1), furnishing: rect(x0, g1, x1, f1), clear: rect(x0, f1, x1, c1),
  };
}
const pavements = [];
for (const side of [-1, 1]) {
  const s = side < 0 ? 'west' : 'east';
  pavements.push({ id: `A-${s}-north`, road: 'ROAD-A', ...zonesAlongZ(side, B.kerbN[1], TILE.z1) });
  pavements.push({ id: `A-${s}-south`, road: 'ROAD-A', ...zonesAlongZ(side, TILE.z0, B.kerbS[0]) });
}
for (const side of [-1, 1]) {
  const s = side < 0 ? 'south' : 'north';
  pavements.push({ id: `B-${s}-west`, road: 'ROAD-B', ...zonesAlongX(side, TILE.x0, A.kerbW[0]) });
  pavements.push({ id: `B-${s}-east`, road: 'ROAD-B', ...zonesAlongX(side, A.kerbE[1], TILE.x1) });
}
const corners = [
  { id: 'corner-NW', r: rect(A.paveW[0], B.kerbN[1], A.kerbW[0], B.paveN[1]) },
  { id: 'corner-NE', r: rect(A.kerbE[1], B.kerbN[1], A.paveE[1], B.paveN[1]) },
  { id: 'corner-SW', r: rect(A.paveW[0], B.paveS[0], A.kerbW[0], B.kerbS[0]) },
  { id: 'corner-SE', r: rect(A.kerbE[1], B.paveS[0], A.paveE[1], B.kerbS[0]) },
];
const clearZones = pavements.map((p) => ({ id: p.id + '-clear', r: p.clear }));

// Driveways: dropped kerbs where vehicles cross a pavement. Pedestrians walk across them level.
const driveways = [
  { id: 'drive-danfo-exit', note: 'danfo park head aisle -> ROAD-A southbound', r: rect(-14, -52, A.wCw[0], -45) },
  { id: 'drive-danfo-entry', note: 'ROAD-B eastbound -> danfo park tail aisle', r: rect(-48, B.cw[1], -41, -62.5) },
  { id: 'drive-okada', note: 'okada park <-> ROAD-A northbound', r: rect(A.eCw[1], -1, 13, 5.5) },
];

// ---------------------------------------------------------------- overhead structures
// Oshodi-Apapa Expressway flyover, east-west across the whole tile. Not reachable
// by the player in M2 (no ramps in this tile). Market lives underneath it.
const FLY = { z0: 34, z1: 56, underside: 6.0, top: 7.2, parapet: 1.0 };
const flyover = {
  id: 'flyover-oshodi-apapa', name: 'Oshodi-Apapa Expressway (elevated)', deck: rect(TILE.x0, FLY.z0, TILE.x1, FLY.z1),
  undersideY: FLY.underside, deckTopY: FLY.top, parapetHeight: FLY.parapet, lanesEachWay: 3,
  reachable: false, note: 'Backdrop deck in M2. Deck runs past both tile edges into the skyline. Ramps and driving on it come with M3+.',
  piers: [],
};
for (const zx of [38, 52]) for (const xx of [14, 40, 66, 92, 118]) for (const sg of [-1, 1]) {
  flyover.piers.push({ id: `fly-pier-${sg < 0 ? 'W' : 'E'}${xx}-${zx}`, x: sg * xx, z: zx, diameter: 1.2, height: FLY.underside, posters: xx === 14 ? 2 : 0 });
}

// Footbridge over ROAD-A at z = -30, Terminal 3 plaza (west) to Terminal 2 forecourt (east).
// Deck top 6.12 (plaza 0.12 + 6.0), 36 risers of 1/6 m in two flights of 18, tread 0.28.
const FB = { z: -30, width: 3.0, deckTop: 6.12, deckThick: 0.4, span: 16, topLanding: 2.0, midLanding: 2.0, risersPerFlight: 18, riser: 6.0 / 36, tread: 0.28, parapet: 1.1 };
const flightRun = (FB.risersPerFlight - 1) * FB.tread;   // 17 treads = 4.76 m
function stair(side) {   // side -1 west, +1 east; stairs continue outward along x
  const s = side;
  const deckEnd = s * FB.span;
  const topL = [deckEnd, deckEnd + s * FB.topLanding];
  const f1 = [topL[1], topL[1] + s * flightRun];
  const midL = [f1[1], f1[1] + s * FB.midLanding];
  const f2 = [midL[1], midL[1] + s * flightRun];
  const zr = [FB.z - FB.width / 2, FB.z + FB.width / 2];
  const midY = FB.deckTop - FB.risersPerFlight * FB.riser;
  // Headroom under flight 2 drops below 2.1 m somewhere along it; close off flight 2 + mid landing underneath.
  const closed = rect(midL[0], zr[0], f2[1], zr[1]);
  const foot = rect(f2[1], zr[0] - 0.5, f2[1] + s * 3.0, zr[1] + 0.5);   // keep 3 m clear landing at the foot
  return {
    id: `footbridge-stair-${s < 0 ? 'west' : 'east'}`,
    topLanding: { r: rect(topL[0], zr[0], topL[1], zr[1]), y: r2(FB.deckTop) },
    flight1: { r: rect(f1[0], zr[0], f1[1], zr[1]), yTop: r2(FB.deckTop), yBottom: r2(midY), risers: FB.risersPerFlight },
    midLanding: { r: rect(midL[0], zr[0], midL[1], zr[1]), y: r2(midY) },
    flight2: { r: rect(f2[0], zr[0], f2[1], zr[1]), yTop: r2(midY), yBottom: Y_PAVE, risers: FB.risersPerFlight },
    closedUnder: closed, footClear: foot,
    walkCollider: 'one smooth sloped BoxCollider per flight (ramp) on layer Ground, steps are visual only (stops CharacterController jitter)',
    footprint: rect(topL[0], zr[0], f2[1], zr[1]),
  };
}
const footbridge = {
  id: 'footbridge-T3-T2', name: 'Oshodi footbridge (Terminal 3 to Terminal 2)',
  deck: rect(-FB.span, FB.z - FB.width / 2, FB.span, FB.z + FB.width / 2),
  deckTopY: r2(FB.deckTop), undersideY: r2(FB.deckTop - FB.deckThick), width: FB.width, parapetHeight: FB.parapet,
  riser: r2(FB.riser * 1000) / 1000, tread: FB.tread,
  stairs: [stair(-1), stair(1)],
  piers: [-17, 17].flatMap((x) => [FB.z - 1.2, FB.z + 1.2].map((z) => ({ id: `fb-pier-${x}-${z}`, x, z, size: 0.4, height: r2(FB.deckTop - FB.deckThick), posters: 1 }))),
  note: 'Single span over ROAD-A and both pavements: no pier on a pavement or the median.',
};

// ---------------------------------------------------------------- buildings (greybox massing)
// rect x0,z0,x1,z1, height h. Terminals 1-3 have a curved glass front (front = the side facing ROAD-A).
const buildings = [];
const addB = (id, kind, r, h, extra = {}) => buildings.push({ id, kind, r, h, ...extra });
addB('T1', 'terminal', rect(-52, 4, -14, 28), 12, { name: 'Oshodi Transport Interchange Terminal 1', front: 'east', frontStyle: 'curved glass, 3 bays' });
addB('T2', 'terminal', rect(34, -44, 94, -4), 16, { name: 'Oshodi Transport Interchange Terminal 2', front: 'west', frontStyle: 'curved glass, largest of the three' });
addB('T3', 'terminal', rect(-92, -62.5, -52, -24), 10, { name: 'Oshodi Transport Interchange Terminal 3', front: 'east', frontStyle: 'curved glass, faces the danfo park' });
// Shophouses: 2-4 storeys, ground-floor shopfront, signboard. Setback 1.0 m from the 11.2 building line.
const SH = BLD_LINE + 1.0;   // 12.2
const heights = [7, 10, 7, 13, 10, 7, 10];
const chunksN = [[60, 72], [72.5, 84.5], [85, 97], [97.5, 109.5], [110, 122], [122.5, 134.5], [135, 147]];
const chunksS = [[-92, -80], [-104.5, -92.5], [-117, -105], [-129.5, -117.5], [-142, -130]];
chunksN.forEach(([a, b], i) => { addB(`shop-NW-${i + 1}`, 'shophouse', rect(-24, a, -SH, b), heights[i], { front: 'east' }); addB(`shop-NE-${i + 1}`, 'shophouse', rect(SH, a, 24, b), heights[(i + 3) % 7], { front: 'west' }); });
chunksS.forEach(([a, b], i) => { addB(`shop-SW-${i + 1}`, 'shophouse', rect(-24, a, -SH, b), heights[(i + 1) % 7], { front: 'east' }); addB(`shop-SE-${i + 1}`, 'shophouse', rect(SH, a, 24, b), heights[(i + 4) % 7], { front: 'west' }); });
addB('shop-W-mid-1', 'shophouse', rect(-24, -22, -SH, -10), 7, { front: 'east' });
addB('shop-W-mid-2', 'shophouse', rect(-24, -9.5, -SH, 2), 10, { front: 'east' });
addB('shop-E-mid-1', 'shophouse', rect(24, 0, 36, 12), 7, { front: 'west' });
addB('shop-E-mid-2', 'shophouse', rect(24, 12.5, 36, 24.5), 10, { front: 'west' });
addB('shop-SE-T2', 'shophouse', rect(SH, -62, 24, -50), 10, { front: 'west' });
// Back rows and far filler (simple boxes, one shared material, they frame the street)
[[60, 84, 7], [86, 110, 10], [112, 146, 13]].forEach(([a, b, h], i) => { addB(`back-NW-${i + 1}`, 'filler', rect(-48, a, -28, b), h); addB(`back-NE-${i + 1}`, 'filler', rect(28, a, 48, b), h); });
addB('filler-W-1', 'filler', rect(-80, 4, -56, 28), 10);
addB('filler-W-2', 'filler', rect(-118, -62, -96, -40), 7);
addB('filler-E-1', 'filler', rect(98, -62, 118, -4), 10);
[[-80, -104], [-108, -146]].forEach(([a, b], i) => { addB(`back-SW-${i + 1}`, 'filler', rect(-48, b, -28, a), 7); addB(`back-SE-${i + 1}`, 'filler', rect(28, b, 48, a), 10); });

// ---------------------------------------------------------------- open areas (y 0.12, walkable)
const areas = [
  { id: 'plaza-T3', name: 'Terminal 3 plaza (footbridge west foot)', r: rect(-52, -34.25, BLD_LINE * -1, -24) },
  { id: 'walk-T3', name: 'Terminal 3 walkway (to the danfo islands)', r: rect(-52, -62.5, -48, -34.25) },
  { id: 'walk-park-head', name: 'Park head walkway (pavement side of the head aisle)', r: rect(-14, -62.5, -BLD_LINE, -34.25) },
  { id: 'forecourt-T2', name: 'Terminal 2 forecourt (footbridge east foot)', r: rect(BLD_LINE, -44, 34, -4) },
  { id: 'plaza-east', name: 'East plaza (okada park, POS kiosk)', r: rect(BLD_LINE, -4, 24, 30) },
  { id: 'yard-mechanic', name: 'Mechanic yard', r: rect(-52, -22, -26, 2) },
  { id: 'under-bridge-W', name: 'Under-bridge market west', r: rect(-70, FLY.z0, -BLD_LINE, FLY.z1) },
  { id: 'under-bridge-E', name: 'Under-bridge market east', r: rect(BLD_LINE, FLY.z0, 70, FLY.z1) },
];

// ---------------------------------------------------------------- danfo park (west of ROAD-A, beside Terminal 3)
// Danfos queue 3-deep in 5 loading lanes, noses east toward the exit. Raised islands
// (0.12) between lanes; conductors stand on the island on each lane's door side
// (right-hand side, south of the lane, because the danfos face east).
// Islands are 2.0 m wide so a conductor, a queue of 6 and a passing player fit (M4 ask, jobs thread).
const ISL_W = 2.0, LOAD_W = 3.25, PARK_Z0 = -62.5;
const islandZ = [], laneZ = [];
{ let z = PARK_Z0; for (let i = 0; i < 6; i++) { islandZ.push([r2(z), r2(z + ISL_W)]); z += ISL_W; if (i < 5) { laneZ.push([r2(z), r2(z + LOAD_W)]); z += LOAD_W; } } }
const PARK_Z1 = islandZ[5][1];   // -34.25
const PARK = { pad: rect(-48, PARK_Z0, -14, PARK_Z1), tailAisle: rect(-48, PARK_Z0, -41, PARK_Z1), headAisle: rect(-21.5, PARK_Z0, -14, PARK_Z1) };
const DANFO = { L: 5.9, W: 2.0, H: 2.6 };
const routes = ['UNILAG GATE', 'IKEJA ALONG', 'MILE 2', 'OBALENDE', 'IKOTUN'];
const danfoPark = {
  id: 'danfo-park-T3', pad: PARK.pad, tailAisle: PARK.tailAisle, headAisle: PARK.headAisle,
  islands: islandZ.map(([a, b], i) => ({ id: `island-${i}`, r: rect(-41, a, -21.5, b), y: Y_PAVE })),
  loadingLanes: laneZ.map(([a, b], i) => ({ id: `load-lane-${i + 1}`, r: rect(-41, a, -21.5, b), route: routes[i] })),
  danfos: [], conductorSlots: [], routeBoards: [],
  fence: { id: 'park-rail', x: -14.0, z0: PARK_Z0, z1: PARK_Z1, height: 1.1, gaps: [[-52, -45], ...islandZ.slice(0, 5).map(([a, b]) => [a, b])], note: 'rail on the head-aisle edge; open at the exit driveway and at each island crossing' },
  walkCrossings: islandZ.flatMap(([a, b], i) => [
    { id: `park-zebra-tail-${i}`, r: rect(-48, a, -41, b), note: 'painted walk across the tail aisle from the Terminal 3 walkway to the island' },
    { id: `park-zebra-head-${i}`, r: rect(-21.5, a, -14, b), note: 'painted walk across the head aisle from the pavement side to the island' }]),
  bays: [],
};
laneZ.forEach(([a, b], i) => {
  const zc = (a + b) / 2;
  [-24.75, -31.25, -37.75].forEach((x, k) => danfoPark.danfos.push({ id: `danfo-${i + 1}-${k + 1}`, x, z: r2(zc), ry: 90, size: DANFO, parked: true }));
  const isl = islandZ[i];   // island south of the lane = door side (danfos face east, sliding door on the right)
  const iz = r2((isl[0] + isl[1]) / 2), doorZ = r2(isl[1] - 0.6);   // stand 0.6 m in from the island edge at the door
  const DOOR_X = -24.0;   // sliding door centre on the head danfo (front at -21.8, door 1.6-2.6 m back)
  danfoPark.conductorSlots.push({ id: `slot-conductor-${i + 1}`, x: DOOR_X, z: doorZ, ry: 0, lane: `load-lane-${i + 1}`, note: 'M4 job slot marker = WorkPoint (bot conductor stands here)' });
  danfoPark.routeBoards.push({ id: `route-board-${i + 1}`, x: -22.0, z: iz, ry: 90, text: `OSHODI - ${routes[i]}`, height: 2.6, readFrom: 'the island tip and the head aisle' });
  danfoPark.bays.push({
    id: `bay-${i + 1}`, lane: `load-lane-${i + 1}`, island: `island-${i}`, route: routes[i], m4: i < 3,
    danfo: `danfo-${i + 1}-1`, doorSide: 'south (right-hand side, facing the island)', door: { x: DOOR_X, z: r2(laneZ[i][0]) },
    workPoint: { x: DOOR_X, z: doorZ, ry: 0, note: 'conductor at the open sliding door, facing it' },
    asidePoints: [
      { id: 'aside-west', x: r2(DOOR_X - 2.5), z: iz, note: 'use when the player comes from the head aisle (east)' },
      { id: 'aside-east', x: r2(DOOR_X + 2.3), z: iz, note: 'use when the player comes from the tail aisle (west)' }],
    asideRule: 'pick the aside point farther from where the player stepped onto the island, so the bot never stands between the player and the door',
    queue: [0, 1, 2, 3, 4, 5].map((k) => ({ x: r2(DOOR_X - 3.6 - k * 0.8), z: iz })),
    queueNote: '6 waiting passengers in a line along the island, 0.8 m apart, behind the west aside point',
  });
});

// ---------------------------------------------------------------- okada park (east plaza)
const okadaPark = { id: 'okada-park', bikes: [], riderSlots: [] };
[0, 1.4, 2.8, 4.2].forEach((z, i) => {
  okadaPark.bikes.push({ id: `okada-${i + 1}`, x: 14.0, z, ry: 45, size: { L: 2.0, W: 0.7, H: 1.1 } });
  okadaPark.riderSlots.push({ id: `slot-okada-${i + 1}`, x: 15.8, z, ry: 270, note: 'M4 job slot marker' });
});

// ---------------------------------------------------------------- props
// type registry: footprint w (x) by d (z) before rotation, height, collider, layer.
const TYPES = {
  nepa_pole: { w: 0.3, d: 0.3, h: 9.0, collider: 'capsule', layer: 'Prop', instanced: true },
  street_lamp: { w: 0.3, d: 0.3, h: 9.0, collider: 'capsule', layer: 'Prop', instanced: true, armOverRoad: 2.5, light: true },
  soffit_lamp: { w: 0.6, d: 0.6, h: 0.3, mountY: 5.7, collider: 'none', layer: 'CameraIgnore', instanced: true, light: true, overhead: true },
  guard_rail: { collider: 'box', layer: 'Prop', h: 1.2 },
  stall: { w: 2.0, d: 2.0, h: 2.4, collider: 'box', layer: 'Prop', instanced: true, note: 'table + awning at 2.2 m' },
  umbrella: { w: 2.2, d: 2.2, h: 2.3, collider: 'capsule-pole-only', layer: 'CameraIgnore', instanced: true, note: 'collider is the pole only, canopy ignored by the camera' },
  bus_shelter: { w: 1.6, d: 4.0, h: 2.6, collider: 'box', layer: 'Prop' },
  bus_stop_pole: { w: 0.15, d: 0.15, h: 2.8, collider: 'capsule', layer: 'Prop', instanced: true },
  street_sign: { w: 0.15, d: 0.15, h: 3.0, collider: 'capsule', layer: 'Prop', instanced: true },
  generator: { w: 1.2, d: 0.8, h: 1.0, collider: 'box', layer: 'Prop', instanced: true, noise: true },
  pos_kiosk: { w: 2.0, d: 2.0, h: 2.5, collider: 'box', layer: 'Prop' },
  tree_almond: { w: 0.5, d: 0.5, h: 7.0, canopy: 5.0, collider: 'capsule-trunk', layer: 'Prop', instanced: true },
  tyre_stack: { w: 0.8, d: 0.8, h: 1.0, collider: 'capsule', layer: 'Prop', instanced: true },
  car_shell: { w: 1.8, d: 4.5, h: 1.45, collider: 'box', layer: 'Prop', instanced: true },
  hoarding: { w: 8.0, d: 0.4, h: 4.0, collider: 'none', layer: 'CameraIgnore', note: 'billboard, mounted high' },
  poster: { collider: 'none', layer: 'CameraIgnore', note: 'quad on a pier, 0.02 off the face' },
  warden_marker: { w: 0.8, d: 0.8, h: 0, collider: 'none', layer: 'Marker' },
  hawker_marker: { w: 0.8, d: 0.8, h: 0, collider: 'none', layer: 'Marker' },
};
const props = [];
const P = (type, id, x, z, ry = 0, extra = {}) => props.push({ id, type, x: r2(x), z: r2(z), ry, ...extra });

// Overhead bands that tall street furniture must avoid (pole 9 m, wires sag to ~7 m).
const tallAvoid = [
  rect(TILE.x0, FLY.z0 - 4, TILE.x1, FLY.z1 + 4),
  rect(TILE.x0, FB.z - 4, TILE.x1, FB.z + 4),
];
const furnX = { W: (A.kerbW[0] - PAVE.gutter - PAVE.furnishing / 2), E: (A.kerbE[1] + PAVE.gutter + PAVE.furnishing / 2) };
const furnZ = { S: (B.kerbS[0] - PAVE.gutter - PAVE.furnishing / 2), N: (B.kerbN[1] + PAVE.gutter + PAVE.furnishing / 2) };
const blockers = () => [...driveways.map((d) => d.r), ...crossings.map((c) => c.r), ...corners.map((c) => c.r), ...tallAvoid,
  rect(TILE.x0, B.paveS[0], TILE.x1, B.paveN[1])];
function placeAlongZ(type, prefix, x, zs) {
  const out = [];
  for (const z0 of zs) {
    let z = z0, ok = false;
    for (let t = 0; t < 4 && !ok; t++, z += 3) {
      const fp = rect(x - 0.5, z - 0.5, x + 0.5, z + 0.5);
      ok = !blockers().some((b) => overlap(fp, b)) && z > TILE.z0 + 2 && z < TILE.z1 - 2;
    }
    if (ok) out.push(r2(z - 3));
  }
  out.forEach((z, i) => P(type, `${prefix}-${i + 1}`, x, z, 0));
  return out;
}
function placeAlongX(type, prefix, z, xs) {
  const out = [];
  for (const x0 of xs) {
    let x = x0, ok = false;
    for (let t = 0; t < 4 && !ok; t++, x += 3) {
      const fp = rect(x - 0.5, z - 0.5, x + 0.5, z + 0.5);
      const bl = [...driveways.map((d) => d.r), ...crossings.map((c) => c.r), rect(A.paveW[0] - 1, TILE.z0, A.paveE[1] + 1, TILE.z1)];
      ok = !bl.some((b) => overlap(fp, b)) && x > TILE.x0 + 2 && x < TILE.x1 - 2;
    }
    if (ok) out.push(r2(x - 3));
  }
  out.forEach((x, i) => P(type, `${prefix}-${i + 1}`, x, z, 0));
  return out;
}
const zPoles = []; for (let z = -145; z < 150; z += 30) zPoles.push(z);
const zLamps = []; for (let z = -130; z < 150; z += 30) zLamps.push(z);
const xPoles = [-110, -80, -50, -20, 20, 50, 80, 110];
const xLamps = [-95, -65, -35, 35, 65, 95];
const poleRuns = {};
poleRuns['A-W'] = placeAlongZ('nepa_pole', 'nepa-AW', furnX.W, zPoles).map((z) => [furnX.W, z]);
poleRuns['A-E'] = placeAlongZ('nepa_pole', 'nepa-AE', furnX.E, zPoles).map((z) => [furnX.E, z]);
poleRuns['B-N'] = placeAlongX('nepa_pole', 'nepa-BN', furnZ.N, xPoles).map((x) => [x, furnZ.N]);
poleRuns['B-S'] = placeAlongX('nepa_pole', 'nepa-BS', furnZ.S, xPoles).map((x) => [x, furnZ.S]);
placeAlongZ('street_lamp', 'lamp-AW', furnX.W, zLamps);
placeAlongZ('street_lamp', 'lamp-AE', furnX.E, zLamps);
placeAlongX('street_lamp', 'lamp-BN', furnZ.N, xLamps);
placeAlongX('street_lamp', 'lamp-BS', furnZ.S, xLamps);
// NEPA wires: sagging strands between consecutive poles on one run. A span is
// dropped if it would pass through the flyover or footbridge band.
const wires = [];
for (const [run, pts] of Object.entries(poleRuns)) {
  for (let i = 0; i + 1 < pts.length; i++) {
    const [ax, az] = pts[i], [bx, bz] = pts[i + 1];
    const span = rect(ax - 0.1, az - 0.1, bx + 0.1, bz + 0.1);
    const blocked = tallAvoid.some((b) => overlap(span, b)) || Math.hypot(bx - ax, bz - az) > 40;
    if (!blocked) wires.push({ id: `wire-${run}-${i + 1}`, from: [ax, az], to: [bx, bz], topY: 8.6, sagY: 7.4, strands: 3 });
  }
}
// Soffit lamps under the flyover, over the road and the market (mounted to the deck underside).
[-60, -45, -30, -5, 5, 30, 45, 60].forEach((x, i) => P('soffit_lamp', `soffit-${i + 1}`, x, (FLY.z0 + FLY.z1) / 2, 0, { y: 5.7 }));
// Guard rail on the ROAD-A median (cut at the junction crossings).
medians.forEach((m) => P('guard_rail', `rail-${m.id}`, 0, (m.r.z0 + m.r.z1) / 2, 0, { from: [0, m.r.z0], to: [0, m.r.z1] }));
// Under-bridge market: 3 rows of stalls each side, 8 stalls per segment, aisles 2.6 m.
const rowsZ = [41.0, 45.6, 50.2];
const segs = [[-17, -33], [-43, -59]];
for (const sg of [-1, 1]) for (const [ri, zc] of rowsZ.entries()) for (const [si, [xa]] of segs.entries()) {
  for (let k = 0; k < 8; k++) {
    const x = sg * (Math.abs(xa) + 1 + k * 2);
    P('stall', `stall-${sg < 0 ? 'W' : 'E'}-r${ri + 1}-s${si + 1}-${k + 1}`, x, zc, sg < 0 ? 90 : 270);
  }
}
// Umbrellas + hawkers on the plazas (never in a clear zone).
[[-46, -30], [-38, -30.5], [-34, -26], [18, -40], [20, -8], [30, -10]].forEach(([x, z], i) => P('umbrella', `umbrella-${i + 1}`, x, z));
[[-44, -26], [-38, -30.5], [-14.2, 38.5], [14.2, 38.5], [18, -36], [22, -6], [-11.7, 0], [-11.7, -15]].forEach(([x, z], i) => P('hawker_marker', `slot-hawker-${i + 1}`, x, z, 0, { note: 'M4 job slot marker' }));
// Bus stop: OSHODI UNDER BRIDGE (shelter behind the pavement, pole in the furnishing zone).
P('bus_shelter', 'shelter-oshodi-under-bridge', -12.2, 45, 0, { opensTo: 'east (the road)', sign: 'OSHODI UNDER BRIDGE' });
P('bus_stop_pole', 'stop-pole-oshodi-under-bridge', furnX.W, 42, 0, { sign: 'OSHODI UNDER BRIDGE' });
// Street name signs at the junction corners (green and white).
P('street_sign', 'sign-NW', furnX.W, furnZ.N, 0, { text: 'AGEGE MOTOR ROAD' });
P('street_sign', 'sign-SE', furnX.E, furnZ.S, 0, { text: 'AGEGE MOTOR ROAD' });
P('street_sign', 'sign-NE', furnX.E, furnZ.N, 0, { text: 'SIDE STREET (OSM NAME)' });
P('warden_marker', 'slot-lastma-warden', 9.8, -65.0, 225, { note: 'LASTMA warden stands here at rush hour (later milestone)' });
// Generators by shop fronts (in the 1.0 m setback strip) and in the mechanic yard.
[[-11.85, 66], [-11.85, 103.5], [11.85, 78.5], [11.85, -98.5], [-11.85, -111]].forEach(([x, z], i) => P('generator', `gen-${i + 1}`, x, z, 0, { w: 0.6, d: 1.2 }));
P('generator', 'gen-yard', -30, -4, 0);
P('pos_kiosk', 'kiosk-pos', 19, 12, 0, { sign: 'POS' });
// Mechanic yard: car shells and tyre stacks.
[[-44, -14, 0], [-38, -6, 15], [-46, -3, 80]].forEach(([x, z, ry], i) => P('car_shell', `car-shell-${i + 1}`, x, z, ry));
[[-30, -18], [-29, -16.6], [-34, -20]].forEach(([x, z], i) => P('tyre_stack', `tyres-${i + 1}`, x, z));
// Trees: almond trees on the plazas, none by the road (Oshodi is concrete).
[[-46, -26], [-46, -31], [-40, -26], [19, 20], [19, 26]].forEach(([x, z], i) => P('tree_almond', `tree-${i + 1}`, x, z));
// Hoardings (parody brands only, L-22). Each names where its reader stands (L-60).
P('hoarding', 'hoarding-T2-roof', 34.2, -24, 270, { y: 16.5, text: 'NAIJA COLA', readFrom: 'footbridge deck and ROAD-A southbound' });
P('hoarding', 'hoarding-flyover-south', 0, FLY.z0 - 0.3, 180, { y: 7.4, text: 'KUDI BANK', readFrom: 'walking north on ROAD-A from the spawn' });
P('hoarding', 'hoarding-NW-roof', -18, 97.5, 90, { y: 13.5, text: 'WAZO MOBILE', readFrom: 'under the flyover looking north' });

// ---------------------------------------------------------------- south approach road (separate scene)
// ROAD-A carries on south out of the tile as a plain road (same cross-section, low
// walls and fences, no shops). It serves two jobs: (1) the M4 conductor route, a
// drop stop about 600 m from the park with a roundabout to turn and come back;
// (2) the M2.3 streaming test (start at the far end, walk north, the Oshodi tile
// wakes and loads). Scene: Approach_South, loaded with Bootstrap.
const APP = { z0: -800, z1: TILE.z0 };
const RB = { x: 0, z: -720, island: 9.0, ring: 7.0 };   // roundabout: island radius 9, ring lane 7 m (one wide lane)
const LAYBY = { z0: -655, z1: -637, depth: 3.0 };
const approach = {
  id: 'approach-south', scene: 'Approach_South', road: 'ROAD-A (continued)', from: APP.z1, to: APP.z0,
  crossSection: 'same as ROAD-A in the tile: 2 lanes each way, 2.1 m median with guard rail, kerbs 0.12, 3.5 m pavements both sides',
  lanes: [
    { id: 'app-west-sb', dir: 'southbound', r: rect(A.wCw[0], RB.z + RB.island + RB.ring + 4, A.wCw[1], APP.z1) },
    { id: 'app-east-nb', dir: 'northbound', r: rect(A.eCw[0], RB.z + RB.island + RB.ring + 4, A.eCw[1], APP.z1) },
  ],
  dropStop: {
    id: 'drop-stop-south', name: 'Drop stop (use the real next stop name south of Oshodi on Agege Motor Road, from OSM)',
    layBy: rect(A.wCw[0] - LAYBY.depth, LAYBY.z0, A.wCw[0], LAYBY.z1),
    stopPoint: { x: r2(A.wCw[0] - LAYBY.depth / 2), z: -646, ry: 180, note: 'danfo stops here, sliding door (right side) faces the west pavement' },
    alightPoint: { x: r2(A.wCw[0] - LAYBY.depth - KERB - 1.2), z: -646, note: 'passengers step down onto the pavement here' },
    shelter: { x: r2(A.wCw[0] - LAYBY.depth - KERB - PAVE_W - 1.0), z: -646, size: [1.6, 4.0], note: 'behind the pavement, never on it' },
    routeDistanceFromPark: null,
    note: 'Lay-by is cut into the west verge so a stopped danfo never blocks the through lane; the pavement steps out around it and keeps 3.5 m.',
  },
  roundabout: {
    id: 'turn-roundabout-south', centre: [RB.x, RB.z], islandRadius: RB.island, ringOuterRadius: RB.island + RB.ring, kerbHeight: 0.12,
    note: 'Turning point for the out-and-back. Drivable round the ring, never through the island. Island collider: cross plus centre square or a cylinder, never one square box (lesson L-125).',
  },
  stubSouth: rect(A.wCw[0], APP.z0, A.eCw[1], RB.z - RB.island - RB.ring),
  endBarrier: { z: APP.z0 + 1, note: 'solid barrier and fence across the road end; skyline continues beyond' },
  streamingTestStart: { x: furnX.W - 1.5, y: Y_PAVE, z: -790, ry: 0, note: '640 m from the tile edge, just outside the 600 m wake ring' },
};
{
  // route length: exit driveway (z -48.5) south to the stop (z -646)
  approach.dropStop.routeDistanceFromPark = r2(-48.5 - approach.dropStop.stopPoint.z);
}

// ---------------------------------------------------------------- lights, spawn, views
const spawn = { x: -10.2, y: Y_PAVE, z: -20, ry: 0, note: 'west pavement clear zone by Terminal 3 plaza, facing north to the flyover' };
const viewpoints = [
  { id: 'V1', x: -10.2, y: Y_PAVE, z: -20, ry: 0, wat: '13:00', shows: 'road, median rail, footbridge overhead, flyover and market ahead, KUDI BANK hoarding' },
  { id: 'V2', x: 0, y: FB.deckTop, z: -30, ry: 180, wat: '13:00', shows: 'from the footbridge deck: danfo park 3-deep, junction, Terminal 3, NAIJA COLA behind you' },
  { id: 'V3', x: -22.3, y: Y_PAVE, z: -40.5, ry: 270, wat: '17:30', shows: 'island head: route board, conductor slot, danfos 3-deep, Terminal 3 front' },
  { id: 'V4', x: -14.5, y: Y_PAVE, z: 43.3, ry: 270, wat: '13:00', shows: 'under-bridge market aisle, piers with posters, soffit lamps, shelter' },
  { id: 'V5', x: 16, y: Y_PAVE, z: -12, ry: 60, wat: '13:00', shows: 'Terminal 2 curved glass, okada park, POS kiosk' },
  { id: 'V6', x: -10.2, y: Y_PAVE, z: -20, ry: 0, wat: '21:00', shows: 'V1 at night: street lamps, soffit lamps, lit terminal glass, lit windows' },
];

// ---------------------------------------------------------------- checks
const errors = [], notes = [];
const err = (m) => errors.push(m);
function footprint(p) {
  const t = TYPES[p.type] || {};
  if (p.from && p.to) return rect(p.from[0] - 0.1, p.from[1], p.to[0] + 0.1, p.to[1]);
  let w = p.w || t.w || 0.3, d = p.d || t.d || 0.3;
  const a = ((p.ry || 0) * Math.PI) / 180, c = Math.abs(Math.cos(a)), s = Math.abs(Math.sin(a));
  const W = w * c + d * s, D = w * s + d * c;
  return rect(p.x - W / 2, p.z - D / 2, p.x + W / 2, p.z + D / 2);
}
const isOverhead = (p) => { const t = TYPES[p.type] || {}; return t.overhead || (p.y || 0) >= OVERHEAD_MIN; };
const laneRects = lanes.map((l) => l.r);

// 1. Nothing at ground level in a lane, and props keep 0.75 m from lane edges.
for (const p of props) {
  if (isOverhead(p)) continue;
  const fp = footprint(p);
  for (const l of lanes) {
    if (overlap(fp, l.r)) err(`prop ${p.id} (${p.type}) stands in lane ${l.id}`);
    else if (!(TYPES[p.type] || {}).h === 0 && overlap(fp, l.r, LANE_CLEAR)) err(`prop ${p.id} is closer than ${LANE_CLEAR} m to lane ${l.id}`);
    else if ((TYPES[p.type] || {}).h > 0 && p.type !== 'guard_rail' && overlap(fp, l.r, LANE_CLEAR - 1e-6)) err(`prop ${p.id} is closer than ${LANE_CLEAR} m to lane ${l.id}`);
  }
  if (p.type === 'guard_rail') { for (const m of medians) if (Math.abs(p.z - (m.r.z0 + m.r.z1) / 2) < 0.01 && !inside(fp, rect(m.r.x0 - 0.2, m.r.z0, m.r.x1 + 0.2, m.r.z1))) err(`rail ${p.id} leaves the median`); }
}
// 2. Pavement clear zones stay clear (no solid prop), so two people pass anywhere.
for (const p of props) {
  const t = TYPES[p.type] || {};
  if (isOverhead(p) || t.collider === 'none') continue;
  const fp = footprint(p);
  for (const cz of clearZones) if (overlap(fp, cz.r)) err(`prop ${p.id} (${p.type}) blocks pavement clear zone ${cz.id}`);
}
// 3. Buildings: off roads, kerbs, pavements; no overlaps with each other, open areas, structures; inside the tile.
const roadAndPave = [...laneRects, ...medians.map((m) => m.r), ...pavements.flatMap((p) => [p.gutter, p.furnishing, p.clear]), ...corners.map((c) => c.r), ...driveways.map((d) => d.r)];
for (const b of buildings) {
  if (!inside(b.r, TILE)) err(`building ${b.id} leaves the tile`);
  for (const r of roadAndPave) if (overlap(b.r, r)) err(`building ${b.id} overlaps road or pavement`);
  for (const o of buildings) if (o !== b && o.id > b.id && overlap(b.r, o.r)) err(`buildings ${b.id} and ${o.id} overlap`);
  for (const a of areas) if (overlap(b.r, a.r)) err(`building ${b.id} overlaps open area ${a.id}`);
  if (overlap(b.r, PARK.pad)) err(`building ${b.id} overlaps the danfo park`);
  if (overlap(b.r, flyover.deck) && b.h > FLY.underside - 0.5) err(`building ${b.id} rises into the flyover`);
  for (const s of footbridge.stairs) if (overlap(b.r, s.footprint) || overlap(b.r, s.footClear)) err(`building ${b.id} hits ${s.id}`);
  if (overlap(b.r, footbridge.deck)) err(`building ${b.id} is under the footbridge deck`);
}
for (const p of props) {
  if (isOverhead(p) || ['hoarding'].includes(p.type)) continue;
  const fp = footprint(p);
  for (const b of buildings) if (overlap(fp, b.r)) err(`prop ${p.id} is inside building ${b.id}`);
  for (const d of driveways) if ((TYPES[p.type] || {}).collider !== 'none' && overlap(fp, d.r)) err(`prop ${p.id} blocks driveway ${d.id}`);
  for (const c of crossings) if ((TYPES[p.type] || {}).collider !== 'none' && overlap(fp, c.r)) err(`prop ${p.id} blocks crossing ${c.id}`);
  if ((TYPES[p.type] || {}).collider !== 'none' && overlap(fp, PARK.pad)) err(`prop ${p.id} is on the danfo park pad`);
  for (const s of footbridge.stairs) if ((TYPES[p.type] || {}).collider !== 'none' && (overlap(fp, s.footprint) || overlap(fp, s.footClear))) err(`prop ${p.id} blocks ${s.id}`);
}
// 4. Overhead structures: clearance over every lane and pavement; piers off roads and pavements.
if (footbridge.undersideY - Y_ROAD < OVERHEAD_MIN) err(`footbridge underside ${footbridge.undersideY} m is below ${OVERHEAD_MIN} m`);
if (flyover.undersideY - Y_ROAD < OVERHEAD_MIN) err(`flyover underside ${flyover.undersideY} m is below ${OVERHEAD_MIN} m`);
for (const pr of [...flyover.piers.map((p) => ({ ...p, s: p.diameter })), ...footbridge.piers.map((p) => ({ ...p, s: p.size }))]) {
  const fp = rect(pr.x - pr.s / 2, pr.z - pr.s / 2, pr.x + pr.s / 2, pr.z + pr.s / 2);
  for (const r of roadAndPave) if (overlap(fp, r, 0.0)) err(`pier ${pr.id} stands on a road or pavement`);
  for (const l of laneRects) if (overlap(fp, l, LANE_CLEAR)) err(`pier ${pr.id} is closer than ${LANE_CLEAR} m to a lane`);
  for (const b of buildings) if (overlap(fp, b.r)) err(`pier ${pr.id} is inside building ${b.id}`);
}
// 5. Footbridge walk: deck reaches past both pavements; stairs land on open plazas; risers and treads in range;
//    chained heights from plaza to deck have no step above the CharacterController step offset (0.3 m).
if (footbridge.deck.x0 > A.paveW[0] || footbridge.deck.x1 < A.paveE[1]) err('footbridge deck does not span both pavements');
for (const s of footbridge.stairs) {
  const land = areas.find((a) => inside(s.footClear, a.r));
  if (!land) err(`${s.id} foot does not land on an open plaza`);
  for (const r of roadAndPave) if (overlap(s.footprint, r)) err(`${s.id} sits on a road or pavement`);
  for (const f of [s.flight1, s.flight2]) {
    const rise = (f.yTop - f.yBottom) / f.risers, run = (f.r.x1 - f.r.x0) / (f.risers - 1);
    if (rise < 0.15 || rise > 0.18) err(`${s.id} riser ${rise.toFixed(3)} m is outside 0.15-0.18`);
    if (run < 0.25 || run > 0.30) err(`${s.id} tread ${run.toFixed(3)} m is outside 0.25-0.30`);
    const slope = Math.atan2(f.yTop - f.yBottom, f.r.x1 - f.r.x0) * 180 / Math.PI;
    if (slope > 40) err(`${s.id} ramp collider slope ${slope.toFixed(1)} deg is above 40 (CharacterController slopeLimit 45)`);
  }
  // headroom check under flight 2 handled by closedUnder; verify it covers where headroom < 2.1
  const f2 = s.flight2, dir = s.id.endsWith('west') ? -1 : 1;
  const xStart = dir < 0 ? f2.r.x1 : f2.r.x0, len = f2.r.x1 - f2.r.x0;
  const xLow = xStart + dir * len * (f2.yTop - (Y_PAVE + HEADROOM)) / (f2.yTop - f2.yBottom);
  if (!(xLow >= s.closedUnder.x0 - 1e-6 && xLow <= s.closedUnder.x1 + 1e-6)) err(`${s.id}: low-headroom point x=${xLow.toFixed(2)} is not closed off underneath`);
  // walk chain: foot -> flight2 -> mid landing -> flight1 -> top landing -> deck
  const chain = [Y_PAVE];
  for (let i = 1; i <= s.flight2.risers; i++) chain.push(s.flight2.yBottom + i * (s.flight2.yTop - s.flight2.yBottom) / s.flight2.risers);
  for (let i = 1; i <= s.flight1.risers; i++) chain.push(s.flight1.yBottom + i * (s.flight1.yTop - s.flight1.yBottom) / s.flight1.risers);
  chain.push(footbridge.deckTopY);
  let maxStep = 0; for (let i = 1; i < chain.length; i++) maxStep = Math.max(maxStep, Math.abs(chain[i] - chain[i - 1]));
  if (maxStep > 0.3) err(`${s.id}: a step of ${maxStep.toFixed(3)} m is above the 0.3 m step offset`);
  if (Math.abs(chain[chain.length - 2] - footbridge.deckTopY) > 0.01) err(`${s.id}: top of the stair (${chain[chain.length - 2]}) does not meet the deck (${footbridge.deckTopY})`);
  notes.push(`${s.id}: plaza ${Y_PAVE} -> deck ${footbridge.deckTopY}, ${s.flight1.risers + s.flight2.risers} risers, max step ${maxStep.toFixed(3)} m`);
}
// 6. Danfo park: danfos inside their loading lane with side margin, never on an island or the pavement.
for (const d of danfoPark.danfos) {
  const fp = footprint({ ...d, w: DANFO.L, d: DANFO.W, ry: 0 });
  const lane = danfoPark.loadingLanes.find((l) => inside(fp, l.r));
  if (!lane) err(`${d.id} is not inside a loading lane`);
  else { const m = Math.min(fp.z0 - lane.r.z0, lane.r.z1 - fp.z1); if (m < 0.5) err(`${d.id} has only ${m} m side margin`); }
  for (const i of danfoPark.islands) if (overlap(fp, i.r)) err(`${d.id} overlaps ${i.id}`);
}
if (!inside(PARK.pad, rect(TILE.x0, B.paveN[1], A.paveW[0], TILE.z1))) err('danfo park pad touches a road or pavement');
for (const s of danfoPark.conductorSlots) if (!danfoPark.islands.some((i) => ptIn(s.x, s.z, i.r))) err(`${s.id} is not on an island`);
// 7. Market aisles >= 2.4 m between stall rows; stalls stay out of the pier bands.
{
  const rowGap = rowsZ[1] - rowsZ[0] - 2.0;
  if (rowGap < AISLE_MIN) err(`market aisle ${rowGap} m < ${AISLE_MIN} m`);
  for (const p of props.filter((q) => q.type === 'stall')) {
    const fp = footprint(p);
    for (const pr of flyover.piers) if (overlap(fp, rect(pr.x - 0.6, pr.z - 0.6, pr.x + 0.6, pr.z + 0.6), 0.6)) err(`stall ${p.id} is too close to pier ${pr.id}`);
    if (!areas.some((a) => a.id.startsWith('under-bridge') && inside(fp, a.r))) err(`stall ${p.id} is outside the under-bridge market`);
  }
}
// 8. Wires never pass through the flyover or footbridge.
for (const w of wires) { const span = rect(w.from[0], w.from[1], w.to[0], w.to[1]); if (tallAvoid.some((b) => overlap(span, b))) err(`${w.id} crosses an overhead structure`); }
// 9. Spawn and viewpoints stand somewhere walkable and clear.
{
  const walk = [...clearZones.map((c) => c.r), ...areas.map((a) => a.r), ...danfoPark.islands.map((i) => i.r), footbridge.deck];
  for (const v of [spawn, ...viewpoints]) {
    if (!walk.some((r) => ptIn(v.x, v.z, r))) err(`${v.id || 'spawn'} is not on a walkable surface`);
    for (const b of buildings) if (ptIn(v.x, v.z, b.r)) err(`${v.id || 'spawn'} is inside ${b.id}`);
  }
}

// 10. Jobs bays (M4): work point on the island at the door, aside points 2-3 m from the door on the island,
//     6 queue spots on the island, all clear of danfos and each other.
for (const b of danfoPark.bays) {
  const isl = danfoPark.islands.find((i) => i.id === b.island);
  const pts = [b.workPoint, ...b.asidePoints, ...b.queue];
  for (const q of pts) if (!ptIn(q.x, q.z, rect(isl.r.x0 + 0.3, isl.r.z0 + 0.3, isl.r.x1 - 0.2, isl.r.z1 - 0.3))) err(`${b.id}: point (${q.x}, ${q.z}) is not on ${isl.id} with 0.3 m to spare`);
  for (const a of b.asidePoints) { const d = Math.hypot(a.x - b.door.x, a.z - b.door.z); if (d < 2 || d > 3) err(`${b.id} ${a.id} is ${d.toFixed(2)} m from the door (want 2-3)`); }
  if (b.queue.length < 6) err(`${b.id} has room for only ${b.queue.length} passengers`);
  for (let i = 0; i < pts.length; i++) for (let j = i + 1; j < pts.length; j++) if (Math.hypot(pts[i].x - pts[j].x, pts[i].z - pts[j].z) < 0.75) err(`${b.id}: two stand points closer than 0.75 m`);
}
if (danfoPark.bays.filter((b) => b.m4).length < 3) err('M4 needs 3 danfo bays');
// 11. Approach road: drop stop about 600 m from the park, lay-by outside the through lane, roundabout clear.
{
  const ds = approach.dropStop, d = ds.routeDistanceFromPark;
  if (d < 550 || d > 650) err(`drop stop is ${d} m from the park (want about 600)`);
  for (const l of approach.lanes) if (overlap(ds.layBy, l.r)) err('drop stop lay-by overlaps a through lane');
  if (!ptIn(ds.stopPoint.x, ds.stopPoint.z, ds.layBy)) err('drop stop point is not in the lay-by');
  if (ds.shelter.x + 0.8 > ds.layBy.x0 - KERB - PAVE_W + 1e-6) err('drop stop shelter is on the pavement');
  const rb = approach.roundabout;
  if (rb.ringOuterRadius - rb.islandRadius < 6.5) err('roundabout ring is narrower than 6.5 m (a danfo needs it to turn)');
  // a danfo (5.9 m, wheelbase ~3.6 m) turns on a 9-16 m ring: mid-ring radius must exceed the danfo's min outer radius (~6.5 m)
  if ((rb.islandRadius + rb.ringOuterRadius) / 2 < 6.5) err('roundabout too tight for a danfo');
  const startDist = APP.z1 - approach.streamingTestStart.z;
  if (startDist <= 600) err(`streaming test start is only ${startDist} m from the tile edge (want > 600 so the tile is asleep at start)`);
}

// ---------------------------------------------------------------- budgets (estimate, checked in Unity by Ted)
const drawCalls = [
  ['road asphalt + lane markings', 2], ['kerbs + median (black/white chequer)', 2], ['pavements + gutter slabs', 2], ['plazas, yard, park pad', 1],
  ['terminals T1-T3 (concrete + glass)', 2], ['shophouses + filler (shared atlas: wall, shopfront, window)', 3], ['signboards + posters + hoardings (one atlas)', 2],
  ['flyover deck + piers', 2], ['footbridge', 2], ['danfos (instanced, body + glass)', 2], ['okadas (instanced)', 1],
  ['NEPA poles (instanced) + wires (one mesh)', 2], ['street lamps + soffit lamps (pole, emissive head)', 2], ['guard rails + park rail', 1],
  ['stalls, umbrellas, stools, tables (instanced)', 4], ['shelter, signs, kiosk, generators, tyres, car shells', 3], ['trees (trunk, canopy)', 2],
  ['skyline shell (one mesh)', 1], ['skybox', 1], ['player', 3], ['main-light shadow casters (Medium tier, 40 m)', 25], ['HUD (M1 stick + sprint)', 4],
];
const dcTotal = drawCalls.reduce((s, [, n]) => s + n, 0);
if (dcTotal >= 100) err(`draw call estimate ${dcTotal} is not under 100`);

const counts = {};
for (const p of props) counts[p.type] = (counts[p.type] || 0) + 1;

const layout = {
  version: 1, generatedBy: 'tools/oshodi/oshodi-layout.js', spec: 'docs/specs/oshodi-greybox.md',
  units: 'metres', axes: 'X east, Z north, Y up; rect = {x0,z0,x1,z1}; ry = yaw degrees clockwise from +Z (Unity Y rotation)',
  levels: { road: Y_ROAD, pavement: Y_PAVE, kerbHeight: 0.12 }, tile: TILE,
  roads, lanes, medians, crossings, pavements, corners, driveways,
  flyover, footbridge, buildings, areas, danfoPark, okadaPark, approach,
  propTypes: TYPES, props, wires, spawn, viewpoints,
  budgets: { drawCallsInViewEstimate: dcTotal, drawCallsInViewMax: 99, drawCallsDistrictMax: 350, drawCallBreakdown: drawCalls },
  counts: { buildings: buildings.length, props: props.length, propsByType: counts, danfos: danfoPark.danfos.length, wires: wires.length, stalls: counts.stall || 0 },
};

// ---------------------------------------------------------------- SVG plan
function svg() {
  const S = 3, pad = 20, W = (TILE.x1 - TILE.x0) * S + pad * 2, H = (TILE.z1 - TILE.z0) * S + pad * 2 + 70;
  const X = (x) => (x - TILE.x0) * S + pad, Z = (z) => (TILE.z1 - z) * S + pad;   // north up
  const R = (r, fill, extra = '') => `<rect x="${X(r.x0).toFixed(1)}" y="${Z(r.z1).toFixed(1)}" width="${((r.x1 - r.x0) * S).toFixed(1)}" height="${((r.z1 - r.z0) * S).toFixed(1)}" fill="${fill}" ${extra}/>`;
  const T = (x, z, t, size = 9, fill = '#111', anchor = 'middle') => `<text x="${X(x).toFixed(1)}" y="${Z(z).toFixed(1)}" font-family="sans-serif" font-size="${size}" fill="${fill}" text-anchor="${anchor}">${t}</text>`;
  const o = [];
  o.push(`<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">`);
  o.push(`<rect width="100%" height="100%" fill="#d9cdb5"/>`);
  for (const a of areas) o.push(R(a.r, '#ece5d6'));
  for (const p of pavements) { o.push(R(p.gutter, '#9a9a9a')); o.push(R(p.furnishing, '#c4c4c4')); o.push(R(p.clear, '#e2e2e2')); }
  for (const c of corners) o.push(R(c.r, '#e2e2e2'));
  for (const l of lanes) o.push(R(l.r, '#4a4a4a'));
  for (const m of medians) o.push(R(m.r, '#f0f0f0', 'stroke="#111" stroke-width="0.5" stroke-dasharray="2,2"'));
  for (const d of driveways) o.push(R(d.r, '#6d6d6d', 'stroke="#e8c200" stroke-width="1"'));
  for (const c of crossings) o.push(R(c.r, 'url(#zebra)'));
  o.push(R(PARK.pad, '#7a7a7a'));
  for (const i of danfoPark.islands) o.push(R(i.r, '#bdbdbd'));
  for (const c of danfoPark.walkCrossings) o.push(R(c.r, 'url(#zebra)', 'opacity="0.6"'));
  for (const d of danfoPark.danfos) o.push(R(footprint({ ...d, w: DANFO.L, d: DANFO.W, ry: 0 }), '#f5c400', 'stroke="#111" stroke-width="0.6"'));
  for (const b of buildings) {
    const fill = b.kind === 'terminal' ? '#7fb3c9' : b.kind === 'filler' ? '#a39a8a' : '#c08a64';
    o.push(R(b.r, fill, 'stroke="#333" stroke-width="0.6"'));
    if (b.kind === 'terminal') o.push(T((b.r.x0 + b.r.x1) / 2, (b.r.z0 + b.r.z1) / 2, b.id, 14, '#0b3142'));
  }
  o.push(R(flyover.deck, '#304060', 'opacity="0.28"'));
  for (const p of flyover.piers) o.push(`<circle cx="${X(p.x)}" cy="${Z(p.z)}" r="${(p.diameter / 2) * S}" fill="#304060"/>`);
  o.push(R(footbridge.deck, '#2f7d3a', 'opacity="0.85"'));
  for (const s of footbridge.stairs) { o.push(R(s.closedUnder, '#2f7d3a', 'opacity="0.55"')); o.push(R(s.flight1.r, '#5fbf6b', 'opacity="0.85"')); o.push(R(s.topLanding.r, '#2f7d3a')); }
  for (const p of props) {
    const fp = footprint(p);
    const col = { nepa_pole: '#5b3a1a', street_lamp: '#e8a400', soffit_lamp: '#ffd34d', stall: '#b5651d', umbrella: '#d9412b', tree_almond: '#2e8b3e', generator: '#333', hawker_marker: '#9c27b0', warden_marker: '#9c27b0', bus_shelter: '#1e5aa8', bus_stop_pole: '#1e5aa8', guard_rail: '#e8c200', street_sign: '#1b7f3a', pos_kiosk: '#1e5aa8', car_shell: '#666', tyre_stack: '#222', hoarding: '#ff00aa' }[p.type] || '#000';
    if (p.type === 'guard_rail') { o.push(`<line x1="${X(0)}" y1="${Z(p.from[1])}" x2="${X(0)}" y2="${Z(p.to[1])}" stroke="${col}" stroke-width="1.2"/>`); continue; }
    if (p.type.endsWith('marker')) { o.push(`<circle cx="${X(p.x)}" cy="${Z(p.z)}" r="2" fill="none" stroke="${col}" stroke-width="1"/>`); continue; }
    o.push(R(fp, col, p.type === 'umbrella' || p.type === 'tree_almond' ? 'opacity="0.55"' : ''));
  }
  for (const w of wires) o.push(`<line x1="${X(w.from[0])}" y1="${Z(w.from[1])}" x2="${X(w.to[0])}" y2="${Z(w.to[1])}" stroke="#5b3a1a" stroke-width="0.5" stroke-dasharray="3,2"/>`);
  for (const b of okadaPark.bikes) o.push(R(footprint({ ...b, w: 2.0, d: 0.7 }), '#c62828'));
  for (const s of danfoPark.conductorSlots) o.push(`<circle cx="${X(s.x)}" cy="${Z(s.z)}" r="2" fill="#9c27b0"/>`);
  for (const v of viewpoints) { o.push(`<circle cx="${X(v.x)}" cy="${Z(v.z)}" r="4" fill="none" stroke="#d00" stroke-width="1.5"/>`); o.push(T(v.x + 4, v.z + 3, v.id, 10, '#d00', 'start')); }
  o.push(`<polygon points="${X(spawn.x)},${Z(spawn.z) - 7} ${X(spawn.x) - 4},${Z(spawn.z) + 3} ${X(spawn.x) + 4},${Z(spawn.z) + 3}" fill="#d00"/>`);
  o.push(T(0, 140, 'AGEGE MOTOR ROAD', 10, '#fff'));
  o.push(T(-95, -71.5, 'SIDE STREET (OSM NAME)', 9, '#fff'));
  o.push(T(-95, 46, 'OSHODI-APAPA EXPRESSWAY (flyover, market below)', 10, '#14213d', 'start'));
  o.push(T(-31, -50, 'DANFO PARK', 10, '#111'));
  o.push(T(0, -22, 'FOOTBRIDGE', 9, '#0d3d14'));
  o.push(T(16, 10, 'OKADA', 8, '#7a0000', 'start'));
  o.push(T(-39, -12, 'MECHANIC YARD', 8, '#333'));
  // north arrow + scale bar
  o.push(`<text x="${W - 30}" y="${pad + 14}" font-family="sans-serif" font-size="14" font-weight="bold">N</text><line x1="${W - 25}" y1="${pad + 18}" x2="${W - 25}" y2="${pad + 40}" stroke="#111" stroke-width="2"/>`);
  o.push(`<line x1="${pad}" y1="${H - 50}" x2="${pad + 50 * S}" y2="${H - 50}" stroke="#111" stroke-width="2"/><text x="${pad}" y="${H - 36}" font-family="sans-serif" font-size="10">50 m</text>`);
  ['Oshodi greybox M2 plan (tools/oshodi/oshodi-layout.js). Tile 240 x 300 m, north up.',
    'Red circles V1-V6 = QA viewpoints, red triangle = spawn, purple = job slots.',
    'Yellow line = median guard rail. Green = footbridge. Blue band = flyover overhead.',
    'Approach road, drop stop and roundabout continue south (see the JSON, approach).']
    .forEach((t, i) => o.push(`<text x="${pad + 170}" y="${H - 58 + i * 13}" font-family="sans-serif" font-size="10">${t}</text>`));
  o.push(`<defs><pattern id="zebra" width="6" height="6" patternUnits="userSpaceOnUse"><rect width="6" height="6" fill="#4a4a4a"/><rect width="3" height="6" fill="#f4f4f4"/></pattern></defs>`);
  o.push('</svg>');
  return o.join('\n');
}

fs.writeFileSync(OUT_JSON, JSON.stringify(layout, null, 1) + '\n');
fs.writeFileSync(OUT_SVG, svg() + '\n');
console.log(`Oshodi greybox layout: ${buildings.length} buildings, ${props.length} props (${Object.entries(counts).map(([k, v]) => `${k} ${v}`).join(', ')}), ${danfoPark.danfos.length} danfos, ${wires.length} wire spans.`);
notes.forEach((n) => console.log('  ' + n));
console.log(`  draw calls in view (estimate): ${dcTotal} (limit < 100)`);
if (errors.length) { console.log(`FAIL: ${errors.length} rule(s) broken:`); errors.forEach((e) => console.log('  - ' + e)); process.exit(1); }
console.log('PASS: every placement rule holds.');
require('./oshodi-build-list.js').write();
