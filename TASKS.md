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

**See first, say first, then build (Benjamin, 2026-11-06):** before
building anything, every team pulls main, reads the new commits since its
last run plus the inbox and this board, then writes a claim (task id + files)
under "Claims" in [docs/team-inbox.md](docs/team-inbox.md) and checks that no
one else is on the same task or files. Ted's team has been building on its
own; never redo or overwrite their work.

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
- **M0.1. Commit the locked project settings (Ted's team).** Status: DONE 44d8428 + 2935a48 (read back by Claude 2026-11-06: URP asset in Graphics and all 3 Quality levels, Linear, Greybox_M1 in build scenes, ugui added).
  **Checked 2026-11-06 on e58209c:** still needed. The first Editor open
  committed defaults: `GraphicsSettings.m_CustomRenderPipeline` and every
  Quality level's `customRenderPipeline` are `{fileID: 0}` (no URP asset,
  so the scene renders with the built-in fallback), `m_ActiveColorSpace: 0`
  (Gamma, not Linear), and `EditorBuildSettings` lists no scenes.
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
- **M1.4. Wire camera orbit input (Claude coded 2026-11-06; Ted runs the harness, inbox IN-13).** Status: REVIEW, UNVERIFIED. From review
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
- **M1.5. Tighten the grounded check (Claude coded 2026-11-06; Ted runs the harness, inbox IN-13).** Status: REVIEW, UNVERIFIED. From
  review R6: in `PlayerController.UpdateGrounded` count the probe as grounded
  only when the ground is within about 0.05 m of the capsule bottom.
  *Acceptance:* stepping off `Wall_Low` falls with gravity (no slow float);
  walking down `Ramp_20deg` never flickers airborne.
- **M1.6. Touch joystick (Ted's team).** Status: READY after M0.1. Add an
  On-Screen Stick (bottom-left, 140 px base, thumb returns to centre) bound
  to `<Gamepad>/leftStick` so it feeds the existing Move action, plus a
  sprint button bottom-right. Nothing else on screen in M1. *Acceptance:*
  phone width 390x844 in WebGL: light tilt walks, full tilt jogs, the stick
  never covers the player. Note (Claude, 2026-11-05): this stock uGUI stick is
  a stopgap for M1 testing only. HUD-2 replaces it with the UI Toolkit
  floating stick (124 px base, per design inventory §9); keep the binding to
  `<Gamepad>/leftStick` so the swap needs no input changes.

## M2 — One Lagos street scene (Oshodi greybox)

Spec: [docs/specs/oshodi-greybox.md](docs/specs/oshodi-greybox.md) (Claude,
2026-11-05). Every position comes from
`docs/specs/oshodi-greybox-layout.json`, made and rule-checked by
`node tools/oshodi/oshodi-layout.js` (spec section 0). Never place or move
anything only in the scene; change the script, run it, commit the JSON with it.
Evidence for every M2 task goes under `qa/M2.<n>/` (spec section 11).

- **M2.1. Oshodi greybox scene (Ted's team runs it, Claude wrote it).**
  Status: READY (M0.1 is in). Claude wrote the builder and tests on
  2026-11-06; they have never been compiled or run (no Unity in the cloud),
  so they are UNVERIFIED. Ted's team: (1) open the project, fix any compile
  error and tell Claude what it was (inbox); (2) run Naija Streets > Build
  Oshodi greybox, or batch mode `-executeMethod
  NaijaLife.World.Editor.OshodiGreyboxBuilder.BuildAllBatch`; (3) run the
  EditMode tests in `NaijaLife.World.Tests` (LanesAreClear,
  PavementsAreWalkable, FootbridgeIsWalkable, SceneMatchesLayout); (4) commit
  the two scenes under `Assets/Scenes/Districts/`, the materials under
  `Assets/Art/Materials/Greybox/` and any .meta files Unity made; (5) play
  `District_Oshodi_Core` and post the evidence. Files: `Assets/Editor/World/`
  (builder, data classes), `Assets/Tests/EditMode/World/`,
  `Assets/Data/Oshodi/oshodi-greybox-build.json` (made by
  `node tools/oshodi/oshodi-layout.js`; never edit it by hand). *Acceptance:*
  the script says PASS, all four tests pass (results XML in qa/M2.1/), V1 to
  V5 screenshots at 390x844 and 1280x800 read every noun in spec section 1,
  a 30 s clip climbing and crossing the footbridge at phone width, under 100
  draw calls at every viewpoint.
- **M2.2. Lighting, time of day, quality tiers.** Status: code Claude, IN
  PROGRESS (2026-11-06); Ted's team runs it and posts evidence after M2.1. TimeOfDay on real WAT with QA presets, sun and fog, lamp light
  pool, night emissives, skyline shell, the Low/Medium/High table (spec
  section 6). *Acceptance:* V1 and V6 at all three tiers; night readable at
  390x844; draw calls still under 100.
- **M2.3. Streaming stub.** Status: code Claude, IN PROGRESS (2026-11-06);
  Ted's team runs it and posts evidence after M2.1.
  Bootstrap scene, Addressables group `District_Oshodi`, DistrictTile +
  DistrictStreamer (wake 600 m, sleep 700 m), FrameSpikeLogger (spec
  section 7). *Acceptance:* walk from the streaming test start (z -790)
  north until the tile loads and back south until it unloads; overlay shows
  no frame over 200 ms, in a WebGL build.
- **M2.4. M2 review (Claude).** Status: WAITING on M2.1 evidence. Claude
  checks every qa/M2.n/ file against the spec, fixes what is wrong, and only
  then marks M2 DONE. Real-phone feel (30 fps at 390x844) is Benjamin's check.

## M3 — Driving

Spec: [docs/specs/vehicles-m3.md](docs/specs/vehicles-m3.md) (Claude,
2026-11-05). Build in the order below; each task has its own `qa/<task id>/`
folder with screenshots or a clip, a WebGL check and a `REPORT.md`. M3 does
not wait for M2: build in a new `Greybox_M3.unity` (spec §2) and repeat the
acceptance on Oshodi when M2 lands. Start after M0.1 (URP, ugui).

- **M3.C. Driving camera rule (Claude).** Status: CODE DONE 2026-11-05,
  UNVERIFIED (branch `claude/project-thread-o554bj`). The walking dead zone
  left the lens stuck 120 deg sideways after a U-turn
  (`node tools/camsim.js`). `PlayerCamera` now has `UpdateDrivingYaw`
  (re-align only rolling forward, hold when reversing, stopped, airborne or
  sliding), `SetDrivingMode(true, distance, pivot)` and
  `SetFollowTarget(target, snap: false)`. Spec §5.
- **M3.1. Vehicle definitions + controller (Ted's team).** Status: READY
  after M0.1. `VehicleDefinition` ScriptableObject, `DriveInput` struct,
  `VehicleController` (WheelCollider arcade, implements
  `IPlayerCameraTarget`), `Danfo.asset` and `TokunboSaloon.asset` with the
  spec §4 numbers, greybox meshes with separate wheels, layers `Vehicle`,
  `DrivenVehicle`, `Pedestrian`, `Road`, fixed timestep 0.02. Drive it from a
  temporary debug script feeding `DriveInput` from the keyboard.
  *Acceptance:* spec §11 lines 2, 3 (handling only), 5 (no falling through),
  8; danfo never rolls on the flat loop.
- **M3.2. Greybox_M3 test scene (Ted's team).** Status: READY (can run in
  parallel with M3.1). Road loop, crossing, bridge with piers outside the
  lanes, narrow gate, two parked drivables, half-parked obstacle car,
  pedestrians, per spec §2. *Acceptance:* top-down and street-level
  screenshots in `qa/m3.2/`.
- **M3.3. Enter and exit (Ted's team).** Status: READY after M3.1.
  `VehicleSeat`, `DriveSession`, `VehiclePlayerDriver`, `Driving` action map
  (keyboard + gamepad), walked entry, exit-point checks, layer swap and camera
  calls exactly as spec §5.2, §5.3 and §7. *Acceptance:* spec §11 lines 1, 6,
  7.
- **M3.4. Touch driving controls (Ted's team).** Status: READY after M1.6
  and M3.3. Steer slider, gas, brake, handbrake, exit, speedo, per spec §12.
  *Acceptance:* spec §11 line 11 at 390x844 in WebGL; screenshot proving no
  control crosses the centre third.
- **M3.5. Damage stub + contact events (Ted's team).** Status: READY after
  M3.1. `VehicleDamage`, `VehicleContactReporter` (collision events only,
  never a radius), debug overlay. Spec §10. *Acceptance:* clip of hitting a
  wall at 20 and 60 km/h and a pedestrian capsule at 10 and 30 km/h with the
  overlay showing the events.
- **M3.6. Lane graph + TrafficSense + bot drivers (Ted's team).** Status:
  READY after M3.1 and M3.2. `LaneGraph`, `LaneNode`, `TrafficSense`,
  `TrafficDriver`, `TrafficSpawner`, per spec §8 (box-cast sensing at the
  vehicle's own height, dynamic near / kinematic far, crossings, steer round
  parked things, never pass through). *Acceptance:* spec §11 line 9 as a
  3-minute clip with 12 bots, plus frame times in `qa/m3.6/REPORT.md`.
- **M3.7. Lane validator (Ted's team).** Status: READY after M3.6. Editor
  menu Naija > Validate Lanes and a PlayMode test, spec §8.7. *Acceptance:*
  0 hits on Greybox_M3, and the test fails when a cube is dropped in a lane.
- **M3.8. M3 review and play check (Claude).** Status: WAITING on M3.1 to
  M3.7. Claude reviews each commit as it lands and checks every `qa/m3.*`
  folder against spec §11.

## M4 — Jobs loop

Spec: [docs/specs/jobs-m4.md](docs/specs/jobs-m4.md) (Claude, 2026-11-05).
Pacing check: `node tools/jobsim.js`. Build in this order; each task needs its
`qa/m4.<n>/` evidence (spec section 11) before it counts. Uses the
`LocalShiftStub` until M4.4 is applied, so M4.1 to M4.3 do not wait on the
backend.

- **M4.1. Job-slot system + bot handover (Ted's team).** Status: READY (after
  M0.1 and M1 play). `JobSlot`, `JobSlotRegistry` (ScriptableObject),
  `BotWorker` (Working, SteppingAside, WaitingAside, WalkingBack; NavMeshAgent,
  never teleports), `PlayerJobs` (one job at a time, release triggers spec
  4.3, one prompt), `IJobSlotSync` + `LocalJobSlotSync`. Spec sections 3 to 5.
  *Acceptance:* spec 11 items 2 and 7 in Editor and WebGL at 390x844 and
  1280x800.
- **M4.2. Danfo conductor shift, client (Ted's team).** Status: READY after
  M4.1, needs the M2 bays + route and the M3 danfo prefab. `ConductorShift`,
  `ShoutMinigame`, `ChangeMinigame`, `DanfoRoute` (kinematic), `PassengerQueue`,
  shift chip and summary card, bot trips with no player. Spec sections 6 and
  10. *Acceptance:* spec 11 items 1, 3, 4, 5 and 8.
- **M4.3. Pawa + shift service client (Ted's team).** Status: READY.
  `PlayerNeeds` (Pawa gate 25, drain 1.2/min, dev menu), `IShiftService`,
  `LocalShiftStub` (refused in release builds), `SupabaseShiftService`,
  `WalletCache.AdoptServerBalance`. Spec sections 7 and 8. *Acceptance:* spec
  11 item 6; the wallet changes only from a server or stub reply.
- **M4.4. Shift RPCs migration (Ted's team, owns and applies).** Status:
  READY. Write the migration from spec section 9 (`nl_job_shifts`,
  `nl_job_trips`, `nl_shift_start`, `nl_shift_trip`, `nl_shift_end`, server
  only ledger source `job_shift`, explicit GRANTs). Post the SQL for Claude's
  review before applying. *Acceptance:* spec 11 item 9.
- **M4.5. M4 review (Claude).** Status: TODO. Review M4.1 to M4.4 against the
  spec and the qa evidence; nothing counts as done on code review alone.

## M5 — Multiplayer smoke test

Spec: [docs/specs/netcode-m5.md](docs/specs/netcode-m5.md) (Claude,
2026-11-05). Guard before every push touching Assets/:
`tools/check-net-boundary.sh` (only `Network/Fusion/` may use Fusion) and
`node tools/netsim.js` (maths check).

- **M5.0. Netcode abstraction layer (Claude).** Status: REVIEW, UNVERIFIED
  in Unity. `INetSession`, `IPlayerReplicator`, `IWorldEvents`,
  `IChatService`, `IStreetCardService` + send policy, 20-byte codec,
  snapshot interpolation, 200 m cell interest, 40-nearest, backoff, rate
  limits, offline session in `Assets/Scripts/Network/Core/` (plain C#, no
  Unity or Fusion types). Fusion Shared adapter skeleton in
  `Network/Fusion/` (compiles only with define `NL_FUSION`). Glue in
  `Network/Unity/` (NetBootstrap, NetPlayerBridge, RemotePlayerSpawner,
  SupabaseSession). Edit Mode tests in `Assets/Tests/EditMode/Network/`.
  Maths checked in node (23 checks pass). Compiled in Ted's Editor on
  2026-11-06 (b2ffc61: the only compile error in the project was in
  PlayerCamera); Edit Mode tests not run yet.
- **M5.1. Fusion room + presence + chat (Ted's team).** Status: READY
  (after M0.1). Fusion room per district, 2+ players see each other move, presence,
  street-card lookup, text chat (lobby + DM), Supabase Auth + profile.
  Sub-tasks in spec §7.1: **M5.1a** Fusion 2 SDK + Photon app (free plan,
  region `eu`, define `NL_FUSION`, weave `NaijaLife.Network.Fusion`);
  **M5.1b** Edge Function `photon-auth` + Photon custom auth, anonymous off;
  **M5.1c** fix the Fusion skeleton against the real SDK, Edit Mode tests
  all green; **M5.1d** scene `Net_M5.unity` (bootstrap, bridge, capsule
  remotes with name tags; Claude wrote the builder: run
  `-executeMethod NetM5SceneBuilder.Build`, or `.BuildWebGL`, then paste
  the anon key locally); **M5.1e** lobby chat panel stub; **M5.1f**
  migration `nl_street_card_by_id` (live `nl_street_card` takes a
  username; the network only knows user ids). Drafts by Claude (2026-11-06):
  `supabase/functions/photon-auth/index.ts` (+ `test.mjs`, 6 checks pass)
  and `supabase/migrations/0041_nl_street_card_by_id.sql` (NOT applied). Claude reviews M5.1b and
  M5.1f; M5.1f is applied only with Benjamin's OK.
  *Acceptance:* the 10 checks in spec §7.2, recorded under `qa/M5.1/`
  (Editor + WebGL, two clients): movement syncs, chat works, no fake
  players.
- **M5.4. Players-online count (Claude, IN PROGRESS 2026-11-06).** Code done:
  `Network/Unity/OnlineHeartbeat.cs` touches live `nl_online_touch` every 30 s
  while the game is in front, `nl_heartbeat_leave` on hide/quit; exposes
  `OnlineHeartbeat.Count` (-1 = unknown, show nothing) and `LastUpdatedUtc` for
  the HUD workstream. Ted: verify the count changes with two clients (qa/M5.1/).
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

## HUD and phone (UI workstream)

Spec: [docs/specs/hud-and-phone.md](docs/specs/hud-and-phone.md) (Claude,
2026-11-05). UI Toolkit for all screen UI; brand tokens in
`Assets/UI/Styles/NLTokens.uss`; 99 line icons in `Assets/UI/Icons/`; shield
logo in `Assets/UI/Brand/`. Owner rules: nothing blocks the screen, one
prompt at a time, lean HUD with the minimap, no gold or yellow in UI, no
emoji, motion on every change. **DONE rule for every HUD task:** Ted's team
posts Editor screenshots and a WebGL capture at 390x844, 844x390, 1280x800
and 1920x1080 under `qa/<task id>/`, then Claude reviews. Until then:
UNVERIFIED.

- **HUD-1. UI foundation (Ted's team).** Status: READY after M0.1. Spec §2-4.
  One runtime `UIDocument` + PanelSettings (Constant Pixel Size);
  `NLUIRoot` (mode classes `.nl-portrait`/`.nl-landscape`/`.nl-desktop`,
  safe-area padding); `NLUIScaler` + `NLGetDevicePixelRatio` `.jslib` so
  1 USS px = 1 CSS px; import `NLTokens.uss`; TextCore font assets Inter
  Medium/Bold/ExtraBold with the ₦ glyph (U+20A6); import the 99 icons and
  the shield mark as VectorImage (fallback: SDF textures if strokes break on
  WebGL). *Acceptance:* an `IconSheet` test scene showing all 99 icons
  tinted accent, the logo, and "₦8,450 ₦12.5k ₦1.2bn" in all three weights,
  sharp on a phone in WebGL, captured at the 4 sizes in `qa/hud-1/`.
- **HUD-2. Touch controls (Ted's team).** Status: READY after HUD-1 and M1.4.
  Spec §5.2. Floating `NLTouchStick` (base 124, thumb 52, 12% dead zone)
  feeding `NLStickControl : OnScreenControl` on `<Gamepad>/leftStick`;
  sprint 56 (hold, Settings toggle), camera 60 (cycles views), phone 58;
  look-drag zone outside stick and buttons; touch UI auto hides on
  keyboard/mouse with no flicker. Replaces the M1.6 stock stick.
  *Acceptance:* real phone in WebGL: stick and look drag work at the same
  time with two thumbs; light tilt walks, full tilt jogs; buttons fire on
  press; nothing covers the player; clip in `qa/hud-2/`.
- **HUD-3. On-foot HUD layout (Ted's team).** Status: READY after HUD-1.
  Spec §5.1 and §10. Radar (orthographic RenderTexture camera, 256 px,
  10 Hz, rotates with camera yaw), place and WAT clock pill, vitals strip,
  wallet chip (`NLMoney.Short`) + menu button, right-column rows via
  `NLHudStack` (+0/+52/+100/+148/+196, collapse upward), desktop key-hint
  chip. *Acceptance:* matches the mockups in `docs/specs/img/hud/` at the 4
  sizes; Profiler shows 0 B GC alloc per frame from the HUD.
- **HUD-4. Prompts, toasts and the screen guard (Ted's team).** Status: READY
  after HUD-3. Spec §6-7. `NLPromptManager` (Offer/Withdraw/Accept; kinds
  action/exit/other; 2.5 m show, 3.0 m hide, 0.4 s min swap; "+N" chip and
  chooser sheet), `NLToastManager` (3 live, 2 in portrait, 5 queued, dedupe
  by key within 10 s), `NLScreenGuard` keep-out box with fallbacks, `F9`
  debug overlay. *Acceptance:* walking past 3 offers in a row never
  flickers and never shows two prompts; Top camera view moves the prompt
  off the player; F9 screenshots at the 4 sizes in `qa/hud-4/`.
- **HUD-5. Phone shell (Ted's team).** Status: READY after HUD-1. Spec §8.1-8.2.
  Open/close in portrait (full screen) and landscape/desktop (side panel);
  status bar; home with shield mark, WAT clock, Next card, 4-column app grid
  showing only built apps (Bank, Map, Settings for M6); `NLPhoneNav`
  push/pop with slide motion; Settings with Reduce motion and sprint toggle.
  *Acceptance:* open, push Bank, back, close by every route (button, Esc,
  P, swipe down) at the 4 sizes; clip in `qa/hud-5/`.
- **HUD-6. Bank app, wallet from the ledger (Ted's team).** Status: READY after
  HUD-5 (live data needs M5/M6). Spec §8.3-8.4. Balance card, History and
  This week tabs, rows by ledger source; `IWalletSource` with an
  Editor-only fake source compiled out of player builds; loading (300 ms),
  slow (3 s), timeout (8 s) and empty states; refresh on open, after money
  events, on tap and every 5 min; `NLMoney.Short/Full` with Edit mode
  tests for every example in §8.4. Send tab stays hidden (B-38 gate).
  *Acceptance:* each state captured at 390x844 and 1280x800 in
  `qa/hud-6/`; tests green.
- **HUD-7. HUD QA pass (Ted's team, Claude reviews).** Status: TODO after
  HUD-2 to HUD-6. Spec §5.1, §6.3, §10. All 32 right-column combinations at
  the 4 sizes with no overlap (script it), two-thumb test on a real phone,
  prompt flicker walk, phone open frame time. *Acceptance:* report and
  captures in `qa/hud-7/`.

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
  The "at least 3 shifts or sales" rule must count the new `job_shift`
  ledger source from M4 (docs/specs/jobs-m4.md).
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
  IN PROGRESS (Claude, Oshodi workstream). First Unity step is the M2.3
  streaming stub (docs/specs/oshodi-greybox.md section 7); the full
  streaming and LOD spec follows once M2.3 measurements are in. Browser spec `docs/streaming-lod-spec.md` is the
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

---

## Lessons for Ted's team

- **L-1. Never hand-write Unity YAML.** (2026-11-06, commits b2ffc61,
  e33fa82, e58209c.) The M1 prefab and scene were written as text without an
  Editor. Three bugs came from that: the CharacterController was saved as a
  BoxCollider2D (class 61 instead of 143), the scene's Player instance was an
  empty shell, and the input actions reference was malformed, so the player
  could never move. Create and edit `.prefab`, `.unity`, `.controller` and
  `.asset` files only in the Editor, then commit what the Editor saved.
- **L-2. "Locked on day one" means committed.** (2026-11-06.) Pipeline,
  colour space and build scenes must be committed settings files, checked by
  reading them back, not a step someone does on first open (see M0.1).

## Review log

- **2026-11-06 02:30 UTC, Claude review run.** Reviewed Ted's b2ffc61,
  e33fa82 and e58209c (first Editor open). Good news: the Unity Editor now
  runs on Ted's side, the one compile error (CM3 `AddComponent`) is fixed,
  and three hand-written-YAML bugs that stopped the player moving are fixed
  (lesson L-1). Claude's camera fix R1 survived the merge
  (`moveVsCamera` in PlayerCamera.cs). Security: no secrets in the diff; no
  backend or payment changes. Still open: M0.1 (no URP asset, Gamma colour,
  no build scenes; details under M0.1), inbox IN-4 to IN-11 unanswered, no
  qa/ captures yet, so M1 stays UNVERIFIED.
  Where I stopped: waiting on Ted's M0.1 and the first M1 play captures under
  qa/m1/; next run reviews them and answers any inbox replies.
- **2026-11-06 04:20 UTC, Claude review run.** Reviewed Ted's 44d8428,
  47d5aca, 924e688, 2935a48: M0.1 is done and read back correct; the M1
  harness (Assets/Scripts/Verification, Assets/Editor) checks R1 strafe yaw,
  orbit, occlusion, ramp, collision and follow distance, and Ted reports run 8
  passed, but no log or screenshots are committed yet. Security: no secrets,
  no backend or money changes. New rule 9 in the inbox (see first, say first,
  then build) and a Claims section. Claude coded M1.4 and M1.5 (IN-13). Each
  workstream thread now has its own scheduled run 8 times a day, staggered
  at :07, :17, :37, :47 and :57 past every third hour (Winnipeg).
  Where I stopped: waiting on Ted's compile check and harness run for M1.4
  and M1.5 and the qa/m1/ captures; next run reviews them.
