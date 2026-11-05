# M3 spec: vehicles, driving and bot traffic

**Author:** Claude (lead engineer). **Date:** 2026-11-05. **Status:** READY for
Ted's team (tasks M3.1 to M3.8 in TASKS.md). Claude's own part (driving camera
rule, M3.C) is pushed on branch `claude/project-thread-o554bj`, UNVERIFIED until
played.

**Sources:** `docs/DESIGN-INVENTORY.md` §3 (B-39 hit rules) and §4 (vehicles),
`docs/MIGRATION-PLAN.md` §6 M3, `docs/specs/camera-m1.2.md`, the frozen web
game's lessons L-55, L-56, L-83, L-97, L-123, L-133 (benjy659/naija-life
TASKS.md, read-only) and its `docs/vehicle-research.md`.

**Owner rules this spec carries (Benjamin, quoted where he said them):**

1. Camera: "stay in the chosen direction (no swinging), follow the way the
   player faces, never obstructed" (2026-10-17). Same steady rules walking and
   driving (design inventory §4).
2. Traffic: "cars, busses passing through each other and the bridge ...
   everything should be aware of their surroundings and react" (2026-10-17,
   iPhone). Nothing ever drives through another vehicle, a person, a bridge
   pier or a prop.
3. The world plays itself with bots; a real player can take any job, replacing
   the bot (2026-10-16). M3 builds the driving part; the danfo driver job
   handover is M4.
4. No teleport. Getting in and out is walked, never a jump cut.
5. No real car maker, bus company or brand names or logos anywhere (design
   inventory §4).
6. Nothing is DONE until Ted's team posts Editor screenshots or a clip AND a
   WebGL build check under `qa/<task id>/` (verification rules, TASKS.md).

---

## 1. Scope

**In M3:** two drivable vehicles (danfo, tokunbo saloon car), enter and exit
any parked drivable, keyboard + gamepad + touch driving, chase camera, damage
stub, contact events for later police rules, and bot traffic that follows lanes,
sees what is ahead, brakes, queues, yields at a crossing, goes round a parked
obstacle and never passes through anything, on flat roads and on a bridge deck.

**Not in M3 (do not build yet):** ownership, garage, buying, fuel, licences,
police reaction, danfo passengers and fares (M4), keke/okada/BRT/train,
multiplayer sync (M5; but see §9 rule on keeping simulation separate),
aviation.

## 2. Test scene first, Oshodi later

M2 (Oshodi greybox) is not built yet. M3 must not wait for it.

Build `Assets/Scenes/Greybox_M3.unity` (copy of `Greybox_M1` plus):

- A rectangular road loop about 200 m by 120 m, two lanes each way, 3.5 m
  lanes, kerbs 0.15 m high, pavements 3 m both sides. Nigeria drives on the
  **right**.
- One four-way crossing in the middle of a long side (no lights; yield rule,
  §8.5).
- One **bridge**: a deck 6 m above the ground road, 80 m long, ramps at 6% each
  end, with the ground road passing UNDER it. Piers are round columns placed
  only outside the lanes (lesson L-83: nothing solid stands in a lane under a
  bridge).
- One narrow gate (two pillars, 3.2 m gap) on a side road, for the steer-round
  and stuck tests.
- Two parked drivables at the kerb: one danfo, one saloon car, with free space
  on the driver's side.
- A parked car half in the near lane of one side (the obstacle bots must go
  round).
- Five or six standing pedestrians (capsules are fine) on the pavement and one
  that steps into the lane on a timer (the brake test).

When M2 lands, the same prefabs and lane graph drop into Oshodi; the
acceptance in §11 is then repeated on the Oshodi loop.

## 3. Files and classes

All under `Assets/Scripts/Vehicles/`, namespace `NaijaLife.Vehicles`.
Gameplay code never calls the Input System or a netcode SDK directly from the
vehicle simulation (see §9).

| File | Role |
|---|---|
| `VehicleDefinition.cs` | ScriptableObject: every number in §4. One asset per type in `Assets/Settings/Vehicles/` (`Danfo.asset`, `TokunboSaloon.asset`). |
| `DriveInput.cs` | Plain struct: `throttle` (0..1), `brake` (0..1), `steer` (-1..1), `handbrake` (bool). The only way anything drives a vehicle. |
| `VehicleController.cs` | Rigidbody + 4 WheelColliders, arcade tuning (§6). `SetInput(DriveInput)`. Implements `NaijaLife.Player.IPlayerCameraTarget` (Velocity = rigidbody velocity, HorizontalSpeed, IsGrounded = at least 2 wheels touching, Facing = transform.forward). Exposes `ForwardSpeed` (signed m/s) and `SpeedKmh`. |
| `VehicleSeat.cs` | Marks the driver seat and the door: `seatPoint`, `doorPoint` (outside the driver door at standing height), `exitPoints[]` (driver side first, then passenger side, then behind). |
| `DriveSession.cs` | On the player. Runs enter and exit (§7): switches action maps, disables the CharacterController, parents the player to the seat, retargets the camera, restores everything on exit. |
| `VehiclePlayerDriver.cs` | Reads the `Driving` action map and touch controls into `DriveInput` and passes it to the controller. Only enabled while the player drives. |
| `VehicleDamage.cs` | Stub (§10): health 0..100 from impact impulses, smoke at 30, no wreck yet. |
| `VehicleContactReporter.cs` | Raises `OnContact(VehicleContact)` with the other collider, impact speed (km/h, relative), point and whether the other body is a person (§10). |
| `TrafficSense.cs` | The shared road sense (§8.2): every moving vehicle registers; `Ahead()` gives the gap to the nearest thing in its path. |
| `LaneGraph.cs` + `LaneNode.cs` | Authored lane network (§8.1), with node y so bridge decks are their own lanes. |
| `TrafficDriver.cs` | Bot driver (§8): follows the lane graph and drives a vehicle through the same `DriveInput` path or the kinematic mover (§8.3). |
| `TrafficSpawner.cs` | Keeps a pool of bot vehicles around the player (§8.6). |
| `Editor/LaneGraphValidator.cs` | Editor menu + PlayMode test: sweeps every lane for static colliders in the way (§8.7). |

Layers to add in `ProjectSettings/TagManager.asset`: `Vehicle` (bot and parked
vehicles), `DrivenVehicle` (the vehicle the local player drives), `Pedestrian`,
`Road`. Set `PlayerCamera.obstacleLayers` to everything except `DrivenVehicle`,
`Pedestrian` and `Player` (§5.3).

## 4. The two vehicles

Numbers start here and get tuned in play. Lengths follow real references
(web `docs/vehicle-research.md` §2), not the 5.6 m in the design inventory,
which is longer than a real T3 bus.

| Field | Danfo | Tokunbo saloon |
|---|---|---|
| Real reference (shape only, no brand) | VW T3-style bus | Corolla-class saloon |
| Length x width x height (m) | 4.9 x 1.9 x 2.3 (with roof rack) | 4.6 x 1.8 x 1.45 |
| Wheelbase (m) | 2.46 | 2.70 |
| Mass (kg) | 1,800 | 1,300 |
| Centre of mass (local, m) | (0, 0.55, 0.1) | (0, 0.40, 0.05) |
| Top speed forward (km/h) | 90 | 140 |
| Top speed reverse (km/h) | 15 | 20 |
| 0 to 50 km/h (s) | 7 | 4.5 |
| Max steer angle at 0 km/h / at top speed (deg) | 38 / 10 | 35 / 8 |
| Steer speed (deg/s at the wheel) | 90 | 120 |
| Brake decel at full brake (m/s²) | 6 | 7.5 |
| Anti-roll stiffness (N/m, per axle) | 9,000 | 6,500 |
| Camera distance / pivot height (m) | 8.0 / 2.6 | 6.5 / 1.8 |
| Seats (M3 uses driver only) | driver + 14 | driver + 4 |
| Driver door | left (Nigeria drives on the right) | left |

**Looks (greybox, no paid assets):** primitives or ProBuilder. Danfo: yellow
body, two black waist stripes, roof rack, a route board on the front reading
"OSHODI - OBALENDE", a sticker "No Food For Lazy Man" on the back. Saloon:
any plain colour, tokunbo look (one slightly different-colour door is a nice
touch). Wheels are separate child meshes with pivots at the hub, spun and
steered from the WheelCollider poses. Lowest vertex within 5 cm of the ground
at rest (design inventory §1 ground rule).

## 5. Chase camera (owner hard rule)

### 5.1 What Claude changed (M3.C, pushed, UNVERIFIED)

The M1 walking rule must not be used for a car. It re-aligns only while travel
is within 35 degrees of the **camera** heading (the R1 fix). A car in a U-turn
turns faster than the lens follows, leaves that cone, and the walking rule then
holds the lens sideways for good. `node tools/camsim.js` shows it:

| Case (12 m/s, 60 deg/s turn) | Walking rule, lens off the nose at the end | Driving rule |
|---|---|---|
| 90 deg corner | 0 (max 34 during) | 0 (max 24) |
| U-turn | **120 deg, stuck** | 0 (max 24) |
| Reversing while steering 60 deg | 61 (held) | 61 (held, on purpose) |
| Spin-out | 32 | 0 after the spin |

So `PlayerCamera` now has a separate driving rule, `UpdateDrivingYaw`, used
while `SetDrivingMode(true, ...)` is on:

- Re-align behind the vehicle's nose only while it rolls **forward** on the
  ground faster than 1.5 m/s (`drivingMinForwardSpeed`) at 2.4/s
  (`drivingRecenterRate`).
- **Hold** the heading when stopped, reversing, airborne, or sliding more than
  50 degrees off the nose (`drivingSlideHoldDegrees`). The lens never flips to
  the front when you reverse and never whips round in a spin.
- Manual orbit and its 2 s hold work as on foot.
- Per-vehicle framing: `SetDrivingMode(true, distance, pivotHeight)` from the
  VehicleDefinition. Distance and pivot height ease over about a second.
- `SetFollowTarget(target, snap: false)` retargets without a cut, so getting in
  or out keeps the current heading and the lens eases in behind once you drive
  forward.

Steering is relative to the vehicle, not to the camera, so the R1 circling
loop cannot happen while driving.

### 5.2 How DriveSession must call it

- Enter: `cam.SetDrivingMode(true, def.cameraDistance, def.cameraPivotHeight)`
  then `cam.SetFollowTarget(vehicle.transform, false)`.
- Exit: `cam.SetDrivingMode(false)` then
  `cam.SetFollowTarget(player.transform, false)`.
- Never call `SnapToTarget()` on enter or exit.

### 5.3 Never obstructed

- The camera pivot sits inside the car's own body, so the driven car's
  colliders must not block the lens. On enter, `DriveSession` moves every
  collider of that vehicle to layer `DrivenVehicle`; on exit it restores
  `Vehicle`. `PlayerCamera.obstacleLayers` excludes `DrivenVehicle`.
  Other vehicles, buildings, bridge decks and walls still pull the lens in.
- Pedestrians never pull the lens in (exclude `Pedestrian`), or every person
  walking behind the car would jerk the camera.
- Under the bridge test: drive under the deck and on top of it. The lens must
  never sit inside the deck, and must never jump between above and below.

## 6. Driving model (arcade, WheelCollider)

- Rigidbody: `interpolation = Interpolate`, `collisionDetection =
  ContinuousDynamic` for the driven car (prevents tunnelling at 140 km/h
  through thin bot colliders), `Discrete` for parked ones.
- Physics: Fixed Timestep 0.02 (50 Hz) in `ProjectSettings/TimeManager.asset`;
  measure on WebGL before raising.
- Motor torque from a simple curve: full torque to 60% of top speed, falling
  to zero at top speed. One gear plus reverse. No clutch.
- Speed-sensitive steering: lerp max steer angle from the 0 km/h value to the
  top-speed value by `speed / topSpeed`; steer angle moves toward the target at
  `steerSpeed` (no instant snaps on keyboard).
- Brake and reverse: brake input while rolling forward brakes; holding brake
  while below 1 km/h engages reverse after 0.25 s; throttle while rolling
  backwards brakes first. One pedal pair does everything, like GTA.
- Handbrake: locks rear wheels, lowers rear sideways stiffness to 0.5 while
  held.
- Stability: anti-roll bar per axle (classic two-WheelCollider anti-roll
  force), a small downforce `0.5 * speed²` N, and a yaw assist that damps
  spin when no steer input is held. A danfo should feel heavy and lean in
  corners but never roll over on the flat test loop at any speed.
- Kerbs: the 0.15 m kerb must be climbable at walking pace with a bump, not a
  launch. WheelCollider `suspensionDistance` 0.25 (danfo) / 0.2 (car).
- Speed readout: `SpeedKmh` for the HUD (a small speedo bottom-right above the
  pedals; numbers only, no plain text label).

## 7. Getting in and out

### 7.1 Enter

1. Only **parked** drivables (no driver, not a bot traffic vehicle) offer
   entry in M3. Taking a bot's car is carjacking, a crime; that waits for the
   police work.
2. The offer appears when the player is within **2.5 m of the `doorPoint`** and
   roughly facing the vehicle. One prompt only ("Drive" with a steering-wheel
   icon), never over the player or the joystick (MU rules). Desktop key F,
   gamepad Y/north button, phone: a round button on the right above the sprint
   button.
3. On press the player **walks** to the `doorPoint` (short auto-walk at walk
   speed, max 1.5 s, using the normal controller so feet stay planted), turns
   to the car, then is placed in the seat. No fade, no teleport across the map.
   If the walk is blocked for 1.5 s, cancel and keep the prompt.
4. In the seat: CharacterController disabled, player parented to `seatPoint`,
   player mesh stays visible through the windows (sit pose if the animator has
   one, else idle; hiding the player is acceptable in M3 only if the windows
   show nothing, and must be noted UNVERIFIED for M4).
5. Switch input: `Player` map off, `Driving` map on. Camera per §5.2. Move the
   vehicle's colliders to `DrivenVehicle`. Rigidbody to dynamic and awake.

### 7.2 Exit

1. Allowed only when `|ForwardSpeed| < 2 m/s`. At higher speed the button
   says "Stop first" via its icon state; it never bails out of a moving car in
   M3.
2. Pick the first `exitPoint` where a standing capsule (the player's
   CharacterController size) does not overlap anything (`Physics.CheckCapsule`
   against everything except the player and the driven car). Driver side first,
   then passenger side, then behind. If none is free, keep the player in the
   seat and show the blocked state; never place the player inside a wall or
   another car.
3. Place the player at the exit point standing beside the door, facing the
   same way as the car. Restore layers, input maps, the CharacterController and
   the camera (§5.2). Apply handbrake on the vehicle so it does not roll away.
4. The vehicle stays exactly where it stopped as a parked drivable.

### 7.3 Regressions guard

Walking must behave exactly as before after any number of enter/exit cycles:
same speeds, same camera rule, grounded check correct, no leftover parenting.
This is part of the acceptance (§11).

## 8. Bot traffic that sees and reacts

Lessons from the web game (L-83, L-97): its traffic only looked for the player
on foot, so vehicles drove through each other, and a bridge stood on solid
walls across a road. Here every rule below is enforced by physics, not by a
list someone forgets to update.

### 8.1 Lane graph

- `LaneGraph` is a set of `LaneNode`s (position including **y**, speed limit,
  links to next nodes) authored in the scene with gizmos. Lanes follow the
  right-hand side of the road, 1.75 m from the centre line of a 3.5 m lane.
- The bridge deck and the road under it are separate chains of nodes at
  different heights. A vehicle on the deck follows deck nodes only.
- Crossings mark their entry nodes with a `crossingId` (§8.5).

### 8.2 TrafficSense (the shared road sense)

Every moving vehicle (bots, the player's car, later buses and rides) registers
with `TrafficSense` on enable and unregisters on disable. Each bot asks every
physics step:

- **Ahead gap:** a `Physics.BoxCast` from the front bumper along the path,
  box = vehicle width + 0.3 m by vehicle height, length = stopping distance at
  current speed + 3 m, mask = `Vehicle`, `DrivenVehicle`, `Player`,
  `Pedestrian`, and static world. Because it is a real physics cast at the
  vehicle's own height, a car on the bridge deck never brakes for a car below
  it, and a car under the bridge never sees the deck above as blocking (the
  web game needed a separate "same deck level" check for this).
- On curves, cast along the next two lane segments, not straight ahead.
- **Speed for gap:** target speed = min(lane limit, speed that stops 1 car
  length (4.5 m) short of the obstacle at 5 m/s² decel). Queue behind a slower
  vehicle at a 2 s gap.

### 8.3 How bots move

- Bots near the player (within 120 m) are **dynamic** WheelCollider vehicles
  driven through `DriveInput` by `TrafficDriver` (steer toward a look-ahead
  point 6 to 10 m along the lane, throttle and brake from §8.2). The same
  controller as the player, so bumping a bot feels like bumping a car.
- Bots further away are **kinematic** (`Rigidbody.MovePosition` along the
  lane at the sensed speed, wheels spun visually) to save CPU on WebGL. They
  still cast and brake the same way. Switch with hysteresis (dynamic below
  110 m, kinematic above 130 m) and only while the bot is stopped or on a
  straight.
- **Never** turn off a bot's collider or make it pass through anything to get
  unstuck.

### 8.4 People

- A pedestrian or the player in the cast makes the bot brake to a stop 2 m
  short. A person standing in the lane for 3 s gets a short horn (one beep, not
  a loop) and, if a bot pedestrian, steps back to the pavement.
- Children (when they exist) are treated the same; never faster.

### 8.5 Crossings

At a `crossingId` entry node, a bot checks whether any other registered
vehicle is inside the crossing box or will reach it within 2 s. First to arrive
goes; on a tie the lower registration id goes. A bot that waits more than 8 s
creeps forward at 1 m/s, still braking for anything in the cast.

### 8.6 Steering round things and getting unstuck

- If the gap stays under 6 m for 2 s and the blocker is **not moving** (parked
  car, fallen prop), the bot tries an offset path: shift the look-ahead point
  up to 3 m to the left (overtaking side for right-hand traffic) when the cast
  along that offset path is clear for the length of the obstacle + 10 m,
  including oncoming vehicles. Then return to its lane.
- If there is no way round, it waits, then after 12 s reverses up to 10 m and
  tries again; after 3 failed tries it picks another route at the next node,
  or despawns if it is out of the player's view and more than 80 m away.
- It never drives through. The web game's "carry on after 9 s" rule is
  dropped on purpose.

### 8.7 Lanes must be clear (LaneGraphValidator)

An Editor menu **Naija > Validate Lanes** and a PlayMode test sweep a box the
size of the danfo along every lane segment and list every static collider it
hits (bridge piers, poles, stalls, kiosks, buildings). The test fails on any
hit. Run it after every scene change that touches roads. This is the Unity
version of the web rule "a structure over a road may only stand where the
lanes are clear" (L-83) and "anything in a lane is a map fix, not a traffic
fix" (L-97).

### 8.8 Spawner and budget

- Up to 12 bot vehicles in the test scene; at most 20 alive anywhere later.
- Spawn only out of camera view and at least 60 m from the player, on a lane
  node with nothing in the cast for 15 m. Despawn when more than 200 m away
  and out of view. Pool, never Instantiate per spawn.
- Mix in the test scene: 1 in 3 danfo, the rest saloon cars in varied colours.
- Budget on WebGL at 390x844: 12 bots must hold 30 fps; report frame time with
  and without traffic in `qa/m3.6/REPORT.md`.

## 9. Ready for multiplayer and jobs later

- `VehicleController` only ever reads a `DriveInput`. Player input, bot AI,
  and later the network replicator all produce a `DriveInput`; none reach into
  the controller another way. This is the hook M4 (danfo driver job handover)
  and M5 (remote vehicles) need.
- No vehicle state lives in static singletons except the `TrafficSense`
  registry, which is per scene.
- Vehicle ids are stable ints from the spawner, never names.

## 10. Damage stub and contact events

- `VehicleDamage`: health 100. Each collision with impulse above a threshold
  removes `impulse / def.damageScale`. Smoke particle under 30. At 0 the
  engine stops (throttle ignored) until reset in the test scene. No explosions,
  no wrecks in M3.
- `VehicleContactReporter` raises `OnContact` from `OnCollisionEnter` only:
  real body contact, never a distance check or radius (L-123, B-39). Fields:
  other collider, `isPerson` (layer `Pedestrian` or `Player`), relative impact
  speed in km/h, point, time. One event per body per 1.5 s.
- M3 only **logs** contacts (and shows them in a debug overlay in the test
  scene). No stars, no fines, no arrest. The police work will listen to this
  event later with the B-39 rules: a hit is contact at 15 km/h or more; a near
  miss is never a crime.

## 11. Acceptance (all in Editor play AND a WebGL build, 1280x800 and 390x844)

Post screenshots or a short clip for each line under `qa/<task id>/`, with a
`REPORT.md` that says PASS, FAIL or UNVERIFIED per line.

1. Walk to the danfo, press Drive: the player walks to the door, sits, and
   the camera eases out behind without a cut or a swing.
2. Drive the full loop including the bridge in both directions: no camera
   swing on corners, U-turn at the end of the loop leaves the lens behind the
   car within about 2 s of driving straight.
3. Reverse 20 m while steering: the lens stays put (does not flip to face
   the front).
4. Handbrake spin: the lens holds during the spin and settles behind once
   driving forward.
5. Under and on the bridge: the lens is never inside the deck or a pier; the
   car never falls through the deck or the ramp.
6. Stop next to a wall on the driver side and press exit: the player gets out
   on the passenger side. Boxed in on all sides: the player stays in.
7. After 5 enter/exit cycles, walking matches M1 (speeds, camera rule).
8. Kerb at walking pace: bump, no launch. 140 km/h into a bot car: no
   tunnelling, both cars react.
9. Traffic (12 bots, 3 minutes): no vehicle passes through another vehicle,
   a person, a pier or the parked car; bots queue, yield at the crossing, go
   round the half-parked car, and stop for the pedestrian who steps out.
   Bots on the deck never brake for cars below and the reverse.
10. Lane validator: 0 hits.
11. Phone 390x844: pedals and steer controls never cover the car; touch
    targets 64 to 104 px; 30 fps with 12 bots.

## 12. Touch and keyboard controls

- **Desktop:** W/S or Up/Down = throttle/brake-reverse, A/D = steer, Space =
  handbrake, F = exit, right mouse drag = orbit (same rule as on foot).
- **Gamepad:** RT throttle, LT brake/reverse, left stick X steer, A/south
  handbrake, Y/north exit, right stick orbit.
- **Phone:** left thumb: a horizontal steer slider (the on-foot stick swaps to
  left/right only while driving, same position, 140 px base). Right thumb:
  gas pedal (tall, 104 px) and brake pedal (wide, 88 px) side by side above
  the bottom edge, handbrake small above them (64 px), exit round button top
  of that column. No other buttons while driving in M3. Nothing may sit over
  the centre third of the screen where the car is.
- New `Driving` action map in `PlayerInput.inputactions`; the touch controls
  are On-Screen Controls bound to the same actions (needs `com.unity.ugui`
  from M0.1).
