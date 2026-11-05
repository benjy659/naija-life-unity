# Naija Streets (Unity) — TASKS (the assignment board)

> **TEAM INBOX:** both teams read [docs/team-inbox.md](docs/team-inbox.md)
> first, every run, and answer OPEN items addressed to them the same run.

> **CLAUDE — START HERE:** read [docs/CLAUDE-BRIEFING.md](docs/CLAUDE-BRIEFING.md)
> first — it is your onboarding for this repo (owner decision, frozen browser
> state, migration plan summary, your first tasks). Then read
> [docs/MIGRATION-PLAN.md](docs/MIGRATION-PLAN.md) and
> [docs/DESIGN-INVENTORY.md](docs/DESIGN-INVENTORY.md) — they are the spec
> sources. You write the full detailed specs for every screen, flow, and
> system; own the game economy and logic; place all buildings; review and
> fix everything Ted's team delivers. Ted's team owns backend/DB/migrations,
> builds all 3D models and generations to your specs, and takes small fixes
> you send over. Specs live in `docs/specs/<feature>.md` — no spec, no build.
> Nothing reaches the owner unverified.
>
> **START HERE, Ted's team:** the migration plan is the engineering
> handbook for this repo until Claude writes a Unity one:
> [docs/MIGRATION-PLAN.md](docs/MIGRATION-PLAN.md) (engine, pipeline,
> multiplayer, milestones M0–M6, verification rules). Work from it when
> Claude is not available.
>
> **GAME NAME (owner decision Oct 9, 2026, carried over):** the game is
> **"Naija Streets"** in all player-facing copy — an existing browser game
> called "NaijaLifeGame" forced the rebrand. Repo name stays
> `naija-life-unity`; internal identifiers stay `NL`/`naija-life-*`
> (Supabase project name unchanged).

> **OWNER DECISION Nov 5, 2026 — BROWSER VERSION FROZEN:** the HTML/JS
> version (`benjy659/naija-life`) is STOPPED. Live site stays on its Nov 1
> build (LOAD_V 20261101f). Local frozen tip:
> `morning-release-20261105-reconciled` af42cd56 (LOAD_V 20261105h),
> verified GO but never pushed. The browser repo is READ-ONLY reference —
> do not build on it, do not push it, do not restart its crews (crons
> disabled). No code carries over. Only the design carries over.

**Engine (locked day one):** Unity **6.3 LTS, patch `6000.3.28f1`**; **URP 17.3,
Forward renderer, Linear**; **WebGL first** (browser play, like the old
version), mobile later. **Multiplayer (recommended, pending your review):
Photon Fusion 2, Shared Mode at launch + Supabase backend** (project
`naija-life`, ref `jdumcxvqjtgnbeuvlufq`). Supabase Realtime is NOT the
movement layer. A thin netcode abstraction layer is mandatory before any
gameplay netcode is written. **Launch cost $0** (Fusion 100 CCU free +
Supabase Free). Nothing is spent without the owner's explicit yes on exact
terms.

**Launch target (carried over):** Lagos + Abuja playable at minimum; Port
Harcourt first city after Lagos. Multiplayer at launch. Quality first over
quantity. **No launch date yet** — do not promise one until M6 is played.

**How this board works:** Claude writes specs, assigns, verifies and fixes.
Ted's team builds what is written here. Ted's team checks this file at
~6 AM, ~12 PM, ~6 PM, ~10 PM Winnipeg time. Claude reviews about 90
minutes after each check-in. Main branch only. Never mint GitHub tokens —
pushes via the vault-surrogate `git_push_api.py` (Ted's team). Never
force-push.

---

## M1 — Player controller + third-person camera (TOP PRIORITY)

- **M1.1. Player controller (Ted's team).** Status: ASSIGNED (Phase 2).
  CharacterController + 1D speed-blend locomotion (idle/walk/jog/run/sprint;
  walk <2.6 m/s, default jog 4.2 m/s); camera-relative movement; Input
  System action maps (touch joystick + keyboard/mouse). Spec source:
  `docs/DESIGN-INVENTORY.md` §2 (player movement, foot-slide <15% of body
  speed, stride grows with speed) and `docs/MIGRATION-PLAN.md` §6 M1.
  *Acceptance:* walk/run in a greybox; no foot sliding; verified at phone
  width and desktop.
- **M1.2. Third-person camera (Ted's team).** Status: ASSIGNED (Phase 2).
  Cinemachine damped follow LOCKED behind the player, collision pull-in/fade
  so a building/wall can never hide the player; no sudden swings, orbit
  drift, or automatic angle changes; bias-corrected follow + dead zone so
  strafing/backpedalling never swings the lens. **Owner hard rule — this is
  the rule the browser version kept failing.** Spec source: design
  inventory §2 (camera) and migration plan §6 M1.
  *Acceptance:* camera never swings, drifts, or hides the player, walking;
  verified at phone width and desktop, in Editor play AND WebGL build.
- **M1.3. M1 review (Claude).** Status: CODE REVIEW DONE 2026-11-05,
  PLAY TEST WAITING on a Unity Editor. Full review:
  [docs/reviews/m1-review.md](docs/reviews/m1-review.md). Claude fixed two
  findings on branch `claude/project-thread-op75cq`: R1 the camera circled
  the player while strafing or backpedalling (dead zone compared travel with
  the body's facing; now compares with the camera heading, maths check in
  `tools/camsim.js`), and R2 the keyboard ran at 6.5 m/s by default instead
  of the 4.2 m/s jog. Both UNVERIFIED until played. Ted's fixes: M0.1,
  M1.4 to M1.6 below.
- **M0.1. Commit the locked project settings (Ted's team).** Status: READY.
  From review R4/R5. Open the project once in 6000.3.28f1 and commit
  `ProjectSettings/` with: a URP 17.3 asset + Forward renderer in
  `Assets/Settings/` assigned in Graphics and every Quality level; three
  quality levels Low / Medium / High (WebGL default Medium); Colour Space
  Linear; WebGL compression Brotli; `Greybox_M1.unity` in the build scene
  list. Add `com.unity.ugui` to `Packages/manifest.json` (the On-Screen
  Stick needs it) and drop `com.unity.textmeshpro` if Unity 6 reports it as
  merged into uGUI. *Acceptance:* a fresh clone opens with URP active and no
  pink materials; screenshot of Project Settings > Graphics and Player >
  Other (colour space) in `qa/m0.1/`.
- **M1.4. Wire camera orbit input (Ted's team).** Status: READY. From review
  R3: `PlayerInput.LookInput` is never passed to `PlayerCamera.AddOrbitInput`,
  so the player cannot turn the camera. Rules: desktop orbits only while the
  right mouse button is held (a free-moving mouse must never move the lens);
  phone orbits by dragging on the right half of the screen outside the
  joystick and buttons; gamepad right stick orbits. Scale pointer deltas as
  degrees per pixel (start 0.15) and the stick as degrees per second (start
  120), so the two do not share `orbitSensitivity`. Keep the 2 s
  `orbitRecenterDelay`. *Acceptance:* in Editor and WebGL, RMB-drag turns
  the camera, plain mouse movement does not, and releasing and walking
  forward eases the lens back behind the player within about 2 s.
- **M1.5. Tighten the grounded check (Ted's team).** Status: READY. From
  review R6: in `PlayerController.UpdateGrounded` count the probe as grounded
  only when the ground is within about 0.05 m of the capsule bottom.
  *Acceptance:* stepping off `Wall_Low` falls with gravity (no slow float);
  walking down `Ramp_20deg` never flickers airborne.
- **M1.6. Touch joystick (Ted's team).** Status: READY after M0.1. Add an
  On-Screen Stick (bottom-left, 140 px base, thumb returns to centre) bound
  to `<Gamepad>/leftStick` so it feeds the existing Move action, plus a
  sprint button bottom-right. Nothing else on screen in M1. *Acceptance:*
  phone width 390x844 in WebGL: light tilt walks, full tilt jogs, the stick
  never covers the player.

## M2 — One Lagos street scene (Oshodi greybox)

- **M2.1. Oshodi greybox (Ted's team).** Status: TODO (starts once M1 is
  verified). Road, kerbs + pavements both sides, danfo park, footbridge,
  props; day/night lighting; URP quality tiers; Addressables streaming
  stub. Spec source: design inventory §1 (Oshodi identity) and migration
  plan §6 M2. *Acceptance:* reads as Oshodi; 60 fps target on mid-range;
  <100 draw calls in view; no frame >200 ms entering the district.

## M3 — Driving

- **M3.1. Vehicle controller (Ted's team).** Status: TODO.
  WheelCollider arcade controller (danfo + one car, per-vehicle
  ScriptableObject), enter/exit any parked drivable, chase camera under the
  same rules as on foot, damage model stub. Spec source: design inventory
  §4 and migration plan §6 M3. *Acceptance:* drive the Oshodi loop,
  enter/exit cleanly, no camera swing, no regressions to walking.

## M4 — Jobs loop

- **M4.1. Job-slot system + danfo conductor end-to-end (Ted's team).**
  Status: TODO. Job-slot manager (ScriptableObject slot registry + bot AI
  handover: walk up, one prompt, bot steps aside and waits, walks back
  when you let go); one job at a time; pay via server-verified RPC stub;
  Pawa ≥25 gate; 3-shifts/day cap. Spec source: design inventory §6 and
  migration plan §6 M4. *Acceptance:* take over from a bot, do the work,
  get paid, bot returns — all visible in-game.

## M5 — Multiplayer smoke test

- **M5.1. Fusion room + presence + chat (Ted's team).** Status: TODO.
  Fusion room per district, 2+ players see each other move, presence,
  street-card lookup, text chat (lobby + DM), Supabase Auth + profile.
  Requires the netcode abstraction layer first (migration plan §4).
  *Acceptance:* two clients in one room, movement syncs, chat works, no
  fake players.
- **M5.2. Multiplayer architecture review (Claude).** Status: DONE
  (Claude, verdict below). Review the Photon Fusion 2 Shared Mode + Supabase decision
  against the design inventory §8 (cells, 200 m, send rates, interpolation,
  identity rules) and the migration plan §4 (district sharding, transport
  swap, anti-cheat). Confirm or replace with a better option — with
  reasons, on this board. Supabase Realtime as movement is rejected;
  defend or overturn that with maths.
  **Verdict (Claude, 2026-11-05): CONFIRMED, with three additions.**
  Fusion 2 Shared Mode + Supabase stays. Reasons: it runs over WebSocket
  in WebGL, launch cost is $0 (100 CCU free), and Shared Mode becomes Server
  Mode in the same code. Mirror needs us to run and pay for servers from day
  one; NGO + Relay caps at 100 per session. Supabase Realtime for movement
  stays rejected: 50 players in one district sending 2 to 4 updates a second
  is 100 to 200 messages in and, fanned out to the other 49, about 5,000 to
  10,000 deliveries a second, which is far above what Realtime is sold for,
  and it has no tick, prediction or authority (inferred from the browser
  netcode spec maths; re-check against Supabase's live quota page).
  Additions: (1) every player connects to ONE fixed Photon region (EU,
  nearest to Lagos and Abuja) so Nigerian players never land in different
  regions and miss each other; (2) the netcode abstraction layer
  (`INetSession`, `IPlayerReplicator`, `IWorldEvents` in
  `Assets/Scripts/Network/`) is the first M5 task, before any Fusion call;
  (3) Fusion never carries money, NIN, email or date of birth: identity comes
  from the street-card RPC, money only from Supabase RPCs.

## M6 — First playable slice (definition of done)

- **M6.1. Integration + playthrough (Ted's team + Claude verify).**
  Status: TODO. M1–M5 integrated in one build: spawn in Oshodi, walk,
  drive a danfo, complete a conductor shift, see other players, open the
  phone (wallet balance from the server ledger). *Acceptance:* played
  end-to-end in the Unity Editor AND a WebGL build, on desktop and phone
  widths. No regressions on any working feature. Nothing counts as done on
  code review alone.

## After M6 (in priority order, quality first)

Property (buy/live/garage) · B-38 transfers (owner-gated, see below) ·
Abuja · police full loop (chase/catch/arrest/cell/bail/court) · formal
careers · transit timetables · interiors · aviation top tier.

---

## Carried-over design items (from the frozen browser version)

These are Unity design items now — NOT browser work. Do not build them in
the browser repo.

- **B-38. Player-to-player transfers.** Status: BUILD APPROVED, apply
  GATED. Spec approved by Benjamin Nov 2, 2026 for BUILD; applying
  migration 0040 needs his SEPARATE OK; B-27 (profiles) + B-29 (username
  lock) must go live first; Send tab stays hidden until the RPC is live
  and a two-account test passes. Caps: min ₦100 / max ₦500,000; sender
  10 transfers + ₦1,000,000/day; receiver ₦1,000,000/day; sender account
  ≥7 days AND ≥3 shifts/sales. Anti-abuse: no self-transfer, no
  same-device transfer, 24h hold on re-sending received money, pair flag
  over ₦2M/7 days (review, not block). Ledger: two rows in one
  transaction, idempotent via `client_ref`. Authority: the browser
  `docs/transfer-spec.md` — it supersedes earlier drafts. Unity work:
  re-implement the RPC contracts in shape (names, caps, error codes:
  `insufficient`, `not_found`, `limit`, `self`, `same_device`,
  `too_small`, `too_much`, `too_new`). Do NOT assume B-38 is approved
  for apply.
- **B-32. Crew chat (migration 0038).** Status: TODO (Unity design).
  Browser migration 0038 was written, never applied. Carry the chat
  design (crew channels, moderation, ephemeral pair DMs, device-local
  logs with disclosure) into the Unity chat service. No voice chat.
- **B-33. Signup and account-abuse risk scoring — design first.**
  Status: TODO (Claude). Browser spec `docs/abuse-risk-spec.md` is the
  starting point. One device per account; tiered response; no fake or
  shared users — guests pass the same gates.
- **B-34. Load balancing and scaling readiness.** Status: TODO (Claude).
  Browser spec `docs/scaling-assessment.md` is the starting point.
  Supabase Pro ($25/mo, spend cap off) — direction confirmed; actual
  purchase needs the owner's yes on exact terms. Fusion CCU tiers:
  100 free → $95/yr (200) → $125/mo (500) → $250/mo (1,000) →
  $500/mo (2,000). Paid tiers only when CCU proves it.
- **B-35. Open-world streaming and LOD — audit and design.** Status:
  TODO (Claude). Browser spec `docs/streaming-lod-spec.md` is the
  starting point. Unity mapping: Addressables + scene streaming
  (districts as tiles, wake within 600 m of edge; one city loaded at a
  time), LOD groups, occlusion culling, budget monitor. Budgets: landmark
  ≤50k tris, filler ≤8k, ≤350 draw calls per district, 30 fps at
  390×844, no frame >200 ms entering a district.
- **Supabase grants (Oct 30, 2026 change).** Status: TODO (Ted's team).
  New public tables get NO automatic Data API grants — every future
  migration that creates tables must include explicit GRANTs. Brief:
  browser goal `hidden_files/supabase-grants-oct30-brief.md`. Applies to
  all Unity backend tables (profiles, ledger, property, transfers,
  friends/DMs, area board).

## Owner-gated decisions (do NOT assume)

1. B-38 transfers — build approved; applying migration 0040 needs
   Benjamin's separate OK.
2. Supabase Pro purchase ($25/mo, spend cap off) — direction confirmed;
   actual purchase needs his yes on exact terms.
3. Abuja homes at launch — owner to confirm; only the owner opens
   cities.
4. Property/economy caps — mansion ₦150M is an owner decision;
   re-confirm caps in Unity.
5. Game NPC voices go through the single voice system. Spirited Canyon
   is the BenoBibleChronicles *channel* voice, not the game voice — do
   not conflate.
6. Real money (Flutterwave) = Naira Coins packs + ad space only.
   Nothing else without his explicit yes on exact terms.
7. "Real people only, no real politics" — no fake NPC players/votes/
   counts; no real party names, politicians, party colours/logos,
   cult/confraternity names anywhere.
8. No voice chat; no teleport (cinematic flyover/journey-home sequences
   are travel, not teleport — and you travel home yourself).
9. MudWorks is a separate, stopped project — nothing from it carries
   into Naija Life.

## Verification rules (owner-mandated, carried over)

- No fix counts as done on code review or static analysis alone. Before
  any milestone is called done, the builder must SEE it working: play it
  in the Editor and in a WebGL build, on phone and laptop widths.
- If it cannot be verified visually/behaviorally, report it as
  UNVERIFIED, never as done.
- Real-device checks (iPhone/Android) remain the gate for phone claims;
  emulation alone never counts as DONE.
- UX checklist on every slice: easy to control? Nothing blocking the
  screen? Possible in real life (real Lagos)? No notification spam?
  Could it be better — and if yes, do it now, not in a rebuild.
