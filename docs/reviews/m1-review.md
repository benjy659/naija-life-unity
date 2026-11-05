# M1 review: player controller (M1.1) and camera (M1.2)

**Reviewer:** Claude (lead engineer). **Date:** 2026-11-05.
**Commits reviewed:** 293c1ae (M1.2 camera), 767ef42 (M1.1 controller).
**Status: code review only. UNVERIFIED in Unity.** No Unity Editor exists on
Ted's VM or in Claude's cloud container yet, so nothing here has been seen
running. M1 is not done until it is played in the Editor and a WebGL build at
phone and desktop widths (TASKS.md verification rules).

## What is good

- Clean split: the controller never touches the camera; the camera reads
  movement through `IPlayerCameraTarget`. Keep this contract.
- One camera state (`followYaw`), exponential framerate-independent damping,
  snap on spawn, fast pull-in / slow pull-out for occlusion. This is the right
  shape for the owner rule.
- Speeds match the design inventory (walk 2.4, jog 4.2, sprint 8.5), accel and
  decel ramps, no velocity snaps, Animator `Speed` driven from real speed.
- Honest status notes: UNVERIFIED written everywhere, no fake animation.

## Findings

| # | Severity | Finding | Status |
|---|---|---|---|
| R1 | Blocker | Camera circles the player while strafing or backpedalling | FIXED by Claude (this branch) |
| R2 | High | Keyboard always ran at 6.5 m/s instead of the 4.2 m/s default jog | FIXED by Claude (this branch) |
| R3 | High | Mouse and right stick do nothing: `AddOrbitInput` has no caller | Ted: M1.4 |
| R4 | High | URP, Linear colour, quality tiers and build scene list are not in the repo | Ted: M0.1 |
| R5 | Medium | Manifest has no `com.unity.ugui`; the touch On-Screen Stick needs it | Ted: M0.1 |
| R6 | Medium | Grounded probe counts the player as grounded up to about 0.4 m above the floor | Ted: M1.5 |
| R7 | Medium | No touch joystick yet, so phone width cannot move the player | Ted: M1.6 |

### R1. Camera circles the player (the owner's camera rule, broken)

`PlayerCamera.UpdateFollowYaw` held the heading only when the travel
direction was more than 35 degrees off the character's **facing**. But
`PlayerController` turns the body toward its travel direction at 720 deg/s,
so facing catches up with travel within a few frames of any input. After that
the dead zone passes, the camera re-aligns behind the body, the camera-relative
input direction turns with it, the body turns again, and so on.

Simulation of the two scripts' maths (`tools/camsim.js`, 6 s holding one key,
`node tools/camsim.js`):

| Key held | Old camera yaw after 6 s | Fixed camera yaw after 6 s |
|---|---|---|
| W | 0 | 0 |
| D (strafe right) | 844 deg (2.3 full circles) | 0 |
| S (back) | -847 deg | 0 |
| A (strafe left) | -844 deg | 0 |

**Fix (pushed):** compare the travel direction with the **camera heading**
(`_followYaw`) instead of facing. Holding D now runs the player to the right
across the screen with the lens held still; holding S runs toward the camera;
pushing W after a turn re-aligns the lens slowly behind. Still to see in Unity.

### R2. Keyboard ran by default

`inputMagnitude > 0.85` picked `runSpeed`. Keyboard input is always magnitude
1, so WASD ran at 6.5 m/s and the "default jog 4.2" never happened on desktop.
**Fix (pushed):** full input = jog; Shift = sprint; Ctrl/C = walk. `runSpeed`
stays as the blend threshold.

### R3. Look input is read but never used

`PlayerInput.LookInput` is filled every frame and `PlayerCamera.AddOrbitInput`
exists, but nothing connects them. Spec for M1.4 is in TASKS.md.

### R4 and R5. Project settings missing

`ProjectSettings/` holds only `ProjectVersion.txt`. On first open Unity will
generate defaults: no URP asset assigned (built-in fallback), and the colour
space default may not be Linear. The plan says these are locked on day one, so
they must be committed files, not a first-open step someone may forget. Also
add `com.unity.ugui` (Unity 6 ships TextMesh Pro inside uGUI 2.0; check
whether `com.unity.textmeshpro` 4.0.1 should be dropped once the Editor opens).

### R6. Grounded tolerance too loose

`UpdateGrounded` sets `IsGrounded` when the sphere cast hits within
`height/2 + 0.15` of the mid-body origin. With a 0.9 x radius sphere that is
roughly 0.4 m below the feet, so walking off a 0.4 m kerb keeps the player
"grounded" and falling at the 3 m/s stick speed instead of gravity: a floaty
step-down. Tighten to about 0.05 m below the capsule bottom, and only use the
probe to bridge a single-frame `isGrounded` flicker.
