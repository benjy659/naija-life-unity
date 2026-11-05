# M1.1 — Player Controller (Naija Streets, Unity rebuild)

**Status: UNVERIFIED.** No Unity Editor exists on this VM yet (install in progress,
separate agent). Nothing here has ever run. Do not call M1 done on code review
alone — see the verification checklist below.

Spec: `docs/DESIGN-INVENTORY.md` §2 (Player) and `docs/MIGRATION-PLAN.md` §6 M1.

## Files (owned by M1.1)

| File | What it is |
|---|---|
| `Assets/Scripts/Player/PlayerController.cs` | CharacterController movement, speed bands, slope/grounded handling, Animator `Speed` drive |
| `Assets/Scripts/Player/PlayerInput.cs` | Input System wrapper — one "Gameplay" action map for keyboard/mouse + touch + gamepad |
| `Assets/Scripts/Player/PlayerInput.inputactions` | The action asset: Move / Look / Sprint / Walk |
| `Assets/Prefabs/Player/Player.prefab` | Player prefab — CharacterController + scripts + Animator + capsule placeholder body (primitives only, no paid assets) |
| `Assets/Scenes/Greybox_M1.unity` | Flat 60×60 m greybox ground + 5 primitive obstacles + Player instance + directional light |
| `Assets/Art/Animations/PlayerLocomotion.controller` | Animator Controller — `Speed` float parameter + 1D blend tree (EMPTY, no clips yet) |
| `Assets/Scripts/Player/README_M1.1.md` | This file |

## What is implemented

- **Movement:** CharacterController-based, camera-relative (reads `Camera.main`
  yaw; falls back to the player's own facing when no camera exists yet).
  Speeds — walk 2.4 m/s (< 2.6 per spec), default jog 4.2 m/s, run 6.5 m/s,
  sprint 8.5 m/s. Keyboard WASD/arrows move at jog; hold Shift = sprint,
  hold Left Ctrl or C = walk. Touch joystick tilt scales speed continuously
  (light tilt walks, full tilt jogs/runs). Speed ramps via accel/decel
  (12 / 16 m/s²) — no velocity snaps, so feet stay planted.
- **Grounded check:** `CharacterController.isGrounded` backed by a downward
  sphere probe (also reads the ground normal for slope logic); small drops
  and kerbs do not flicker the grounded state.
- **Slopes:** `slopeLimit` 45°, `stepOffset` 0.3 m; slopes steeper than the
  limit slide the player down instead of letting them stand on them.
- **Facing:** player turns smoothly toward the movement direction
  (720°/s); standing still keeps the last heading.
- **Input:** ONE action map ("Gameplay"), no old Input Manager. Move =
  WASD composite + arrows composite + gamepad left stick (deadzoned);
  Look = mouse delta + gamepad right stick; Sprint = Shift / stick press;
  Walk = Left Ctrl / C. Touch scheme declared in the asset; the mobile
  on-screen joystick (UI work, not yet built) feeds the SAME Move action
  via an Input System On-Screen Stick component — no second move path.
- **Camera interface (M1.2):** `PlayerRoot`, `FollowTarget` (head-height
  child at 1.6 m), `CurrentSpeed`, `IsGrounded`, `Velocity`, plus
  `IPlayerCameraTarget` (`Velocity` / `HorizontalSpeed` / `IsGrounded` /
  `Facing`). **Note the wiring contract:** the M1.2 camera follows the
  player ROOT (it adds its own 1.55 m pivot height and auto-finds the root
  by the "Player" tag). `FollowTarget` is the head-height anchor for other
  consumers — do NOT point the camera at it or the height double-counts.
  No camera code lives in M1.1 files.

## What is stubbed / not yet real

- **Animations — PLACEHOLDER, do not fake it.** No animation clips exist in
  the project yet. `PlayerLocomotion.controller` has the `Speed` float
  parameter and a 1D blend tree with the correct thresholds (0–8.5 m/s),
  but the tree has NO child motions. `PlayerController` drives `Speed`
  from real horizontal speed every frame, so the moment clips are added
  (idle / walk / jog / run / sprint, thresholds ≈ 0 / 2.4 / 4.2 / 6.5 /
  8.5) the blend works with no code change. Until then the player is a
  grey capsule + sphere head and visibly does not animate. Foot-slide
  (< 15% of body speed, stride grows with speed) is an animation-side
  property and CANNOT be verified until clips exist.
- **Touch joystick UI:** the On-Screen Stick component is not built yet
  (UI work). Touch input path is wired at the action level only.
- **URP pipeline asset:** the scaffold has no Universal Render Pipeline
  asset yet (M0 scaffold only). On first Editor open, create one
  (Create → Rendering → URP Asset, or the M2 agent's quality-tier work)
  and assign it in Project Settings → Graphics, or the scene renders
  with the built-in fallback. This is a one-click first-open step, not
  M1.1 code.
- **Scene not in Build Settings:** `Greybox_M1.unity` is not yet in
  File → Build Settings (ProjectSettings/EditorBuildSettings.asset does
  not exist in the scaffold). Add it before any WebGL build test.

## Known first-open risks (for the verifier)

- All `.prefab` / `.unity` / `.controller` / `.inputactions` files were
  hand-authored as YAML/JSON (no Editor on this VM). GUIDs are unique and
  cross-references were checked by script, but Unity may still re-import
  or warn on first open (e.g. `.meta` importer blocks were left minimal
  on purpose so Unity fills the correct importer while keeping the GUID).
  If an asset fails to import, fix the YAML in the Editor and re-save —
  do not delete and recreate the asset (GUIDs are referenced elsewhere).
- Built-in primitive mesh/material references use the standard fileIDs
  (Cube 10207, Capsule 10206, Sphere 10208, Default-Material 10302).

## Verification checklist (verifier: play, don't review)

Run in the Unity Editor AND in a WebGL build, at phone width (390×844)
and desktop (1280×800). Real-device phone check is the gate for phone
claims; emulation alone never counts as DONE.

1. Open `Greybox_M1.unity`, press Play. Player spawns at (0,0,0), visible
   as a capsule, standing on the ground (not floating/sinking).
2. WASD / arrows: player moves camera-relative (W = away from camera),
   turns smoothly toward movement, stops without sliding.
3. Speeds: walk (Ctrl) ≈ 2.4 m/s, jog (WASD) ≈ 4.2 m/s, sprint (Shift)
   ≈ 8.5 m/s. Check with a stopwatch over a known distance or the
   `CurrentSpeed` value in the Inspector.
4. Collisions: player cannot pass through Block_Tall, Wall_Low,
   Block_Small, Pillar; walks around them.
5. Slope: walk up Ramp_20deg (8, −6) — climbs smoothly, stays grounded;
   place a >45° slope (or tilt the ramp) and confirm the player slides
   down instead of standing on it.
6. Grounding: walk off the 1 m Wall_Low edge — falls, lands, `IsGrounded`
   returns true; no falling through the ground.
7. Input map: with the Input System package, confirm no errors about the
   "Gameplay" map; mouse delta arrives on Look (visible in the camera
   once M1.2 is wired); gamepad left stick moves the player if a pad is
   available.
8. Animator: `Speed` parameter in the Animator window tracks horizontal
   speed (0 idle → 8.5 sprint). Confirm NO animation plays yet (expected
   — no clips) and the capsule does not slide unnaturally (it has no legs
   to slide; the real foot-slide check waits for clips).
9. Camera (with M1.2): steady damped follow locked behind the player, no
   swing/drift while strafing or backpedalling, never hides the player
   behind Block_Tall / Wall_Low.
10. WebGL build: same checks 1–6 in a browser build at both widths;
    confirm the scene is in Build Settings and the build loads without
    Input System errors.

## Coordination notes

- M1.2 camera agent owns `PlayerCamera.cs` / `IPlayerCameraTarget.cs`
  (same folder) and the scene's camera rig. M1.1 owns only the files in
  the table above. The temp "Main Camera (TEMP - camera agent owns)"
  object in the greybox scene is a stand-in so the scene is playable
  before the camera rig lands; the camera agent replaces/removes it.
- Do not touch `~/workspace/naija-life` (frozen browser version).
