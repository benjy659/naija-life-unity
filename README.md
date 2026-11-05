# Naija Life — Unity Rebuild

GTA-style open-world Lagos (+ Abuja) MMO, rebuilt from scratch in Unity.
Replaces the frozen browser/HTML-JS version (`benjy659/naija-life` — frozen Nov 5, 2026;
live site stays on its Nov 1 build). Same game plan, same ideas — new engine.

## Engine

- **Unity 6.3 LTS, patch `6000.3.28f1`** — pinned; every team member installs this exact
  patch via Unity Hub. Do not float on "latest 6.3". Re-evaluate a move to 6.7 LTS only
  after it ships (expected Dec 2026) and gets its first patches.
- **Render pipeline: URP 17.3, Forward renderer**, Linear color space, from the
  Universal 3D template. HDRP cannot target WebGL; Built-in is deprecated in Unity 6.5.
- **Build target first: WebGL** (play in the browser, like the old version). Mobile later.
- **Input:** Input System only (touch joysticks + keyboard/mouse/gamepad in one action-map
  system). Never the old Input Manager for new code.
- **Camera:** Cinemachine — steady chase camera, damped follow, collision-aware,
  locked behind the player (owner hard rule).
- **Content streaming:** Addressables + remote CDN. Ban `Resources.Load` for game content.
  Initial compressed download budget: under ~50 MB; stream Lagos/Abuja districts,
  interiors, and vehicles after first paint.
- **Multiplayer (recommended by the multiplayer workstream, pending tech-lead
  confirmation): Photon Fusion 2, Shared Mode at launch** — one Fusion room per
  district/instance (50–100 players per room; up to 200), Supabase as the persistent
  backend (Auth, profiles, ledger, transfers, property, friends/DMs) via REST +
  Edge Functions. All money validated server-side in Supabase; Fusion never decides
  ownership or balances. Fusion is added via Photon Hub/Asset Store at project setup,
  not via this manifest. Launch cost $0 (Fusion 100 CCU free + Supabase Free).
  Supabase Realtime is NOT the movement layer — the message-rate maths fails at
  ~50 players. A thin netcode abstraction layer (session service, player-replication
  interface, world-event bus) is mandatory before any gameplay netcode is written,
  so the transport stays swappable (Shared → Server Mode is a mode flip, not a
  rewrite). No gameplay code may call the netcode SDK directly.

## Owner rules (carry over from the browser version)

- Quality first over quantity. No regressions on working features.
- Steady camera locked behind the player; nothing blocking the screen.
- Easy controls on phone and laptop.
- Police rules (B-39): a hit requires REAL body contact — near miss is not a crime;
  no arrest from a counter alone — arrest only after police chase + catch;
  children never count; a genuine hit earns a star + fine.
- Property: buy and actually live in homes (Ikoyi/Lekki/VI), garage showing all owned
  cars, mansion + private jet/helicopter top tier, house customization.
- Jobs: danfo driving, vending, deliveries.
- Multiplayer at launch. Architect netcode for a transport swap to dedicated regional
  game servers later without rewrite.
- Never claim a build is verified on code review alone — every slice must be seen
  running (Unity Editor / WebGL build) before it counts as done.
- Main branch only. Never mint GitHub tokens; pushes go through the vault-surrogate
  `git_push_api.py`. Never spend money or buy assets without the owner's explicit yes
  on exact terms.

## Opening the project

1. Install **Unity 6000.3.28f1** via Unity Hub.
2. Open this folder as a Unity project. The Editor generates `Library/`, `Temp/`,
   and the URP settings assets on first open.
3. Create the URP asset + renderer from the Universal 3D template settings if not
   present, and set the project to Linear color space.

> Status: scaffold only — folder structure, `.gitignore`, pinned package manifest,
> and docs. No scenes or scripts yet. The scaffold was created without a Unity
> Editor on this machine, so first open in the Editor is still UNVERIFIED.

## Layout

- `Assets/Scenes/` — scenes (Main, districts, interiors)
- `Assets/Scripts/` — `Core`, `Player`, `Vehicles`, `World`, `UI`, `Network`,
  `Economy`, `Jobs`, `Property`, `Audio`
- `Assets/Prefabs/` — `Player`, `Vehicles`, `World`, `UI`, `Property`
- `Assets/Art/` — `Models`, `Materials`, `Textures`, `Animations`
- `Assets/Audio/` — `Music`, `SFX`, `Voice`
- `Assets/UI/` — `Fonts`, `Sprites`
- `Assets/Addressables/` — Addressables content groups
- `Assets/Settings/` — URP/render settings assets (created in Editor)
- `docs/` — migration plan, task list, specs

## Docs

- `docs/MIGRATION-PLAN.md` — migration plan (design carry-over, engine/pipeline,
  multiplayer, milestones to first playable slice)
- `TASKS.md` — task board for Claude (tech lead) and the crew (added in Phase 2)
