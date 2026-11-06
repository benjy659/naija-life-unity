# M5 netcode layer and two-client smoke test (spec)

Owner of this spec: Claude (multiplayer and backend workstream).
Builders: Ted's team (Fusion SDK install, Photon app, Supabase Edge Function,
Editor and WebGL verification). Date: 2026-11-05.
Status: SPEC READY. Code in `Assets/Scripts/Network/` is UNVERIFIED (written
without a Unity Editor; nobody has compiled or run it yet).

Sources: `docs/MIGRATION-PLAN.md` §4, `docs/DESIGN-INVENTORY.md` §8,
TASKS.md M5.2 verdict (Fusion 2 Shared Mode + Supabase, fixed EU region,
abstraction first), browser `docs/netcode-spec.md` (design only).

---

## 1. Why a layer first

Fusion Shared Mode is client-authoritative. Before the economy grows or
cheating shows up we must be able to move to Fusion Server Mode or our own
servers without touching gameplay. So:

- **Gameplay code never references `Fusion.*`.** Only
  `Assets/Scripts/Network/Fusion/` does. A grep for `using Fusion` outside that
  folder is a review failure.
- Gameplay talks to four interfaces: `INetSession`, `IPlayerReplicator`,
  `IWorldEvents`, `IChatService`. Two more sit beside them for Supabase:
  `IStreetCardService` (identity) and later the ledger service (M4/M6).
- The maths that decides when to send, how to pack a pose, which cells you
  are in and how to draw a remote player lives in plain C# with no Unity or
  Fusion types (`NaijaLife.Network.Core`, `noEngineReferences: true`). It is
  the same under every transport and is unit tested.

```
 Gameplay (Player, Vehicles, Jobs, UI)          <- Assembly-CSharp
   |  NetPose, PublicState, WorldEvent
   v
 NaijaLife.Network.Core   interfaces + SendPolicy, MoveCodec, CellGrid,
                          SnapshotBuffer, NearbyPicker, Backoff, OfflineSession
   ^                                   ^
 NaijaLife.Network.Fusion             (later) Server Mode / own WS server
   Photon Fusion 2 Shared              same interfaces, no gameplay change
 Supabase (REST + Edge Functions): auth, street card, money, friends, DMs
```

## 2. Assemblies and folders

| Folder | Assembly | References | Notes |
|---|---|---|---|
| `Assets/Scripts/Network/Core/` | `NaijaLife.Network.Core` | none | `noEngineReferences: true`. No `UnityEngine`. |
| `Assets/Scripts/Network/Fusion/` | `NaijaLife.Network.Fusion` | Core + Fusion DLLs | `defineConstraints: ["NL_FUSION"]`, so it does not compile until the SDK is in and the define is set. Add this assembly to Fusion's **Assemblies to Weave** list in the NetworkProjectConfig. |
| `Assets/Scripts/Network/Unity/` | Assembly-CSharp (no asmdef) | everything | Glue between `PlayerController` and the layer (player scripts have no asmdef yet). |
| `Assets/Tests/EditMode/Network/` | `NaijaLife.Network.Tests` | Core + NUnit | Edit Mode tests, run from Test Runner. |

## 3. The interfaces (contract)

Full signatures are in the code; this is what each one promises.

### 3.1 `INetSession`

- `Status`: `Offline`, `Connecting`, `Online`, `Reconnecting`, `Failed`.
  `StatusChanged` fires on every change. The HUD shows a calm pulse while
  `Connecting`/`Reconnecting` (no hanging waits rule); the game stays fully
  playable while not `Online`.
- `JoinAsync(NetJoinRequest, CancellationToken)`: joins one **room**.
  A room is one district instance: `RoomKey` = `<city>:<district>:<n>`, for
  example `lagos:oshodi:1`. Returns a `NetResult` (`Ok`, `AuthFailed`,
  `RoomFull`, `Timeout`, `NoNetwork`, `Error`) and never throws for network
  reasons.
- `LeaveAsync()`: leaves the room cleanly (used before a district change and
  on quit).
- `LocalPlayer`: our `NetPlayerId` = the Supabase `auth.uid()` as text,
  stamped by the server (§5), never chosen by the client.
- `RemotePlayers`, `PlayerJoined`, `PlayerLeft`: only real connected players.
  **No fake, bot or seeded players ever come through this interface.** Bots
  are world simulation, not network players.
- `Players` (`IPlayerReplicator`), `Events` (`IWorldEvents`), `Chat`
  (`IChatService`): the three channels.
- `SetInterestCenter(x, z)`: tells the transport where we are, for area of
  interest (§4.3).

District change = `LeaveAsync` then `JoinAsync` behind the existing travel
fade. Max room size 80 (fits Fusion's free 100 CCU with room to spare and the
50 to 100 target). When `RoomFull`, the client tries `<n+1>`.

### 3.2 `IPlayerReplicator`

- `SubmitLocal(NetPose pose, double now)`: gameplay calls it **every frame**
  with the local pose. The replicator decides if this frame is sent, using
  `SendPolicy` (§4.1). Gameplay never decides send timing.
- `SetLocalPublicState(PublicState)`: slow state (appearance id, vehicle model
  id, public flags). At most one change per 5 s is sent; later calls inside
  the window replace the pending value.
- `TryGetRemotePose(id, double now, out NetPose)`: the interpolated pose to
  draw this frame (§4.2). False = do not draw (unknown, hidden, or too many
  bad snapshots).
- `TryGetPublicState(id, out PublicState)`.
- `GetNearby(centerX, centerZ, List<NetPlayerId> into)`: at most **40**
  nearest remote players, nearest first. Drawing and name tags use this list
  only.

### 3.3 `IWorldEvents`

- `Raise(WorldEventKind kind, ushort arg)`: `Wave`, `Hail`, `Horn`, `Emote`
  (arg = emote id), `EnterVehicle`, `ExitVehicle` (arg = vehicle model id).
- Sender limit: 2 per second (`RateLimiter`); extra raises return false and
  are not sent. Receivers apply the same limit per sender and drop extras.
- `Received(NetPlayerId from, WorldEvent ev)`: `from` comes from the
  transport, never from the payload.

### 3.4 `IChatService` (M5.1 text chat)

- `Lobby`: one room-wide channel (the district room). Ephemeral, rides the
  Fusion room (RPC to all). Max 200 characters, 1 message per second per
  sender, text escaped before display, blocklist applied before send and on
  receive.
- `SendDirectAsync(to, text)` and `DirectReceived`: one-to-one between
  friends. **Not over Fusion**: DMs go through Supabase Realtime private
  broadcast topic `d:<lo uid>:<hi uid>` with RLS (low message rate, so
  Realtime is fine here; it was only rejected for movement). Ephemeral: no
  server table. Device-local log only, with the phone Settings disclosure
  (design inventory §8).
- No voice chat (owner boundary, Oct 10).

### 3.5 `IStreetCardService` (identity)

- `GetAsync(NetPlayerId)`: looks the player up once and caches the card for
  **10 minutes** (`StreetCardCache`). Name tags, crew badge and the street
  card UI read only from here.
- Live state (checked in the Supabase catalog 2026-11-05): `nl_street_card`
  takes a **username** (`p_username text, p_request_id uuid`), not a user id,
  and is limited to 120 calls an hour per caller. The network only knows the
  user id. So M5.1 needs a new RPC **`nl_street_card_by_id(p_uid uuid,
  p_request_id uuid)`** that returns the same jsonb as `nl_street_card` (same
  ban, block and audit checks) with its own limit of 600 an hour (one room of
  80 players, joins and leaves, 10-minute cache). Ted's team writes it as the
  next free migration; Claude reviews; Benjamin's OK before it is applied;
  Claude never applies migrations. Until it is live, name tags show
  "Player" plus nothing else (no made-up names).
- A name, crew or badge is **never** read from a network payload.

## 4. Behaviour in Core (same under every transport)

### 4.1 Send policy (`SendPolicy`)

| state | when a pose is sent |
|---|---|
| driving | every 250 ms (4 Hz) |
| walking/running | every 500 ms (2 Hz, owner decision Oct 23, B-34) |
| starts or stops moving, enters or leaves a vehicle, goes indoors | at once |
| moving flag set but pose changed under 5 cm and under ~1 degree | treated as standing |
| standing still | one at the stop, then a keep-alive every 5 s |
| app hidden / paused | nothing (`SetPaused(true)`) |

Under Fusion the replicator writes the networked pose only when the policy
says so; Fusion then only sends what changed. Fusion tick rate for the room:
**30 Hz simulation, 15 Hz client send** (set in NetworkProjectConfig) so the
policy, not the tick, is the real limit. 80 players at a mix of 2 and 4 Hz is
well inside one room's budget.

### 4.2 Drawing remote players (`SnapshotBuffer`)

- Keep the last 8 snapshots per remote player, stamped with **local receive
  time**.
- Render a walker **600 ms** in the past and a driver **300 ms** in the past
  (design inventory §8 and migration plan §4 both say ~300 ms for drivers; the
  browser spec's 150 ms assumed a different stack). When a player's mode
  changes, the delay slides at 300 ms per second, so it never jumps.
- Interpolate position linearly and heading along the short arc.
- Late packets: extrapolate at most 250 ms along the last velocity, then hold
  still.
- Drop a snapshot whose implied speed from the previous good one is over
  **45 m/s driving** or **9 m/s on foot**. More than 5 drops in 10 s = hidden
  until the next good snapshot.
- Gap over **30 m** between two good snapshots (for example, a player got into
  a car across a junction or a district respawn): snap, no slide.
- The local player never teleports; this is drawing only.

Under Fusion: we do **not** use `NetworkTransform` for players. The Fusion
avatar is an invisible data carrier (`[Networked]` packed pose + sequence);
the game draws remote players from `SnapshotBuffer` so the feel is the same
when the transport changes.

### 4.3 Area of interest (`CellGrid`)

- Cells are **200 m** squares per city: `cx = floor(x/200)`, `cz = floor(z/200)`.
- Interest = own cell plus the 8 around it (a 600 m square). When crossing a
  border, the new ring is added at once; the old cells are kept **3 s** more
  so walking along a border does not churn.
- Under Fusion Shared Mode: `SetInterestCenter` maps to Fusion Area of
  Interest for the local player (radius = 300 m box). Check the exact Fusion
  2 call (`Runner.AddPlayerAreaOfInterest` / object interest = Area Of
  Interest) in the SDK version installed and note it in the PR.
- `NearbyPicker` keeps the 40 nearest; anyone further is not drawn even if in
  interest.

### 4.4 Reconnect (`Backoff`)

`delay = random(0, min(30 s, 1 s * 2^attempt))` (full jitter, so a server blip
does not cause every client to reconnect at the same moment). Reset after
60 s online. One connection per account: a second login takes over and the
first shows "Playing on another device".

### 4.5 Movement packing (`MoveCodec`)

20 bytes, little-endian, used by any byte transport and by the Fusion avatar
(packed into networked fields):

| bytes | field | encoding |
|---|---|---|
| 0 | schema version | 1 |
| 1 | flags | bit0 moving, bit1 driving, bit2 running, bit3 indoors, bits4-7 anim id |
| 2-3 | sequence | uint16, wraps |
| 4-7 | x | float32 metres |
| 8-11 | z | float32 metres |
| 12-13 | y | int16 centimetres (-327 m to +327 m) |
| 14-15 | heading | uint16, 0..65535 = 0..360 degrees |
| 16-17 | speed | uint16 cm/s (max 655 m/s) |
| 18-19 | vehicle model id | uint16, 0 = on foot |

Receivers drop: wrong version, wrong length, NaN or infinite numbers, older
sequence (wrap aware).

## 5. Identity, auth and the money rule

1. **Photon custom authentication** against a new Supabase Edge Function
   `photon-auth` (Ted's team writes and deploys it; Claude reviews). The client
   sends its Supabase access token; the function verifies it with Supabase
   Auth and returns `{ ResultCode: 1, UserId: <auth.uid()> }`, else
   `ResultCode: 2`. Photon then stamps that UserId on the player, so
   `NetPlayerId` cannot be faked. Anonymous Photon auth is switched **off** in
   the Photon dashboard.
2. **Fixed region `eu`** for every client (`FixedRegion = "eu"` in Photon app
   settings), so Lagos and Abuja players always meet in the same region.
3. Nothing in any Fusion state, RPC or chat line ever carries: Game NIN,
   email, date of birth, money or wallet values, exact home location, device
   id. `PublicState` is a fixed struct with only `AppearanceId`,
   `VehicleModelId`, `PublicFlags`, so a forbidden field cannot be added by
   accident; adding a field needs a spec change.
4. Money, ownership, wages, purchases: **only** Supabase RPCs / Edge Functions
   (server ledger). Fusion never decides who owns what or how much money
   anyone has.
5. Shared Mode risk we accept for M5: a modified client can lie about its own
   position. Receivers clamp speed (§4.2), so it can only look odd. Earnings
   checks (speed, distance, shift time) are done server-side in the job RPCs
   (M4). Move to Server Mode before the economy scales.
6. The Photon App ID is public in a WebGL build by design; it is still kept
   out of git (set in the Fusion settings asset locally, documented in the PR).
   No Supabase service key ever ships in the client.

## 6. Offline

`OfflineSession` implements `INetSession` with no remote players and status
`Offline`. It is used when there is no network, when Photon auth fails, and in
single-player test scenes. It never invents players.

## 7. M5.1 two-client smoke test (Ted's team builds, Claude verifies)

### 7.1 Setup tasks (Ted's team)

- **M5.1a** Import Photon Fusion 2 (latest 2.x stable for Unity 6.3) from the
  Photon dashboard package; create a Photon Fusion app on the free plan; set
  App ID locally; `FixedRegion = "eu"`; add scripting define `NL_FUSION`; add
  `NaijaLife.Network.Fusion` to Assemblies to Weave; NetworkProjectConfig tick
  30 Hz, client send 15 Hz. Commit the config asset but **not** the App ID if
  the settings asset holds it (tell Claude in the inbox how it is kept out).
- **M5.1b** Supabase Edge Function `photon-auth` (§5.1) and Photon dashboard
  custom auth pointing at it, anonymous off. Ted owns deploys. The project
  has no Edge Functions yet (checked 2026-11-05), so this is the first one.
- **M5.1c** Fix compile errors in `Network/Fusion/` against the real SDK
  (the skeleton was written without the SDK; keep the shape, fix the calls).
  Run the Edit Mode tests in `Assets/Tests/EditMode/Network/`: all green.
- **M5.1d** Greybox scene `Assets/Scenes/Net_M5.unity`: copy of Greybox_M1
  with a `NetBootstrap` object (district `oshodi`), the `NetPlayerBridge` on
  the Player, and a `RemotePlayerSpawner` using a coloured capsule prefab with
  a world-space name tag that reads from `IStreetCardService`.
- **M5.1f** Migration `nl_street_card_by_id` (§3.5). Review by Claude,
  apply only with Benjamin's OK.
- **M5.1e** Text chat panel stub (lobby only is enough for M5.1; DM after).

### 7.2 Acceptance (all must be seen, recorded under `qa/M5.1/`)

1. Two clients: Editor + WebGL build in a browser (and a second WebGL tab on
   a phone-width window). Both join `lagos:oshodi:1`.
2. Each sees the other as a capsule within 2 s of joining. Name tag shows the
   username from the street card, not "Player 2".
3. Walk: remote capsule follows smoothly, about 0.6 s behind, no jitter, no
   sliding through walls beyond what the real player did.
4. Stop: remote stops within about 1 s, no drift.
5. Wave (`Raise(Wave)`) shows on the other client; spamming the key shows at
   most 2 a second.
6. Lobby chat line appears on the other client; 201 characters is refused;
   `<b>hi</b>` shows as literal text.
7. Close one client: the other removes the capsule within 5 s.
8. Pull the network on one client: HUD shows Reconnecting, game stays
   playable; restore network: back to Online without a restart.
9. Network panel (Fusion stats) screenshot: our client sends ~2 updates a
   second walking, ~0.2 a second standing.
10. Log check: no NIN, email, DOB or money in any networked field (grep the
    Fusion avatar and chat code; show the `PublicState` struct).

Put the clip, screenshots and a short `REPORT.md` in `qa/M5.1/`. Claude
verifies from those. Until then M5.1 stays UNVERIFIED.

## 8. After M5.1 (not now)

M5.3 DMs over Supabase Realtime + friends; M5.4 presence across districts
(Supabase `nl_online_touch` heartbeat, already live); M5.5 drivers in
vehicles (4 Hz, vehicle model id); M5.6 Server Mode spike before the economy
scales.

### 8.1 M5.3 friends and DMs (Claude, part 1 written 2026-11-06)

All server pieces are already live (checked 2026-11-06); no migration is needed.

- **Friends** = live RPCs `nl_friends_list`, `nl_friend_request(p_username,
  p_met_place, p_request_id)`, `nl_friend_respond(p_username, p_accept,
  p_request_id)`, `nl_friend_remove(p_username, p_request_id)`. Client:
  `Network/Unity/SupabaseFriends.cs`, reached as `NetBootstrap.Friends`.
  Errors map to `FriendError` (Core) with a short toast line each
  (`FriendErrors.Message`). The list has no user ids, only username,
  avatar_ref, met_place, since. Pending requests are not listed by the server
  yet (a later RPC).
- **DM topic** = `d:<lo uuid>:<hi uuid>`, lowercase, sorted, never yourself
  (`DmTopic.For`). Live RLS `nl_rt_dm_read` / `nl_rt_dm_write` (function
  `nl_rt_dm_ok`) allow only the two players, both signed in, NOT anonymous,
  both with a claimed username, no block either way, neither banned. So the
  M5.1 anonymous sign-in cannot DM; DMs start once the UI workstream's real
  sign-in exists.
- **Send** (part 1, done): `SupabaseDirectMessages.SendAsync` posts to
  `/realtime/v1/api/broadcast` with `private: true`, event `dm`, payload
  `{text, ts}`. Plain HTTP, so WebGL needs no socket to send. Wired as
  ChatCore's `sendDirect` through `FusionSessionHost.Create`. ChatCore still
  normalizes, blocklists and rate limits (1 line/s, 200 characters) first.
- **No sender id in the payload.** The receiver takes the sender from the
  topic (`DmTopic.TryOther`); RLS guarantees only the two players can write
  it, so it cannot be faked. Nothing private ever goes in a DM.
- **Receive** (part 2, done 2026-11-06, UNVERIFIED): `SupabaseDmReceiver`
  (added by NetBootstrap after sign-in) runs Core `RealtimeDmClient` over one
  `RealtimeSocket` (WebGL: `Assets/Plugins/WebGL/NLRealtime.jslib`; Editor and
  desktop: `ClientWebSocket`). URL `wss://<ref>.supabase.co/realtime/v1/websocket?apikey=<anon>&vsn=1.0.0`.
  Each `d:` topic is joined with `config.private = true` and the player's JWT;
  a new JWT is pushed with `access_token`; heartbeat every 25 s, link closed
  after 60 s without a reply, reconnect with full-jitter backoff. It listens to
  every player in the room plus anyone you wrote to or heard from (kept open),
  at most 60 topics. A refused join (guest, block, ban) waits 5 min before a
  retry. Guests never connect. Each `dm` broadcast goes to
  `ChatCore.OnDirect(other side of the topic, text)`.
- **Known gap:** `nl_friends_list` returns no user ids, so a friend in another
  district can only DM you once you have met in a room or written first. Fix
  later with an RPC that returns friend ids (a migration for Ted, needs review).
- **Ted's check** (once real sign-in exists): two non-anonymous test accounts
  with usernames DM each other both ways; an anonymous account's send is
  refused; a blocked pair cannot DM. Captures under `qa/M5.3/`.
