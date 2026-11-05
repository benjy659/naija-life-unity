# Naija Life Unity — Migration Plan

**Decision:** owner decision Nov 5, 2026 — STOP the browser/HTML-JS version, rebuild in Unity.
Same game plan, same ideas — new engine. The browser version is FROZEN (live site stays on
its Nov 1 build, LOAD_V 20261101f; local frozen tip `morning-release-20261105-reconciled`
af42cd56, LOAD_V 20261105h, verified GO but never pushed). No code carries over. Only the
design carries over. See `DESIGN-INVENTORY.md` for the full design record.

**Launch target (carried over):** Lagos + Abuja playable at minimum; Port Harcourt first
city after Lagos. Multiplayer at launch. Quality first over quantity.

## 1. What carries over (design, not code)

Full record: `DESIGN-INVENTORY.md` (compiled from `~/workspace/naija-life/docs/`,
`TASKS.md`, and the goal `GOAL.md` — read-only inventory, Nov 5, 2026).

| System | Carries over as design |
|---|---|
| World | Lagos districts (Oshodi start, Makoko, Ikeja, Lekki/VI/Ikoyi, Surulere, Yaba, Lagos Island) + Abuja zones and landmarks L1–L19, real OSM road layouts, compressed/recognizability mapping, district identity rules, world budgets (GLB ≤50k tris landmark / ≤8k filler, ≤350 draw calls per district, 30 fps at 390×844) |
| Player | Third-person, 1D blend locomotion (walk <2.6 m/s, jog 4.2 m/s), character creator + wardrobe (130-row starter catalogue), needs (hunger/energy/Pawa ≥25 to clock in), onboarding "Lapo or Nepo" bands, one device per account |
| Camera (HARD RULE) | One steady damped follow angle locked behind the player, walking and driving; no sudden swings, orbit drift, or automatic angle changes; occlusion pull-in/fade so a building/wall can never hide the player; interiors use the player's floor as camera floor |
| Police / B-39 | Hit requires REAL body contact (vehicle OBB vs ped capsule, ≥15 km/h, one report per impact, 1.5 s window); near miss is NOT a crime; no arrest from a counter alone — arrest only after police chase + catch (vehicle: officer within 3 m AND speed <1.5 m/s; on foot: officer within ~1.3 m for ~1.2 s); children never count; genuine hit = 1 star + ₦10,000 fine; wanted 1–5 stars, witnesses, heat decay, case files at ★★★+, LASTMA fines, arrest flow (cuff → station → booking → cell), bail, court, criminal record, impound, player police career (Phase 2) |
| Vehicles | Danfo, keke, BRT, city bus, Red Line train, FCT taxis, patrol Hiluxes; garage is server truth; buy outright or lien (20% down, 12–52 weekly instalments, 8% flat); licences (Class D, CPL) via server-scored tests; aviation top tier (helicopter ₦2.5bn, private jet ₦9bn, MMIA, flight school ₦2.5M) |
| Property | Buy and actually live in homes — starter ₦450k–900k, mid ₦3M–18M, fine ₦45M–150M, Banana Island mansion ₦150M (owner decision), garage slots 1/2/4/6+helipad; 20% deposit + weekly instalments (12/26/52 weeks); rent-out at 0.7%/week; sleep/cook/respawn at active home; interiors furnished and lit; garage shows ALL owned cars; customization (≤16 KB design, ≤200 items); Naira Coins never buy property |
| Jobs | City works on its own — bots do every job; a real player takes over a free job slot (walk up, one prompt, bot steps aside, never vanishes); one job at a time; danfo conductor, ride driver, okada rider built; danfo driver, POS agent, mechanic, tailor, hawker, trader TODO; 10 formal careers with interviews (60–80% pass), shift caps (3/in-game-day), Pawa ≥25 to clock in; side gigs, busking, clubs, businesses, loans |
| Economy | ₦V virtual naira, nothing cashes out; three laws (money is earned never granted, scale feels like real Lagos, big money needs big play); hash-chained server ledger, balance = latest `balance_after`, every credit server-verified via RPC; grind arc (room in ~1 week, flat ~2 months, duplex 3–4 months, coach 12–18 months); sinks (casino RTP 94%, LagosBet, rent, fines, impound ₦5k/day, taxes); businesses (registration ₦50,000, weekly plot rent, staff); B-38 transfers (see §5) |
| Multiplayer/social | Players must see each other at launch; friends cap 300; street card; chat (lobby + ephemeral pair DMs + crew channels, device-local logs with disclosure); crews (20 members v1, one crew per player, no crew wallet in v1); respect tiers; Area Board; elections post-launch; real players only, no real politics; **no voice chat** |
| UI/HUD | In-game phone (Bank, Property, Jobs, Naija Ride, Flights, Shop, Invest, Chat, Contacts, Nearby, Map, Events, Gov House, My Crew, Settings, NaijaGram, NaijaNews); HUD stacking rule (top-right rows +52/+100/+148/+196, one element per row); one prompt system; toasts capped 3 live / 5 queued; ≥44 px touch targets; brand: Nigerian-flag green, gold gradient CTAs, indigo night panels |
| Progression | Unlock ladder, Life Record (permanent), story mode, family post-launch, events (Detty December, Sallah, owambe), transit timetables (BRT/danfo/keke/Red Line/Abuja buses), interiors (additive scenes, not world-Y offset), real-player ads (Flutterwave), streaming/LOD budgets |

**Does NOT carry over:** all `js/` (Three.js, bundle builder, LOAD_V versioning), `localStorage`
caches (several were security holes), `quiz.html` client start grant, Supabase Realtime as the
movement transport, browser QA harness, CSS brand tokens (palette/logo assets carry; CSS does
not), and known browser bugs (phantom-hit radius, counter-based jail, client money minting).

## 2. Engine — Unity 6.3 LTS `6000.3.28f1`, URP 17.3 Forward

- **Version:** Unity **6.3 LTS, patch `6000.3.28f1`** (released 2026-11-03, latest patch in the
  6.3 line as of Nov 5, 2026 — verified via endoflife.date + Unity docs). Pin this exact patch;
  everyone installs via Unity Hub. Standard support to 2027-12-04. Do NOT start on 6.0 LTS
  (support ended 2026-10-16). Do NOT build on 6.6 (short-lived update release). Re-evaluate a
  move to **6.7 LTS** only after it ships (expected Dec 2026) and gets first patches.
- **Render pipeline:** **URP 17.3, Forward renderer**, Linear color space, Universal 3D template.
  Decisive: HDRP cannot target WebGL at all; Built-in was deprecated in Unity 6.5 and is
  supported only through the 6.7 LTS lifecycle. URP gives SRP Batcher, GPU instancing, Shader
  Graph, the widest Asset Store support, and the smallest build footprint. What it gives up
  (volumetrics, SSR, ray tracing) is not on the critical path for a stylized Lagos on
  WebGL/mobile. **Switching pipelines later is a rewrite — this decision is locked day one.**
  Set Low/Medium/High URP quality tiers from day one, dynamic resolution as safety valve.
- **Day-one packages (pinned in `Packages/manifest.json`):** URP 17.3, Input System 1.20.0,
  Cinemachine 3.x, Addressables 2.10.3, TextMeshPro, Test Framework. Burst/Mathematics/
  Collections come as dependencies. Deliberately deferred: Entities/DOTS (only if crowds prove
  CPU-bound — measure first), VFX Graph (GPU sim unavailable on WebGL 2), any paid Asset
  Store packs (evaluate after the URP foundation runs).

## 3. Build target — WebGL first, mobile later

- WebGL is the first target (play in the browser, like the old version). Mobile after.
- **WebGL reality (verified):** single-threaded (no C# threads, no Job System workers, no
  TCP/UDP sockets — coroutines/`Awaitable` only); WebSocket-only networking; WASM heap ceiling
  4 GB in 6.3, but real browsers cap a tab near ~2 GB and **iOS Safari per-page limit means
  targeting 400–600 MB total**; WebGL 2.0 only (no compute shaders — no GPU Resident Drawer,
  no VFX Graph GPU sim, no Adaptive Probe Volumes); no direct filesystem (IndexedDB/
  `PlayerPrefs` for saves). WebGPU is experimental in 6.3 and only fully supported in 6.6 —
  do NOT enable it for production; re-evaluate at 6.7 LTS.
- **Budgets:** initial compressed download **under ~50 MB** (empty 3D URP project is ~10.7 MB
  Brotli; mid-range titles land 30–80 MB). Brotli is ~10–15% smaller than gzip but needs the
  server to serve `.br` with correct `Content-Encoding` — verify GitHub Pages serves the
  build's compression correctly (or fall back to gzip) before assuming Brotli load times.
  Stream districts/interiors/vehicles via Addressables + remote CDN after first paint.
  Guideline: under ~100 draw calls and ~500k–1M triangles with LOD for 60 fps on mid-range.
- **Loading UX:** real loading/progress screen from the first byte; stream scenes additively;
  never a blank page while WASM compiles (typical startup 3–8 s even optimized).

## 4. Multiplayer — Photon Fusion 2 (Shared Mode) + Supabase backend

- **Recommendation (multiplayer workstream, verified pricing Nov 5, 2026): Photon Fusion 2,
  Shared Mode at launch + Supabase as the persistent backend.** Runner-up: Mirror +
  self-hosted VPS (best anti-cheat per dollar, slowest to ship, highest ops burden).
  NGO + Relay not recommended as primary (weakest per-session scale, Relay cap 100,
  Unity's dedicated hosting deprecated Mar 2026). **Supabase Realtime as the movement
  layer is REJECTED** — message-rate maths fails at ~50 players on Pro, and it has no
  tick, prediction, or authority.
- **Topology:** district sharding is mandatory in every option — no single seamless
  500-player Lagos. One Fusion room per district/instance (target 50–100 players per
  room; Fusion supports up to 200, tick-based, 60 Hz, built-in AoI). Cross-district
  presence/friends handled outside the movement layer (Supabase).
- **Split:** Fusion = movement/presence only. Supabase (existing project `naija-life`,
  ref `jdumcxvqjtgnbeuvlufq`, ca-central-1) = Auth, profiles, ledger, transfers,
  property, friends/DMs, area board — via REST + Edge Functions, never via Fusion state.
  **Money rule:** all transfers, purchases, earnings, property changes validated in
  Supabase Edge Functions/RPC with RLS. Fusion never decides who owns what or how much
  money anyone has.
- **Transport swap (owner directive):** Shared → Server Mode is a mode flip in the same
  Fusion codebase, not a rewrite. But the real protection is architectural: **a thin
  netcode abstraction layer is mandatory before any gameplay netcode is written**
  (session service, player-replication interface, world-event bus; simulation separated
  from transport; Supabase as source of truth for persistent state). Gameplay code must
  never call the netcode SDK directly.
- **Carried-over netcode design (from the browser spec):** 200 m cells, join own cell + 8
  around it, `nearby()` max 40 nearest, binary movement messages, driving 4 Hz / walking
  2 Hz + immediate on start/stop, interpolate remote players (drivers ~300 ms, walkers
  ~600 ms in the past), identity looked up via street-card RPC (cached 10 min), never
  from payload.
- **Anti-cheat:** Shared Mode is client-authoritative for movement — mitigate by validating
  speed/teleport/earnings server-side in Edge Functions; plan the move to Fusion Server
  Mode before the economy scales or cheating appears.
- **Costs (verified Nov 5, 2026 — re-check live pages before any purchase):**
  Fusion 100 CCU free evergreen → $95/year (200 CCU) → $125/mo (500 CCU) → $250/mo
  (1,000 CCU) → $500/mo (2,000 CCU). Supabase Free now → Pro $25/mo when the owner
  approves it on exact terms (spend-cap-off is a separate explicit yes). **Launch cost $0.**
  Nothing is spent now; paid tiers only when CCU proves it. Do not pre-buy.

## 5. Economy & transfers — B-38 (owner-gated)

- B-38 transfer spec: player-to-player only, by username. Caps: min ₦100 / max ₦500,000;
  sender per WAT day 10 transfers + ₦1,000,000; receiver per WAT day ₦1,000,000; sender
  account ≥7 days AND ≥3 shifts/sales. Anti-abuse: no self-transfer, no same-device
  transfer, 24h hold on re-sending received money, pair flag over ₦2M/7 days (review,
  not block). Ledger: two rows in one transaction, idempotent via `client_ref`.
- **Status:** spec approved for BUILD; applying migration 0040 needs Benjamin's SEPARATE
  OK; B-27 (profiles) + B-29 (username lock) must go live first; Send tab stays hidden
  until the RPC is live and a two-account test passes. The Unity team must not assume
  B-38 is approved — `transfer-spec.md` is the authority and supersedes earlier drafts.
- RPC contracts carry over in shape (names, caps, error codes: `insufficient`,
  `not_found`, `limit`, `self`, `same_device`, `too_small`, `too_much`, `too_new`).

## 6. Milestones to first playable slice

**M0 — Scaffold (done Nov 5, 2026):** repo structure, `.gitignore`, pinned package
manifest, README, this plan. First open in the Unity Editor still UNVERIFIED (no Editor
on this machine).

**M1 — Player controller + third-person camera.** CharacterController + 1D speed-blend
locomotion (idle/walk/jog/run/sprint), Cinemachine damped follow locked behind the player
with collision pull-in, camera-relative movement, Input System action maps (touch joystick
+ keyboard/mouse). *Acceptance:* walk/run in a greybox; camera never swings, drifts, or
hides the player; verified at phone width and desktop.

**M2 — One Lagos street scene.** Oshodi greybox: road, kerbs + pavements both sides,
danfo park, footbridge, props; day/night lighting; URP quality tiers; Addressables
streaming stub. *Acceptance:* reads as Oshodi; 60 fps target on mid-range; <100 draw
calls in view; no frame >200 ms entering the district.

**M3 — Driving.** WheelCollider arcade vehicle controller (danfo + one car, per-vehicle
ScriptableObject), enter/exit any parked drivable, chase camera under the same rules as
on foot, damage model stub. *Acceptance:* drive the Oshodi loop, enter/exit cleanly,
no camera swing, no regressions to walking.

**M4 — Jobs loop.** Job-slot system + ONE job end-to-end (danfo conductor: shout + change,
pay via server-verified RPC stub), Pawa ≥25 gate, 3-shifts/day cap, bot handover (walk up,
one prompt, bot steps aside and waits, walks back when you let go). *Acceptance:* take
over from a bot, do the work, get paid, bot returns — all visible in-game.

**M5 — Multiplayer smoke test.** Fusion room per district, 2+ players see each other
move, presence, street-card lookup, text chat (lobby + DM), Supabase Auth + profile.
*Acceptance:* two clients in one room, movement syncs, chat works, no fake players.

**M6 — First playable slice (definition of done):** M1–M5 integrated in one build —
spawn in Oshodi, walk, drive a danfo, complete a conductor shift, see other players,
open the phone (wallet balance from the server ledger). *Acceptance:* played end-to-end
in the Unity Editor AND a WebGL build, on desktop and phone widths. No regressions on
any working feature. Nothing counts as done on code review alone — it must be seen running.

**After M6:** property (buy/live/garage), B-38 transfers (owner-gated), Abuja, police
full loop (chase/catch/arrest/cell/bail/court), formal careers, transit timetables,
interiors, aviation top tier — in that priority order, quality first.

## 7. Verification rules (owner-mandated, carried over)

- No fix counts as done on code review or static analysis alone. Before any milestone
  is called done, the builder must SEE it working: play it in the Editor and in a WebGL
  build, on phone and laptop widths.
- If it cannot be verified visually/behaviorally, report it as UNVERIFIED, never as done.
- Real-device checks (iPhone/Android) remain the gate for phone claims; emulation alone
  never counts as DONE.
- UX checklist on every slice: easy to control? Nothing blocking the screen? Possible
  in real life (real Lagos)? No notification spam? Could it be better — and if yes, do
  it now, not in a rebuild.

## 8. Open / owner-gated decisions (do NOT assume)

1. B-38 transfers — build approved; applying migration 0040 needs Benjamin's separate OK.
2. Supabase Pro purchase ($25/mo, spend cap off) — direction confirmed; actual purchase
   needs his yes on exact terms.
3. Abuja homes at launch — owner to confirm; only the owner opens cities.
4. Property/economy caps — mansion ₦150M is an owner decision; re-confirm caps in Unity.
5. Game NPC voices go through the single voice system. Spirited Canyon is the
   BenoBibleChronicles *channel* voice, not the game voice — do not conflate.
6. Real money (Flutterwave) = Naira Coins packs + ad space only. Nothing else without
   his explicit yes on exact terms.
7. "Real people only, no real politics" — no fake NPC players/votes/counts; no real party
   names, politicians, party colours/logos, cult/confraternity names anywhere.
8. No voice chat; no teleport (cinematic flyover/journey-home sequences are travel,
   not teleport — and you travel home yourself).
9. MudWorks is a separate, stopped project — nothing from it carries into Naija Life.

## 9. Risks

- **Scope:** a full rebuild is months of work; the Nov 15 browser launch is OFF and
  there is no launch date for Unity yet. Do not promise one until M6 is played.
- **WebGL performance:** the 400–600 MB iOS memory target and single-threaded main loop
  are the tightest constraints; measure early (M2) or the city will not fit.
- **Multiplayer cheating in Shared Mode:** mitigated by server-side validation, but the
  Server Mode move must be planned before the economy scales.
- **Asset costs:** no paid Asset Store packs without the owner's explicit yes on exact
  terms. Greybox with primitives/ProBuilder first.
- **Team continuity:** Claude remains tech lead; his branch work merges to main.
  Main branch only. Never mint GitHub tokens — pushes via `git_push_api.py` vault
  surrogate.

## 10. Phase 2 checklist (waits for the repo URL)

1. Push scaffold to `benjy659/naija-life-unity` main.
2. Write `TASKS.md` in the new repo so Claude (tech lead) can pick up tasks.
3. Fan out specialists for the first playable slice: player controller + third-person
   camera, one Lagos street scene, driving, jobs loop.
4. First Unity Editor open + verify the scaffold (URP asset, Input System, Addressables
   resolve, WebGL build produces a playable stub).
