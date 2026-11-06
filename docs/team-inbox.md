# Team inbox: Claude's team and Ted's team (Unity version)

This is the two-way channel between the two teams (owner ask, Benjamin,
2026-11-05: "start helping you guys communicate and work together"). It lives
in this repo because the project moved here (Benjamin, 2026-11-05: "the new
repo"). The web version stays in benjy659/naija-life, whose
docs/claude-team-charter.md still says who owns what until a new charter is
written here. This file is where the teams talk to each other between runs.

## Rules

1. **Read this file first, every run.** Claude's review loop (8 runs a day)
   and every Ted crew batch (around 6 AM, 12 PM, 6 PM and 10 PM Winnipeg)
   start here, before TASKS.md.
2. **Answer every OPEN item addressed to you** in the same run, even if the
   answer is "not yet, because ...". Write the answer under the item, set its
   status, and push.
3. **Statuses:** OPEN (waiting on the other team), ACK (seen, being worked),
   DONE (answered or finished; say where), WAITING ON BENJAMIN (only the owner
   can decide; say what he must decide).
4. **Append, never delete.** Add new items at the top of the right section.
   Close an item by setting DONE, not by removing it. Items DONE for more than
   7 days may move to the archive at the bottom.
5. **One ask per item**, with the task id from TASKS.md when there is one.
   Bigger work still goes into TASKS.md as a task; the inbox points to it.
6. **Owner decisions** come only from Benjamin. Quote where he said it (date
   and channel). Neither team treats an inbox line as his approval.
7. **No secrets** here (keys, passwords, tokens). Say "set in Supabase
   secrets" instead.
8. Plain words, no em dashes, no emoji.
9. **See first, say first, then build** (Benjamin, 2026-11-06, project chat:
   "they must see what Ted already done first ... and communicate before
   they start building"). Before writing any code or asset, every team
   (Ted's crews and every Claude thread): (a) pulls main and reads every new
   commit since its last run, this file and TASKS.md; (b) adds a line to
   "Claims" below: date, team, task id, files it will touch; (c) checks that
   no other open claim or recent commit covers the same task or files. If one
   does, do not build it: talk under that claim first. Remove nothing; mark
   your claim DONE (with the commit) or DROPPED when finished.

## Claims (who is building what right now)

Format: `- <date> <team/thread>: <task id>, files <paths>. Status: ACTIVE | DONE <commit> | DROPPED.`

- 2026-11-06 07:30 Claude (lead thread): M1.7 close lift (review R8), file Assets/Scripts/Player/PlayerCamera.cs (new UpdateCloseLift; one line in Update, one in SnapToTarget). Checked first: Ted's 74482c6 does not touch it; the vehicles thread's driving-pivot lines are kept as they are. Status: DONE (this commit), UNVERIFIED until Ted's harness run (IN-15).
- 2026-11-06 Claude (lead thread): M1.4 camera orbit input and M1.5 grounded check, files Assets/Scripts/Player/PlayerInput.cs, PlayerCamera.cs, PlayerController.cs. Checked first: Ted's commits 44d8428 to 2935a48 touch settings and the M1 harness only, not these lines. Status: DONE (code, see IN-13), UNVERIFIED until Ted's harness run.
- 2026-11-06 Claude (multiplayer thread): M5.1b photon-auth and M5.1f migration 0041 drafts, files supabase/functions/photon-auth/*, supabase/migrations/0041_nl_street_card_by_id.sql, Assets/Scripts/Network/Unity/SupabaseSession.cs. Checked first: no Ted commit touches supabase/ or Assets/Scripts/Network/ (b2ffc61 to 2935a48 add folder metas only). Status: DONE 9334853 (drafts; Ted deploys photon-auth, applies 0041 only with Benjamin's OK, see IN-12).
- 2026-11-06 Claude (Oshodi thread): M2.1 builder, build list and EditMode tests, files Assets/Editor/World/*, Assets/Tests/EditMode/World/*, Assets/Data/Oshodi/*, tools/oshodi/*, ProjectSettings/TagManager.asset (layers 8 to 13). Checked first: Ted's 44d8428 to 2935a48 do not touch these. Status: DONE bb5316f (never compiled; Ted runs it, IN-14).
- 2026-11-06 Claude (Oshodi thread): M2.2 and M2.3 runtime code (TimeOfDay, lamp light pool, night emissives, DistrictTile, DistrictStreamer, FrameSpikeLogger, Bootstrap and Addressables setup script), files Assets/Scripts/World/*, Assets/Editor/World/OshodiBootstrapSetup.cs, Assets/Data/Oshodi/*, Assets/Settings/URP_Low|Medium|High.asset and the customRenderPipeline line of each level in ProjectSettings/QualitySettings.asset (spec 6.4 tier table). Checked first: nothing on main under Assets/Scripts/World/ but .gitkeep; no open claim on M2. Ted's part stays the Editor run and the evidence. Also touched: the LOD bias line of each quality level, the World test asmdef, DistrictStreamingTests.cs and two lines of OshodiGreyboxBuilder.cs (TimeOfDay and LampLightPool on the playtest root). Status: DONE 8d3f39c (never compiled; Ted runs it, IN-17).
- 2026-11-06 Claude (multiplayer thread): M5.1d scene builder (-executeMethod, builds Assets/Scenes/Net_M5.unity and Assets/Prefabs/Network/RemotePlayer.prefab) and M5.4 online heartbeat (live nl_online_touch / nl_heartbeat_leave), files Assets/Editor/Network/*, Assets/Scripts/Network/Unity/OnlineHeartbeat.cs, FaceCamera.cs, SupabaseSession.cs, NetBootstrap.cs. Checked first: Ted's 74482c6 and the Oshodi/lead commits since 0905ce9 do not touch these; no other claim on M5. Status: DONE (this push; never compiled).
- 2026-11-06 Claude (multiplayer thread): one shared Supabase session for the whole game (NetBootstrap.Supabase / NetBootstrap.SupabaseCfg), file Assets/Scripts/Network/Unity/NetBootstrap.cs only. Checked first: jobs thread's ShiftServiceFactory.UseServer(cfg, session) has no caller yet; I do not touch Assets/Scripts/Economy/. Status: DONE (this push; jobs thread told to call UseServer(NetBootstrap.SupabaseCfg, NetBootstrap.Supabase)).
- 2026-11-06 Claude (multiplayer thread): HUD-6 live data, the real IWalletSource reading the player's own economy_ledger rows (live, RLS owner read), files Assets/Scripts/Network/Unity/SupabaseWalletSource.cs (new), SupabaseSession.cs (GET helper), NetBootstrap.cs (register after sign-in). I do not edit Assets/Scripts/UI/ (HUD thread owns NLWallet; I only implement its interface). Checked first: no other claim on the wallet source; HUD-6 spec says the economy service registers it. Status: DONE (this push; never compiled).
- 2026-11-06 Claude (multiplayer thread): M5 compile check and Core test run outside Unity (same method as tools/jobs), files tools/network/* (new), plus fixes in Assets/Scripts/Network/** and Assets/Tests/EditMode/Network/** if it finds errors. Checked first: no other claim on these paths. Status: DONE (this push: 27/27 Core tests pass under NUnitLite, glue and builder type-check; fixed two test-file compile errors).
- 2026-11-06 17:50 Claude (multiplayer thread): M5.3 part 1, friends over the live RPCs (nl_friends_list, nl_friend_request, nl_friend_respond, nl_friend_remove) and DM sending over Supabase Realtime private broadcast (live RLS nl_rt_dm_ok), files Assets/Scripts/Network/Core/DirectMessages.cs (new), Assets/Scripts/Network/Unity/SupabaseFriends.cs, SupabaseDirectMessages.cs (new), NetBootstrap.cs, Assets/Scripts/Network/Fusion/FusionNetSession.cs and FusionSessionHost.cs (one optional sendDirect argument), tests in Assets/Tests/EditMode/Network/. Checked first: no Ted commit since af73629 touches Network or supabase/; no other claim on friends or DMs. Receiving DMs (WebSocket) is part 2, next run. Status: DONE (this push: 34/34 Core tests, glue type-checks, boundary OK; never run in Unity or against the server).
- 2026-11-06 20:50 Claude (multiplayer thread): M5.3 part 2, receive DMs over the Supabase Realtime WebSocket, files Assets/Scripts/Network/Core/Realtime*.cs and MiniJson.cs (new, pure), Assets/Scripts/Network/Unity/RealtimeSocket*.cs and SupabaseDmReceiver.cs (new), Assets/Plugins/WebGL/NLRealtime.jslib (new), NetBootstrap.cs, Assets/Scripts/Network/Fusion/FusionNetSession.cs (expose OnDirect to the receiver), tests in Assets/Tests/EditMode/Network/. Checked first: 80699ee, 2a8cce7, 1170532 since b68920c do not touch these; no Ted commit; no other claim on Realtime. Fusion file not touched after all (the receiver reaches ChatCore through Session.Chat). Status: DONE (this push: 43/43 Core tests, glue type-checks in both the WebGL and non-WebGL branches, check-all PASS; never run in Unity or against the server).
- 2026-11-06 Claude (multiplayer thread): M5 code review of anything Ted pushes under Assets/Scripts/Network/ and supabase/. No new files claimed. Status: ACTIVE.
- 2026-11-06 Claude (HUD thread): M3.4 speedo and handbrake button (vehicles thread asked), files Assets/Scripts/UI/NLDriveHud.cs (new), Assets/UI/UXML/NLHud.uxml (nl-drive block), Assets/UI/Styles/NLHud.uss (driving section), one line in Assets/Editor/UI/NLUISetup.cs. Checked first: Ted has pushed nothing under Assets/UI or Assets/Scripts/UI; the vehicles thread's code only reads DriveSession, no overlap. Status: DONE (this commit), never compiled.
- 2026-11-06 15:00 Claude (HUD thread): compile check outside Unity for the UI code (same method as tools/jobs and tools/vehicles), files tools/ui/* (new), plus compile fixes in Assets/Scripts/UI/*, Assets/Editor/UI/* if it finds any. Checked first: the jobs, vehicles, Oshodi and M5 checks (13e2920 to af73629) do not cover Assets/Scripts/UI; no Ted commit in UI. Status: DONE (this commit): PASS, NLMoney 20/20; one real fix (PointerType ambiguous in NLUIRoot), see IN-16 note.
- 2026-11-06 18:00 Claude (HUD thread): HUD-4 remainder, NLScreenGuard keep-out box + F9 overlay and the +N chooser sheet, files Assets/Scripts/UI/NLScreenGuard.cs (new), NLPrompts.cs, NLToasts.cs, NLTouchControls.cs, NLHud.cs (Esc guard), Assets/UI/Styles/NLHud.uss, one line in Assets/Editor/UI/NLUISetup.cs. Checked first: Ted and the other threads have pushed nothing under Assets/Scripts/UI or Assets/UI since 098914f. Status: DONE (this commit), passes tools/ui/compile-check.sh, never run in Unity.
- 2026-11-06 21:00 Claude (HUD thread): HUD-3 right-column stack NLHudStack (spec 5.1, rows collapse upward) and the HUD-7 overlap test (32 right-column combinations, Editor menu + batch report to qa/hud-7/), files Assets/Scripts/UI/NLHudStack.cs, Assets/Scripts/UI/NLOverlapTest.cs (new), NLJobHud.cs (shift chip and busy row go through the stack), Assets/Editor/UI/NLUISetup.cs, tools/ui/*. Checked first: nothing new under Assets/Scripts/UI or Assets/UI since 1170532; no other claim on HUD-7. Status: ACTIVE.
- 2026-11-06 Claude (vehicles thread): M3.1, M3.3, M3.5, M3.6, M3.7 code (VehicleDefinition, VehicleController, DriveSession, VehiclePlayerDriver, VehiclePromptBridge, TrafficSense, LaneGraph, TrafficDriver, TrafficSpawner, damage and contact events, lane validator) plus the M3.2 Greybox_M3 scene builder and an M3 self-check, files Assets/Scripts/Vehicles/*, Assets/Editor/Vehicles/*, Assets/Tests/EditMode/Vehicles/*, Assets/Scripts/Verification/M3*, ProjectSettings/TagManager.asset (layers 14 to 16: Vehicle, DrivenVehicle, Pedestrian; road surfaces use the Oshodi thread's Ground layer). TimeManager not changed (fixed step was already 0.02). Checked first: Ted's commits up to 74482c6 and the other threads' commits up to 3b42af9 do not touch these; the bridge uses only NLPrompts.Offer/Withdraw from the HUD thread. Ted keeps M3.4 handbrake button and speedo and every Editor run. Status: DONE (this commit, IN-19), never run in the Editor.

- 2026-11-06 14:20 Claude (vehicles thread): M3 compile check and logic test run outside Unity, files tools/vehicles/compile-check.sh, tools/vehicles/stubs-runtime.cs.txt (new). Checked first: commits since 6274ff9 (feac519 HUD-8, 8995102 review log, 2c11750 Oshodi compile check) and Ted's 3026a56 do not touch M3 files; no other claim on tools/vehicles. Result: 16/16 logic tests pass under real NUnit, runtime and editor code compile (only SaveAsPrefabAsset is newer than the 2018.1 editor reference). Status: DONE (this commit).
- 2026-11-06 Claude (jobs thread): M4.1, M4.2, M4.3 code (everything but the Editor steps) and the M4.4 migration DRAFT, files Assets/Scripts/Jobs/**, Assets/Scripts/Economy/*, Assets/Scripts/Player/PlayerNeeds.cs (new file only), Assets/Tests/EditMode/Jobs/*, Assets/Editor/Jobs/*, supabase/migrations/drafts/m4_job_shifts.sql, docs/specs/jobs-m4.md, tools/jobsim.js. Checked first: Ted's 44d8428 to 74482c6 touch settings, the M1 harness and Greybox materials only; no claim covers Jobs, Economy or these files. Ted keeps: running the tests and scene builder in the Editor, captures, applying the migration. Status: DONE d1ded08 + HUD hookup (see IN-18), UNVERIFIED until Ted's run.
- 2026-11-06 Claude (HUD and phone thread): HUD-1 to HUD-6 code (UI root, scaler, safe area, touch stick and buttons, HUD, radar, prompts, toasts, phone shell, Bank, NLMoney + tests, Editor setup menu), files Assets/Scripts/UI/*, Assets/Scripts/UI/Core/*, Assets/UI/UXML/*, Assets/UI/Styles/*, Assets/UI/Sprites/nl-balance-card.png, Assets/Plugins/WebGL/NLDevice.jslib, Assets/Editor/UI/*, Assets/Tests/EditMode/UI/*, plus one new method PlayerInput.SetLocked (phone stops the player). Checked first: Ted's commits up to 74482c6 do not touch these; no other claim on HUD; the lead's M1.4 already does the right-half touch look drag, so the HUD has no look zone of its own. Status: DONE (this commit), never compiled; Ted runs it (IN-16).
- 2026-11-06 Claude (HUD and phone thread): HUD-8 job UI pieces for the jobs thread (shift chip, shout ring on the action button, change sheet at most 35% height, end-of-shift card) in the HUD kit, swapped into Assets/Scripts/Jobs/NLJobUiBridge.cs by agreement with the jobs thread; plus the Editor fake wallet following WalletCache balances, files Assets/Scripts/UI/*, Assets/UI/*, NLJobUiBridge.cs (bridge lines only). Checked first: jobs thread 79228ae owns Assets/Scripts/Jobs and agreed in its note to me. Status: DONE (fake wallet 3b42af9; job pieces NLJobHud in the HUD-8 commit, also touched NLPrompts.cs and NLUISetup.cs), never compiled.
- 2026-11-06 Claude (Oshodi thread): M2.2 skyline shell (spec 6.5), files tools/oshodi/*, docs/specs/oshodi-greybox*, Assets/Data/Oshodi/*, Assets/Editor/World/OshodiGreyboxBuilder.cs and OshodiGreyboxData.cs, Assets/Scripts/World/NightOnly.cs. Checked first: Ted has pushed nothing since 74482c6; bcb0293, 3b42af9, 79228ae, d1ded08 (M3, M4, HUD threads) do not touch these files. Status: DONE 74cf0cd (never compiled; Ted rebuilds, IN-17).

- 2026-11-06 Claude (jobs thread, 08:37 run): M4 self-review fix in the test scene builder (asset folders made through AssetDatabase), files Assets/Editor/Jobs/M4ConductorSceneBuilder.cs. Checked first: commits since 79228ae (3b42af9 HUD wallet, bcb0293 M3, 74cf0cd skyline, d7bb77d drive HUD) do not touch jobs files; no Ted commit yet. Status: DONE (this run's commit "M4 scene builder: create asset folders through AssetDatabase").

- 2026-11-06 Claude (jobs thread): use the shared NetBootstrap Supabase session for shift RPCs, and note the client-reported Pawa risk in 0042 (multiplayer thread review), files Assets/Scripts/Economy/ShiftServiceFactory.cs, supabase/migrations/0042_nl_job_shifts.sql (header comment only). Checked first: 2a0010f touches Network only. Status: DONE (commit "M4: shift service uses the shared NetBootstrap session").
- 2026-11-06 Claude (Oshodi thread): M2.1/M2.2 QA capture runner (one batch method that builds, sets up Bootstrap and writes the V1 to V6 screenshots at every tier and QA hour plus a report under qa/M2.1/ and qa/M2.2/), files Assets/Editor/World/OshodiCaptureRunner.cs and small public ApplyNow/RefreshNow hooks in Assets/Scripts/World/TimeOfDay.cs, LampLightPool.cs, NightOnly.cs. Checked first: Ted's 3026a56 (player body) and the M3/M4/HUD/net commits since a2628f3 do not touch these files. Status: DONE 1dac422 (never compiled; IN-17 updated).
- 2026-11-06 Claude (Oshodi thread): compile check for the Oshodi C# outside Unity, files tools/oshodi/compile-check.sh and stub .cs.txt files, plus two foreach lines in OshodiCaptureRunner.cs and OshodiGreyboxTests.cs. Checked first: commits since 44641b7 (HUD, review, vehicles inbox) do not touch these. Status: DONE (this commit).

- 2026-11-06 14:40 Claude (jobs thread): M4 compile check and Jobs.Core test run outside Unity (same method as tools/vehicles), files tools/jobs/compile-check.sh, tools/jobs/stubs-*.cs.txt (new), plus any compile fixes in Assets/Scripts/Jobs/**, Economy/*, PlayerNeeds.cs, Tests/EditMode/Jobs/*, Editor/Jobs/*. Checked first: commits since 1aa6e7f (Ted's 3026a56 player body, feac519 HUD-8, 8995102, 2c11750, 13e2920) touch only NLJobUiBridge's Show* lines in my area (reviewed, fine); no other claim on tools/jobs. Status: DONE aed1e90 + 5e1f906. Run by the vehicles thread at 5e1f906: 28/28 Jobs.Core tests pass under real NUnit; runtime glue and scene builder compile clean (one false alarm fixed: Unity 6 awaits AsyncOperation, the 2021.3 refs needed a stub). Hand check done: every stub mirrors the real signature on main (NLPrompts, NLToasts, NLWallet, NLJobHud, NetBootstrap, SupabaseSession, PlayerCamera).

- 2026-11-06 17:25 Claude (lead thread): M1 compile check outside Unity for the player code (PlayerInput, PlayerController, PlayerCamera, M1VerifyHarness), files tools/player/* (new) only. Checked first: 2c11750 to 098914f add compile checks for World, M3, M4, M5 and UI; none covers Assets/Scripts/Player or the M1 harness; Ted has pushed nothing since 3026a56. Status: DONE 58fda20 (compiles; still UNVERIFIED in Unity 6 until Ted's harness run, IN-13/IN-15).

- 2026-11-06 18:30 Claude (lead thread): one runner for every outside-Unity compile check, file tools/check-all.sh (new) only. Checked first: 86d093c to 1170532 (M5.3 friends/DMs, HUD-4 guard) do not touch tools/*/compile-check.sh; no claim covers a runner. Status: DONE 80699ee (all six areas plus net-boundary PASS in 8 s; Ted can run it before pushing).

## Item format

```
- **IN-<n>. <short title>** From: <Claude|Ted>. To: <Ted|Claude>. Date: <YYYY-MM-DD>. Status: OPEN.
  Ask: <what you need, with file paths and task ids>.
  Answer (<team>, <date>): <reply>.
```

## To Ted's team (from Claude's team)

- **IN-20. The new body plays in T-pose: it needs idle, walk, run clips (M1.8).** From: Claude. To: Ted. Date: 2026-11-06. Status: OPEN.
  Reviewed 3026a56 (body_m on the Player, harness 0 failures, thanks). Your
  note says the body has no Avatar and renders in bind pose until clips
  exist, so on screen the player now slides along in a T-pose. Benjamin's
  standing rule is that characters walk like normal humans (no gliding, legs
  not spread), so this is the next most visible gap.
  Ask: M1.8 on TASKS.md. Make body_m a Humanoid rig with an Avatar, add idle,
  walk, jog, run, sprint clips (free or generated, no paid assets without
  Benjamin's yes), and fill PlayerLocomotion.controller's blend tree at
  0 / 2.4 / 4.2 / 6.5 / 8.5 m/s. Until then, set BodyM inactive and keep the
  capsule. Post a short clip of walk, jog and sprint under qa/m1.8/.
  Also: (1) Supabase now shows 23 policies and 78 definer functions (was 22
  and 76 on 2026-11-05). Please list here what you applied; 0041 and 0042
  are not applied, good. (2) IN-13 and IN-15 still need your M1 phone and
  WebGL runs.

- **IN-19. M3 driving and traffic code is on main; please compile, test and play it (M3.1 to M3.7).** From: Claude (vehicles thread). To: Ted. Date: 2026-11-06. Status: OPEN.
  Claude took every non-Editor part of M3 (see TASKS.md M3): the vehicle
  controller and the danfo and saloon definitions, getting in and out, the
  Drive / Get out prompt on the HUD, one-stick touch driving, traffic
  sensing and bot drivers, damage and contact events, the lane check, 16
  EditMode tests, a scene builder and a self-check. It compiles against
  Unity reference assemblies and the 16 logic tests pass outside Unity, but
  it has never run in the Editor. Update 2026-11-06 11:17 run: your
  3026a56 ran the M1 harness with this code in the tree, so it looks like it
  compiles (Unity will not enter Play mode otherwise); say so if it did not.
  Please:
  1. Pull main and open the project. Paste any compile error here (files
     under Assets/Scripts/Vehicles, Assets/Scripts/Verification/M3*,
     Assets/Editor/Vehicles).
  2. Check Project Settings > Tags and Layers shows 14 Vehicle,
     15 DrivenVehicle, 16 Pedestrian.
  3. Test Runner > EditMode > NaijaLife.Vehicles.Tests > Run All. All 16
     must pass. Screenshot to qa/m3.1/.
  4. Menu Naija Streets > M3 > Build test scene (batch: -executeMethod
     NaijaLife.Vehicles.EditorTools.M3SceneBuilder.BuildBatch). Commit what
     it makes: Assets/Scenes/Greybox_M3.unity, Assets/Settings/Vehicles/*,
     Assets/Prefabs/Vehicles/*, Assets/Art/Materials/M3/*,
     Assets/Art/Models/M3/*. Screenshots to qa/m3.2/.
  5. Menu Naija Streets > M3 > Run self-check (batch: -executeMethod
     NaijaLife.Vehicles.EditorTools.M3VerifyRunner.RunBatch). It plays the
     scene by itself (enter, loop drive over the bridge, reverse, spin,
     exit, then 120 s of traffic) and writes qa/m3-selfcheck/<time>/REPORT.md
     with screenshots. Commit that folder, pass or fail.
  6. Then drive it yourself at 1280x800 and 390x844 (WebGL too): F walks you
     to the door and in (or tap the prompt / E when the HUD is in the
     scene), WASD or arrows drive, Space handbrake, F gets out under
     2 m/s, F3 shows the debug overlay. Clips per TASKS.md to qa/m3.3/,
     qa/m3.5/, qa/m3.6/.
  Notes: the player body is hidden while seated (no sit pose yet). Gamepad
  handbrake is RB because A/South accepts the HUD prompt. If the wheels feel
  wrong, report what you saw; please do not retune in the scene, the numbers
  live in VehicleDefinition presets so Claude can fix them in code.
  Your M3 Editor part left after that: M3.4 handbrake button and speedo in
  the HUD layout.
- **IN-18. M4 jobs code is on main; please compile, test and play it (M4.1 to M4.4).** From: Claude. To: Ted. Date: 2026-11-06. Status: OPEN.
  Claude took all the non-Editor work of M4 so you only do Editor steps:
  job slots and bot handover, the danfo conductor shift, Pawa, the shift
  service (Editor stub + Supabase client), 25 Edit Mode tests, a test scene
  builder, and the migration draft 0042 (commit d1ded08 and the HUD hookup
  after it). Nothing has been compiled. Please:
  1. Open the project; paste any compile errors here (files under
     Assets/Scripts/Jobs, Economy, Player/PlayerNeeds.cs, Editor/Jobs).
  2. Test Runner > EditMode > NaijaLife.Jobs.Tests > Run All. All 25 must
     pass. Screenshot to qa/m4.3/.
  3. Menu Naija Streets > M4 > Build conductor test scene (batch:
     -executeMethod NaijaLife.JobsGlue.EditorTools.M4ConductorSceneBuilder.BuildBatch).
     It makes Assets/Scenes/M4_ConductorTest.unity: 3 bays, bot conductors,
     loop route, your Player prefab with PlayerNeeds + PlayerJobs.
  4. Play it at 1280x800 and 390x844 and capture (spec section 11): the
     buses leaving on their own; walking up to a bot and pressing E (the bot
     walks aside, never jumps); one full trip (Space to shout on gold, ride
     at the door, 1/2/3 to give change); End shift (X) after 3 trips and 10
     minutes (paid, wallet shows TEST); End shift after 2 trips (no pay);
     F6 sets Pawa to 20 (clock in refused), F7 fills it; walk 9 m away (the
     bot walks back). Clips to qa/m4.1/ and qa/m4.2/.
  5. supabase/migrations/0042_nl_job_shifts.sql is a DRAFT: review it, apply
     it when you are happy (it adds tables and functions only, no change to
     live objects), run the checks at the bottom, post results to qa/m4.4/.
     Claude does not apply migrations.
  6. Add M4_ConductorTest to the build list for the WebGL check, or tell us
     if you would rather keep it out of builds.
  Known limits: greybox capsules for people; the shout ring, change sheet,
  shift chip and summary card draw with a plain placeholder until the HUD
  kit has them; prompts and toasts already use NLPrompts/NLToasts when the
  HUD is in the scene. Sign-in is not wired, so play uses the Editor stub.

- **IN-17. Oshodi lighting, quality tiers and streaming code is written; please run it (M2.2, M2.3).** From: Claude. To: Ted. Date: 2026-11-06. Status: OPEN.
  Ask: Claude took every M2.2 and M2.3 part that does not need the Editor
  (commit 8d3f39c). What Claude wrote: Assets/Scripts/World/ (new assembly
  NaijaLife.World: TimeOfDay, LampLightPool, DistrictTile, DistrictStreamer,
  FrameSpikeLogger), Assets/Editor/World/OshodiBootstrapSetup.cs,
  Assets/Settings/URP_Low|Medium|High.asset wired into each quality level
  (spec 6.4 values; Medium stays the default), and DistrictStreamingTests.
  The greybox builder now puts TimeOfDay and LampLightPool on the playtest
  root. None of it has been compiled. Editor steps left for you, in order:
  (1) after IN-14, open the project and fix or report any compile error here
  (do not patch scenes by hand); (2) rebuild with Naija Streets > Build
  Oshodi greybox; (3) run Naija Streets > Set up Oshodi streaming (Bootstrap)
  (batch: `-executeMethod NaijaLife.World.Editor.OshodiBootstrapSetup.SetupBatch`),
  which makes the Addressables group, the tile asset and Bootstrap.unity;
  (4) add a global Volume with Bloom per tier (off on Low, low on Medium, on
  on High), which is easier in the Editor than in code; (5) run all
  NaijaLife.World.Tests; (6) M2.2 evidence: V1 and V6 at 07:00, 13:00,
  17:30 and 21:00 (TimeOfDay buttons, top left) on all three tiers, under
  qa/M2.2/; (7) M2.3 evidence: WebGL development build from Bootstrap, walk
  north from z -790 until the overlay says Oshodi_Core Awake, then back south
  past 700 m until Asleep; screenshot the overlay lines and the console
  [FrameSpike] lines under qa/M2.3/. Update 2026-11-06 (74cf0cd): the
  skyline shell (spec 6.5) is now in the layout and the builder (two merged
  meshes, Skyline group); step (2) builds it, so V1 and V6 should show it, with
  window lights at 21:00 only.
  Update 2026-11-06 (1dac422): to save you time, one batch run now does steps (2),
  (3) and a first pass of (6): `Unity -batchmode -projectPath . -executeMethod
  NaijaLife.World.Editor.OshodiCaptureRunner.RunAllBatch -logFile -` (needs
  the GPU, so no -nographics; menu Naija Streets > Capture Oshodi QA shots).
  It writes camera-only frames of V1 to V5 at both sizes and V1/V6 on every
  tier and QA hour, plus capture-report.md, under qa/M2.1/ and qa/M2.2/. Push
  those and Claude reviews them straight away; the WebGL walk shots and the
  M2.3 streaming walk still follow.
  Update 2026-11-06 14:12 run: all Oshodi C# (Assets/Scripts/World,
  Assets/Editor/World, Assets/Tests/EditMode/World) now compiles against
  Unity reference assemblies (`bash tools/oshodi/compile-check.sh`). Unity 6
  can still differ, so step (1) stays: paste any compile error here.
- **IN-16. HUD, joystick, phone and Bank code is on main: please compile and capture (HUD-1 to HUD-6).** From: Claude. To: Ted. Date: 2026-11-06. Status: OPEN.
  Claude wrote all the code for HUD-1 to HUD-6 (UI Toolkit; files in the
  Claims line above). Nothing has been compiled. Only Editor steps are left
  for you:
  1. Open the project, fix or report compile errors here (paste them).
  2. Run the Edit Mode tests (NaijaLife.UI.Tests, NLMoney). All must pass.
  3. Open Greybox_M1, menu Naija Streets > UI > Add HUD to open scene, save.
     (Batch: -executeMethod NaijaLife.UI.Editor.NLUISetup.AddToGreyboxBatch.)
  4. Check the SVG icons import as images (wallet, phone, camera icons show
     on the HUD). If they show blank, tell us here; do not add a package
     without saying which.
  5. Play at 390x844, 844x390, 1280x800, 1920x1080 (Device Simulator for the
     phone sizes). Use Naija Streets > UI > Test menus: pay a shift (wallet
     counts up, money toast), offer 3 prompts (one prompt and a +2 chip),
     go offline then open the Bank (timeout state at 8 s), low vitals.
     Open the phone (P key or phone button), open Bank, Back, close.
  6. Capture screenshots of each size and a short clip of the joystick on a
     real phone in a WebGL build, under qa/hud-1/ to qa/hud-6/.
  Known limits: Inter font assets are not made yet (default font for now,
  check the naira sign shows); the M1.6 stock stick is not needed any more,
  NLTouchControls replaces it.

- **IN-15. M1 harness reviewed: camera fix proven; three things left for M1.** From: Claude. To: Ted. Date: 2026-11-06. Status: OPEN.
  Reviewed 74482c6 and qa/m1/results-desktop-gui.txt. Proven in the Editor:
  strafe D and backpedal S held the camera yaw at 0.00 deg while the player
  moved 25 m (the R1 fix works), jog 4.0, sprint 8.25, walk 2.3 m/s (R2
  fix), wall collision, ramp, occlusion pull-in. Your run included Claude's
  M1.4/M1.5 code, so it compiles. Good work.
  New from Claude (no Editor needed from you except the run): M1.7 close lift.
  Your 08-occlusion.png shows the pull-in at 2 m with the head filling the
  screen; the camera now tilts down over the head while pulled in.
  Ask: (1) re-run the harness with M1.7 and check 08-occlusion.png shows the
  whole player; (2) add the IN-13 checks (plain mouse move does not orbit,
  RMB drag does, step off Wall_Low falls with gravity); (3) run RunPhone
  390x844 and one WebGL build, and commit both results and shots under qa/m1/.
  Then M1 can be called done.

- **IN-14. Oshodi builder and tests are written; please run them (M2.1).** From: Claude. To: Ted. Date: 2026-11-06. Status: OPEN.
  Ask: Claude wrote the M2.1 code so you do not have to: Assets/Editor/World/
  (OshodiGreyboxBuilder, menu Naija Streets > Build Oshodi greybox, batch
  method NaijaLife.World.Editor.OshodiGreyboxBuilder.BuildAllBatch) and
  Assets/Tests/EditMode/World/ (4 EditMode tests). Layers Ground, Building,
  Prop, CameraIgnore, PlayerBlocker, Marker are added in TagManager (8 to 13).
  It has never been compiled. Please open the project, run the build and the
  tests, and commit the generated scenes, materials and .meta files. If
  anything fails to compile or a test fails, write the exact error under this
  item and do not work around it in the scene; Claude fixes the code or the
  layout. Then play District_Oshodi_Core (it has a playtest player and camera)
  and post V1 to V5 shots and the footbridge clip under qa/M2.1/.

- **IN-13. Claude took M1.4 and M1.5 off your plate; please run your harness.** From: Claude. To: Ted. Date: 2026-11-06. Status: OPEN.
  Benjamin (2026-11-06): help Ted's team go faster. Claude coded M1.4 (camera
  orbit: right-mouse drag on desktop, right-half drag on phone above the
  bottom 35 percent button band, gamepad right stick; PlayerInput.OrbitDegrees
  feeds PlayerCamera.AddOrbitDegrees) and M1.5 (grounded probe now allows
  only 5 cm plus skin below the feet). Claude cannot compile Unity here.
  Ask: open the project, fix any compile error you hit (tell us in a reply),
  run M1VerifyRunner desktop and phone, and add three checks to the harness:
  plain mouse movement does not move the camera; RMB drag does; stepping off
  Wall_Low falls with gravity. In the WebGL build, check that right-click
  does not open the browser menu. Commit the run log and screenshots under
  qa/m1/ (your harness says run 8 passed, but nothing is in the repo yet).
  Also good work on M0.1 and the harness; both reviewed, no issues found.

- **IN-12. M5.1b and M5.1f drafts are ready for you.** From: Claude. To: Ted. Date: 2026-11-06. Status: OPEN.
  Ask: (1) Deploy supabase/functions/photon-auth (first Edge Function in the
  project) with --no-verify-jwt, set secret PHOTON_AUTH_KEY in Supabase
  secrets, point Photon custom auth at .../functions/v1/photon-auth?k=<key>,
  and turn off anonymous Photon clients. Local check: node
  --experimental-strip-types supabase/functions/photon-auth/test.mjs.
  (2) Review supabase/migrations/0041_nl_street_card_by_id.sql. Do NOT apply
  it until Benjamin says yes. (3) While Unity is open: Window > General >
  Test Runner > EditMode > Run All, and post the result under qa/M5.1/
  (NaijaLife.Network.Tests, 27 tests). Your first Editor open already showed
  the Network code compiles; thank you.
  Update (Claude, 2026-11-06 run 2): M5.1d no longer needs hand wiring. Run
  -executeMethod NetM5SceneBuilder.Build (or menu Naija Streets > Network >
  Build Net_M5 scene); it makes Assets/Scenes/Net_M5.unity and
  Assets/Prefabs/Network/RemotePlayer.prefab. NetM5SceneBuilder.BuildWebGL
  builds it to Builds/WebGL-Net_M5. Paste the Supabase anon key on Net >
  NetBootstrap locally; do not commit it. Also new: M5.4 OnlineHeartbeat
  (players online count, live RPCs). Please post compile errors here if any.
  Update (Claude, 2026-11-06 run 4): HUD-6 live wallet. After sign-in,
  NetBootstrap sets NLWallet.Source = SupabaseWalletSource, which reads the
  player's own economy_ledger rows (live table, owner-read policy): balance =
  newest balance_after, history newest first. Read only. To keep the Editor
  fake wallet, untick useServerWallet on Net > NetBootstrap. Ted: in Net_M5,
  open the phone Bank and capture it under qa/M5.1/ (a new anonymous player
  shows 0 until a server grant row exists).
  Update (Claude, 2026-11-06 run 6): M5.3 part 1. NetBootstrap.Friends
  (live friend RPCs) and DM sending over Supabase Realtime are in; Fusion
  host Create() now takes an optional DM sender (fix it with the rest in
  M5.1c). Nothing to do for M5.1; DMs need non-anonymous accounts, so the
  two-account check (spec 8.1) waits for the real sign-in. 34/34 Core tests
  now (7 new in DirectMessageTests.cs).
  Update (Claude, 2026-11-06 run 7): M5.3 part 2, receiving DMs over the
  Supabase Realtime WebSocket (SupabaseDmReceiver, added by NetBootstrap;
  new Assets/Plugins/WebGL/NLRealtime.jslib). Guests never connect. Nothing to
  do now; the two-account check in spec 8.1 covers it. 43/43 Core tests.

- **IN-11. Everything is on main now; push yours every check-in.** From: Claude. To: Ted. Date: 2026-11-05. Status: OPEN.
  Benjamin said on 2026-11-05 (this project's chat) that your team cannot see
  Claude's work and he is not sure Claude sees yours. Fixed on Claude's side:
  all Claude work (M1 fixes, M2 to M5 specs, HUD spec, M5.0 netcode layer,
  this inbox) is merged into main, and Claude's threads push straight to main
  from now on. Claude reads your commits and this inbox every review run.
  Ask: (1) At every check-in, push what you have, even unfinished (mark it
  WIP in the commit message), so it is never only on your VM. (2) Write one
  status line under this item each check-in: what you did, what is next, and
  whether the Unity Editor runs yet. (3) Answer IN-4 to IN-10 with ACK or a
  reason.

- **IN-10. M5 netcode layer is in; your M5.1 tasks are READY.** From: Claude. To: Ted. Date: 2026-11-05. Status: OPEN.
  Ask: Read docs/specs/netcode-m5.md. Claude wrote the abstraction layer
  (M5.0) and a Fusion Shared adapter skeleton. Nothing has been compiled:
  please do M5.1a (Fusion 2 SDK, free Photon app, region eu, define
  NL_FUSION) and M5.1c (fix compile errors in Assets/Scripts/Network/Fusion/
  against the real SDK and run the Edit Mode tests) as soon as M0.1 is done,
  and post the test run under qa/M5.1/. Also M5.1b (first Edge Function,
  photon-auth) and M5.1f (migration nl_street_card_by_id) for Claude's
  review; do not apply M5.1f until Benjamin says yes. Tell us here how the
  Photon App ID is kept out of git. Gameplay code must never use Fusion
  directly: run tools/check-net-boundary.sh before you push.

- **IN-9. M3 driving and traffic spec is ready.** From: Claude. To: Ted. Date: 2026-11-05. Status: OPEN.
  Spec: docs/specs/vehicles-m3.md. Tasks M3.1 to M3.7 in TASKS.md are yours,
  in that order, after M0.1. M3 does not wait for the Oshodi greybox: build a
  Greybox_M3 test scene first (spec section 2). Claude already changed
  PlayerCamera for driving (M3.C): call SetDrivingMode(true, distance, pivot)
  then SetFollowTarget(vehicle, false) on enter, and the reverse on exit;
  never SnapToTarget on enter or exit. The driven car's colliders go to layer
  DrivenVehicle so the lens is never blocked by its own car.
  Two rules from the web game that must not come back: traffic must sense
  every vehicle and person with a real physics cast (cars drove through each
  other, L-83), and a bot never drives through anything to get unstuck (L-97).
  Ask: reply ACK, and say if any spec number or rule cannot work in Unity
  6.3 so Claude can change the spec before you build.

- **IN-8. M2 Oshodi greybox spec ready.** From: Claude. To: Ted. Date: 2026-11-05. Status: OPEN.
  Ask: Read docs/specs/oshodi-greybox.md (on main). M2.1 to M2.3 are READY in TASKS.md once M0.1 lands. Build the
  scene FROM docs/specs/oshodi-greybox-layout.json with an Editor builder
  (spec section 8), do not place things by hand; to move anything, change
  tools/oshodi/oshodi-layout.js and run it (it checks lanes, pavements, the
  footbridge climb and the M4 bays). Two things only you can do: (1) put the
  real OSM name of the side street at the junction and of the next danfo stop
  south of Oshodi on Agege Motor Road into the script (spec 3.2 and 4.6);
  (2) post evidence under qa/M2.<n>/ as spec section 11 lists. Reply with
  when you expect M2.1 to start, given IN-4 (Unity Editor).

- **IN-7. HUD and phone spec is ready: HUD-1 to HUD-7.** From: Claude. To: Ted. Date: 2026-11-05. Status: OPEN.
  Claude's UI workstream wrote docs/specs/hud-and-phone.md with mockups in
  docs/specs/img/hud/. Decision: UI Toolkit for all screen UI (reasons in
  spec section 2). Already in the repo for you: brand tokens
  Assets/UI/Styles/NLTokens.uss, 99 line icons Assets/UI/Icons/, shield logo
  Assets/UI/Brand/naija-life-mark.svg. Note the owner's 2026-10-28 rule: no
  gold or yellow anywhere in UI, accent is green #2EE59D (this overrides the
  gold CTA line in DESIGN-INVENTORY section 9). M1.6 can still ship the stock
  stick for M1 testing; HUD-2 replaces it.
  Ask: Reply ACK, then start HUD-1 right after M0.1. Post captures at 390x844,
  844x390, 1280x800 and 1920x1080 under qa/hud-1/. If UI Toolkit SVG import
  or the naira glyph fails on WebGL, say so here first, before building on it.

- **IN-6. M4 jobs spec ready.** From: Claude. To: Ted. Date: 2026-11-05. Status: OPEN.
  Ask: Read docs/specs/jobs-m4.md (on main). Start M4.3 (Pawa + shift service with
  LocalShiftStub) and M4.4 (shift RPC migration from spec section 9) now,
  because they do not wait on M2 or M3. Post the M4.4 SQL for Claude's review
  before applying anything. M4.1 and M4.2 come after M0.1 and the M1 play
  test. Evidence under qa/m4.<n>/.

- **IN-5. Claude read your plan: what we understood and what we are taking.** From: Claude. To: Ted. Date: 2026-11-05. Status: OPEN.
  Understood: Unity 6.3 LTS 6000.3.28f1, URP Forward, Linear, WebGL first;
  the browser game is frozen and only the design carries over; Nov 15 launch
  is off until M6 is played; milestones M1 to M6 as in docs/MIGRATION-PLAN.md;
  you build, Claude specs, reviews and fixes. Benjamin named Claude senior
  lead engineer on 2026-11-05 (project chat).
  Done tonight: M1 code review (docs/reviews/m1-review.md), two fixes pushed
  (R1 camera circling while strafing, R2 keyboard ran by default), M5.2 verdict
  (Fusion 2 Shared + Supabase confirmed, fixed EU region, abstraction layer
  first). New READY tasks for you in TASKS.md: M0.1, M1.4, M1.5, M1.6.
  Claude's next: M2 Oshodi greybox spec, then specs for M3 and M4.
  Ask: Reply ACK, and start with M0.1 because nothing can be verified without
  it.

- **IN-4. Who can run Unity, and when?** From: Claude. To: Ted. Date: 2026-11-05. Status: OPEN.
  Ask: Your M1 notes say the Editor install on your VM is in progress.
  Claude's cloud container cannot run the Unity Editor. Please say when the
  Editor and a WebGL build are working on your side, and post screenshots or a
  short clip of every milestone check under qa/<task id>/ so Claude can verify
  from them. Until then every M1 result stays UNVERIFIED.

- **IN-3. Crew schedule and contact rule.** From: Claude. To: Ted. Date: 2026-11-05. Status: ACK.
  Ask: Write under this item when your crews run (Winnipeg time), which lanes
  each crew covers in the Unity work, and how you want urgent items flagged.
  Claude's review loop runs 8 times a day and reads this file each run.
  Answer (Claude, 2026-11-05): TASKS.md says your team checks at about 6 AM,
  12 PM, 6 PM and 10 PM Winnipeg. Still OPEN for lanes and the urgent flag.

- **IN-2. What carries over from the web repo?** From: Claude. To: Ted. Date: 2026-11-05. Status: DONE.
  Ask: Say which parts of benjy659/naija-life you plan to reuse here: the GLB
  models under models/, the Supabase project jdumcxvqjtgnbeuvlufq and its
  migrations (0001 to 0040), the specs under docs/ (character, transit,
  property, ride-booking, transfer, netcode), and the "Lessons for Ted's
  team" in TASKS.md. Claude's team will copy the specs and lessons that still
  apply into docs/ here once we know.
  Answer (Claude, 2026-11-05): DONE. docs/CLAUDE-BRIEFING.md section 2 and
  docs/DESIGN-INVENTORY.md section 11 answer it: design only, no code.

- **IN-1. Write the Unity plan down.** From: Claude. To: Ted. Date: 2026-11-05. Status: DONE.
  Ask: This repo has only a README so far ("Naija Life, GTA-style Lagos MMO,
  Unity version (WebGL + multiplayer)"). Please add the plan as
  docs/PLAN.md: Unity version and render pipeline, WebGL target and phone
  support, multiplayer stack, which cities ship first and the launch date
  (the web plan said Lagos + Abuja on 2026-11-15), the folder layout, how to
  build and deploy the WebGL player, and who owns what between the two teams.
  Claude's team will turn it into the task board (TASKS.md) and review loop
  for this repo as soon as it is here.
  Answer (Claude, 2026-11-05): DONE. The plan is docs/MIGRATION-PLAN.md with
  docs/DESIGN-INVENTORY.md, docs/CLAUDE-BRIEFING.md and TASKS.md (commit
  6a77c04). No separate PLAN.md needed.

## To Claude's team (from Ted's team)

(No items yet. Ted's team: add yours at the top of this section.)

## Archive

(Empty.)

- **IN-20 (HUD thread to Ted, 2026-11-06): M3.4 driving HUD is in code.**
  NLDriveHud shows a km/h speedo and, on touch, a BRAKE button bottom-right,
  only while DriveSession.State is Driving. The brake feeds
  <Gamepad>/rightShoulder (a child object "NL Handbrake Control"). The
  on-foot Sprint, camera and phone buttons hide while driving; the stick
  stays. Re-run Naija Streets > UI > Add HUD to open scene on Greybox_M3 to
  pick up the new component. For M3.4 captures: 390x844 WebGL, driving, show
  that nothing crosses the centre third. Never compiled; paste errors here.

- **IN-16 note (HUD thread, 2026-11-06 15:20): UI code now passes a compile check outside Unity.**
  `bash tools/ui/compile-check.sh` (mono; same method as tools/jobs) compiles
  Assets/Scripts/UI (player and Editor builds) and Assets/Editor/UI against
  the 2021.3 reference dlls and runs the NLMoney tests (20/20 pass). It found
  one real error: `PointerType` in NLUIRoot.cs was ambiguous between
  UnityEngine and UnityEngine.UIElements (now fully named). Three local
  functions became lambdas and NLMoney lost its digit separators only
  because mcs cannot read them. Unity 6 is still the real check (IN-16 steps).

- **IN-21 (HUD thread to Ted and the jobs thread, 2026-11-06): HUD-8 job UI is in code.**
  From: Claude. To: Ted. Status: OPEN.
  New Assets/Scripts/UI/NLJobHud.cs, styles at the end of NLHud.uss. The
  conductor shift now draws through it (NLJobUiBridge forwards the shift chip,
  shout ring, change sheet, busy line and end-of-shift card; JobDebugUi is
  only a fallback when NLJobHud is missing). Shift chip and busy line sit
  under the wallet; the shout button takes the on-foot buttons' corner; the
  change sheet and shift card are a bottom sheet, at most 35% of the screen
  height. Keys: Space or gamepad South shouts, 1 2 3 (West, North, East)
  pick change, E / Enter / South close the card. NLPrompts waits while a
  sheet is up. Steps for Ted: re-run Naija Streets > UI > Add HUD to open
  scene (adds NLJobHud), then in Play use the new Test menus (job shout,
  job change sheet, job shift card, job busy line, clear job UI) and capture
  each at 390x844, 844x390, 1280x800, 1920x1080 under qa/hud-8/. Never
  compiled; paste any errors here.

- **IN-22 (HUD thread to Ted, 2026-11-06): HUD-4 screen guard and +N chooser are in code.**
  From: Claude. To: Ted. Status: OPEN.
  NLScreenGuard (new, added by Add HUD to open scene) projects the player's
  CharacterController to a keep-out box (+16 px). The prompt moves to top
  centre while its home spot touches it, the toast lane hides and waits,
  and the stick rests 24 px further left and down. Debug builds: F9 draws
  every rect (keep-out green, stick blue, buttons, prompt, toasts). The "+N"
  chip and Tab now open a bottom sheet with every offer as a 52 px row;
  tap or 1-9 chooses, Esc / Tab / gamepad East closes. Steps: re-run Add HUD
  on Greybox_M1, Test: offer 3 prompts, tap +2, then switch to the Top
  camera view and press F9; capture at the 4 sizes under qa/hud-4/.
