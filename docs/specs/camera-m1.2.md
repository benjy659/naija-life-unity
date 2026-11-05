# M1.2 — Third-Person Camera (implementation note)

**Status: UNVERIFIED.** No Unity Editor exists on this VM yet, so this code has
never compiled or run. It is written against the Cinemachine 3.1.4 API as
documented for 3.1 (namespace `Unity.Cinemachine`, `CinemachineCamera`,
`CinemachineThirdPersonFollow` + nested `ObstacleSettings` — all verified
against the Unity 3.1.7 scripting reference on 2026-11-05). First Editor open
must compile it before anything else. Do not call M1 done on code review alone.

Spec sources: `docs/DESIGN-INVENTORY.md` §2 (Camera — owner hard rule) and
`docs/MIGRATION-PLAN.md` §6 M1.

## Files

| File | What it is |
|---|---|
| `Assets/Scripts/Player/PlayerCamera.cs` | The camera. Builds its own rig at runtime, owns the follow-yaw rule, drives a CinemachineCamera + ThirdPersonFollow. |
| `Assets/Scripts/Player/IPlayerCameraTarget.cs` | Optional interface the M1.1 controller can implement for authoritative movement state. Not required. |

No scene was touched (the greybox scene `Assets/Scenes/Greybox_M1.unity` is
owned by the M1.1 controller agent and did not exist yet). No prefab — the rig
is built in code so wiring is one component drag.

## Wiring (greybox scene)

1. Add an empty GameObject (e.g. `PlayerCamera`) and attach `PlayerCamera`.
2. Set **Follow Target** to the player root Transform — or leave it empty and
   tag the player GameObject `Player` (auto-find, retries until it appears).
3. Tag the player `Player` in any case: Cinemachine ignores obstacles with
   that tag, so the player's own colliders never pull the camera in.
4. Make sure the scene has a Main Camera. The script adds a
   `CinemachineBrain` to it if one is missing.
5. World geometry that must block the camera (walls, buildings) needs
   colliders on layers included in **Obstacle Layers** (default: Everything).

## The follow-yaw rule (owner hard rule, in one paragraph)

The script owns exactly one heading value, `followYaw`. It changes ONLY by
(a) manual orbit input via `AddOrbitInput` (mouse drag / right stick — a user
action, never automatic), or (b) damped re-alignment toward the player's
facing, and only while the player is grounded, moving faster than
`minMoveSpeed` (0.6 m/s), AND the angle between velocity direction and facing
is inside the dead zone (`strafeDeadZoneDegrees`, 35°). Strafing or
backpedalling suspends re-alignment entirely, so the lens never swings for a
sideways step. Idle never re-aligns. Recenter is exponential
(`recenterRate` 1.6/s), so turning a corner is a smooth swing, never a snap.
After manual orbit ends, re-alignment stays suspended for
`orbitRecenterDelay` (2 s).

## Interface for the M1.1 controller

**Camera → controller (camera-relative movement):**
`GroundForward`, `GroundRight`, `FollowYawDegrees` on `PlayerCamera` — the
camera's ground-plane axes, guaranteed to match where the lens points.

**Controller → camera (optional, all with fallbacks):**
- Implement `IPlayerCameraTarget` on the player (`Velocity`,
  `HorizontalSpeed`, `IsGrounded`, `Facing`) for authoritative state.
- Without it: `CharacterController.velocity` / `isGrounded` are used; without
  that: velocity from transform deltas (smoothed), facing from
  `followTarget.forward`, grounded assumed true.
- Feed manual orbit: call `AddOrbitInput(delta)` every frame input is active
  (mouse drag / right stick). If nobody calls it, the camera never orbits.
- `SetFollowTarget(Transform)` — retarget (player ↔ vehicle in M3).
- `SetDrivingMode(bool)` — eases distance 4.5 m → 6.5 m for driving (M3).
- `SnapToTarget()` — snap behind the player now (spawn, warp, target change).

## Occlusion (never hide the player)

`CinemachineThirdPersonFollow.AvoidObstacles` is enabled: pull-in damping
0.15 s (fast — the player is hidden for as little as a frame as possible),
pull-out damping 1.2 s (slow — no snapping back, no oscillation), collision
radius 0.3 m. The camera is pulled in FRONT of any wall between it and the
player, so line of sight is restored by construction.

## Tuning defaults

| Setting | Value | Why |
|---|---|---|
| Walk distance / drive distance | 4.5 m / 6.5 m | GTA-style chase; wider while driving |
| Pitch | 12° down | Player sits slightly below centre |
| Pivot height | 1.55 m | Shoulder height, relative to the player |
| Rig damping (x/y/z) | 0.25 / 0.45 / 0.25 s | Vertical bob damped hardest |
| FOV landscape / portrait | 60° / 68° | Wider on narrow phone screens |
| Min move speed | 0.6 m/s | Turning in place never swings the lens |
| Strafe dead zone | 35° | Forward-ish travel re-aligns; strafe/backpedal holds |
| Recenter rate | 1.6/s | Smooth, no sudden swings |

No world-Y clamp exists anywhere: the pivot sits at player position +
`pivotHeight`, so interiors use the player's floor as camera floor (design
inventory §2).

## Known gaps / NOT implemented

- **Occluder fade:** only pull-in is implemented. Fade (making an occluding
  wall transparent) needs material manipulation of arbitrary occluders and
  was deliberately not attempted; the pull-in alone satisfies "a building/wall
  can never hide the player", because the camera always ends up in front of
  the occluder. If the verifier finds a case where pull-in alone looks bad
  (e.g. camera jammed against a wall in a narrow alley), fade is the follow-up.
- **Cutscene/flyover cameras:** out of scope for M1.2. The vcam priority is
  10; a flyover cam with higher priority will take over via the Brain, and
  the gameplay cam resumes when it is disabled. Brain default blend is left
  at the project default — set it to Cut in the scene if the spawn blend
  swoops.
- **No compile or play test yet** (see Status above).

## Verification checklist (for the verifier — Editor + WebGL build)

Run in the Unity Editor first, then in a WebGL build, at phone width
(390×844 portrait) and desktop (1280×800 landscape):

1. **Spawn:** camera starts behind the player immediately, no swoop, no
   2-second blend drift. Player framed slightly below centre.
2. **Walk forward, turn corners:** camera follows smoothly behind; no sudden
   swings, no snapping. Turning 180° and walking back re-aligns smoothly.
3. **Strafe left/right (A/D with no mouse):** camera heading does NOT move.
   Same for backpedalling (S). This is the dead-zone test — the rule the
   browser version kept failing.
4. **Stand still / turn in place:** camera does not drift or orbit.
5. **Manual orbit (mouse drag):** camera orbits; on release it stays put for
   ~2 s, then gently re-aligns only once the player walks forward.
6. **Walk into a wall / back against a building:** camera pulls in smoothly
   (no snap); the player is NEVER hidden behind the wall. Walk away: camera
   eases back out slowly, no oscillation.
7. **Walk through a narrow gap between two buildings:** no jitter, no
   clipping through geometry, player always visible.
8. **Phone width:** player not filling the frame; HUD/joystick do not cover
   the player; FOV feels wider than desktop.
9. **Frame rate:** no per-frame allocation spikes from the camera.
   Movement-state components are cached on target change (no per-frame
   `GetComponent`); verify with the profiler in the Editor.
10. **Drive mode (once M3 lands):** `SetDrivingMode(true)` eases the camera
    out to 6.5 m with the same no-swing rules.

## Risks to check at first Editor open

- Cinemachine 3.1.4 API drift vs the 3.1.7 docs used here (fields on
  `CinemachineThirdPersonFollow` / `ObstacleSettings` / `CinemachineCamera.Lens`).
- `AddCinemachineComponent<T>()` behaviour when the vcam is created at
  runtime (should be fine — it is the documented CM3 API).
- `ReadMovementState` caches its component lookups on target change (no
  per-frame `GetComponent`).
