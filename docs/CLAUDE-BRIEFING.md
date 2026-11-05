# Claude Briefing — Naija Streets, Unity Rebuild

**For:** Claude (Anthropic), tech lead of Naija Streets.
**Date:** Nov 5, 2026. **From:** Ted's team (Phase 2 lead).
**Read this first, then** `TASKS.md` (the assignment board),
`docs/MIGRATION-PLAN.md` (engine/pipeline/multiplayer/milestones) and
`docs/DESIGN-INVENTORY.md` (the full design record carried over from the
browser version).

---

## 1. The owner's decision (Nov 5, 2026)

Benjamin stopped the browser/HTML-JS version and ordered a rebuild in
Unity. Same game plan, same ideas — new engine. His words: Unity "has
more features and looks more real", and he knows it is a big migration.
The Nov 15 browser launch is OFF. There is no Unity launch date yet —
do not promise one until M6 (first playable slice) is played.

## 2. Migration plan summary

- **Engine:** Unity **6.3 LTS, patch `6000.3.28f1`** (pinned; everyone
  installs this exact patch via Unity Hub). Support to Dec 2027.
- **Render pipeline:** **URP 17.3, Forward renderer, Linear color space**,
  Universal 3D template. HDRP cannot target WebGL; Built-in was
  deprecated in Unity 6.5. Pipeline choice is locked day one — switching
  later is a rewrite. Low/Medium/High quality tiers from day one.
- **Build target:** **WebGL first** (play in the browser, like the old
  version), mobile later. Budgets: initial compressed download under
  ~50 MB; Addressables + CDN streaming; 400–600 MB practical memory
  target on iOS; single-threaded; WebSocket-only networking.
- **Multiplayer (recommended by the Phase 1 workstream, pending your
  review): Photon Fusion 2, Shared Mode at launch + Supabase backend.**
  One Fusion room per district/instance (50–100 players, up to 200).
  Supabase (project `naija-life`, ref `jdumcxvqjtgnbeuvlufq`,
  ca-central-1) keeps Auth, profiles, ledger, transfers, property,
  friends/DMs via REST + Edge Functions; all money validated server-side.
  **Supabase Realtime as movement is rejected** (message-rate maths fails
  at ~50 players on Pro; no tick/prediction/authority). A thin netcode
  abstraction layer is mandatory before any gameplay netcode is written
  (Shared → Server Mode then becomes a mode flip, not a rewrite).
  **Launch cost $0** (Fusion 100 CCU free + Supabase Free). Paid tiers
  only when CCU proves it; Supabase Pro ($25/mo) still needs the owner's
  yes on exact terms.
- **Milestones to first playable slice:** M0 scaffold (done) → M1 player
  controller + steady third-person camera → M2 one Lagos street scene
  (Oshodi greybox) → M3 driving → M4 jobs loop (danfo conductor
  end-to-end) → M5 multiplayer smoke test → M6 first playable slice
  (spawn in Oshodi, walk, drive, do a shift, see other players, phone
  wallet from server ledger — played end-to-end in Editor + WebGL build,
  phone and desktop widths). After M6: property, B-38 (owner-gated),
  Abuja, full police loop, careers, transit, interiors, aviation.
- **Game name:** "Naija Streets" in all player-facing copy (owner
  rebrand, Oct 9, 2026 — an existing "NaijaLifeGame" forced it). Repo
  stays `naija-life-unity`; internal identifiers stay `NL`/`naija-life-*`.
- **What carries over:** the design only — Lagos districts + Abuja
  landmarks L1–L19, B-39 police rules, property tiers (mansion ₦150M),
  job-slot system, economy three laws + hash-chained server ledger, B-38
  transfer caps, crews/friends/chat (no voice chat), phone/HUD rules,
  brand palette. **What does NOT carry over:** all `js/` (Three.js,
  bundle builder, LOAD_V versioning), localStorage caches, the quiz.html
  client start grant, Supabase Realtime as movement, the browser QA
  harness, CSS brand tokens, and known browser bugs (phantom-hit radius,
  counter-based jail, client money minting).

## 3. Frozen browser state (reference only)

- **Live site:** https://benjy659.github.io/naija-life/index.html — still
  on the Nov 1 build (LOAD_V 20261101f, remote main `26f5a06e`).
  Playable, unchanged.
- **Local frozen tip:** branch `morning-release-20261105-reconciled`,
  commit `af42cd56` (LOAD_V 20261105h) — MKICO fix verifier gave GO for
  push, but the push was BLOCKED (remote main moved) and never pushed.
  Local repo: `~/workspace/naija-life` (read-only reference on this
  machine).
- **Crews stopped:** morning/evening crew crons + hourly crew watch
  DISABLED Nov 5. Do not restart them. Do not build on or push the
  browser version.
- **Frozen-state caveats (never verified at freeze, so verify fresh in
  Unity):** night lighting, helipad lights/prompts, Makoko gate re-fire,
  Banex/Garki drive-throughs, phone HUD overlap, wallet changes,
  rush-hour evidence, phone-width check, phone FAB tap, duplex interiors
  (camera), church/school cards, B-39 live-car iPhone drive, Abuja
  street-life/night screenshots, Wuse Market night frames.

## 4. Your first tasks

1. **Review the migration plan + design inventory** (`docs/MIGRATION-
   PLAN.md`, `docs/DESIGN-INVENTORY.md`). Confirm the engine/pipeline/
   multiplayer decisions or replace them with better ones — with reasons,
   on `TASKS.md`. In particular: the Photon Fusion 2 Shared Mode +
   Supabase recommendation vs your own multiplayer architecture (the
   browser netcode spec said Supabase Realtime day one + transport swap;
   the Unity workstream rejects Realtime for movement — adjudicate).
2. **M1 review — player controller + third-person camera.** Ted's team
   is building it now (CharacterController, 1D blend locomotion,
   Cinemachine damped follow locked behind the player). Review against
   design inventory §2 and the owner camera hard rule (one steady damped
   follow angle, no swings/drift, occlusion pull-in so a building can
   never hide the player). Play it at phone + desktop widths when the
   build lands; send fixes back on `TASKS.md`.
3. **Multiplayer architecture review** (TASKS.md M5.2). Same as (1) but
   as a formal verdict: Fusion vs Mirror vs NGO+Relay, with the
   district-sharding, send-rate, interpolation, and identity rules from
   design inventory §8 carried over.
4. **Write specs the way you did for the browser version** —
   `docs/specs/<feature>.md`, no spec no build — starting with the M2
   Oshodi greybox spec when M1 is verified. Assign on `TASKS.md`; Ted's
   team builds what is written there.

## 5. Browser repo is FROZEN — no new tasks there

Do not write or assign browser tasks. Your open browser items carry over
as **Unity design items only** (listed on `TASKS.md`):

- **B-38 hold** — transfer spec approved for BUILD; applying migration
  0040 needs Benjamin's SEPARATE OK; B-27 (profiles) + B-29 (username
  lock) must go live first; Send tab stays hidden until the RPC is live
  and a two-account test passes. Do not assume B-38 is approved.
- **B-32 / migration 0038** — crew chat, written never applied. Carry
  the chat design into the Unity chat service.
- **B-33** — signup and account-abuse risk scoring, design first
  (browser `docs/abuse-risk-spec.md` is the starting point).
- **B-34** — load balancing and scaling readiness (browser
  `docs/scaling-assessment.md` is the starting point).
- **B-35** — open-world streaming and LOD, audit and design (browser
  `docs/streaming-lod-spec.md` is the starting point).
- **Supabase grants brief (Oct 30, 2026)** — new public tables get NO
  automatic Data API grants; every future migration creating tables must
  include explicit GRANTs. Brief: browser goal
  `hidden_files/supabase-grants-oct30-brief.md`.

## 6. How we work (unchanged from the browser version)

- You are tech lead: you write specs, assign, verify and fix. Ted's
  team builds to your specs, owns backend/DB/migrations, builds all 3D
  models and generations, and pays for ALL AI generations and model
  resources. Your branch work merges to main.
- **Main branch only. Never mint GitHub tokens** — pushes go through
  Ted's team's vault-surrogate `git_push_api.py`. Never force-push.
- **Verification rule (owner-mandated):** nothing counts as done on code
  review or static analysis alone. Every milestone must be SEEN running
  (Editor play + WebGL build, phone and desktop widths) before it counts.
  If it cannot be verified visually/behaviorally, report it as
  UNVERIFIED, never as done.
- **Owner rules carried over:** quality first over quantity; steady
  camera locked behind the player; nothing blocking the screen; easy
  controls on phone + laptop; no regressions on working features; B-39
  police rules (real body contact only, no arrest from a counter, chase
  + catch only, children never count); no voice chat; no teleport;
  "real people only, no real politics"; never spend money or buy assets
  without the owner's explicit yes on exact terms.
