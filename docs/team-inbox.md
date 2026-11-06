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

- 2026-11-06 Claude (lead thread): M1.4 camera orbit input and M1.5 grounded check, files Assets/Scripts/Player/PlayerInput.cs, PlayerCamera.cs, PlayerController.cs. Checked first: Ted's commits 44d8428 to 2935a48 touch settings and the M1 harness only, not these lines. Status: DONE (code, see IN-13), UNVERIFIED until Ted's harness run.
- 2026-11-06 Claude (multiplayer thread): M5.1b photon-auth and M5.1f migration 0041 drafts, files supabase/functions/photon-auth/*, supabase/migrations/0041_nl_street_card_by_id.sql, Assets/Scripts/Network/Unity/SupabaseSession.cs. Checked first: no Ted commit touches supabase/ or Assets/Scripts/Network/ (b2ffc61 to 2935a48 add folder metas only). Status: DONE 9334853 (drafts; Ted deploys photon-auth, applies 0041 only with Benjamin's OK, see IN-12).
- 2026-11-06 Claude (Oshodi thread): M2.1 builder, build list and EditMode tests, files Assets/Editor/World/*, Assets/Tests/EditMode/World/*, Assets/Data/Oshodi/*, tools/oshodi/*, ProjectSettings/TagManager.asset (layers 8 to 13). Checked first: Ted's 44d8428 to 2935a48 do not touch these. Status: DONE bb5316f (never compiled; Ted runs it, IN-14).
- 2026-11-06 Claude (Oshodi thread): M2.2 and M2.3 runtime code (TimeOfDay, lamp light pool, night emissives, DistrictTile, DistrictStreamer, FrameSpikeLogger, Bootstrap and Addressables setup script), files Assets/Scripts/World/*, Assets/Editor/World/OshodiBootstrapSetup.cs, Assets/Data/Oshodi/*. Checked first: nothing on main under Assets/Scripts/World/ but .gitkeep; no open claim on M2. Ted's part stays the Editor run and the evidence. Status: ACTIVE.
- 2026-11-06 Claude (multiplayer thread): M5 code review of anything Ted pushes under Assets/Scripts/Network/ and supabase/. No new files claimed. Status: ACTIVE.

## Item format

```
- **IN-<n>. <short title>** From: <Claude|Ted>. To: <Ted|Claude>. Date: <YYYY-MM-DD>. Status: OPEN.
  Ask: <what you need, with file paths and task ids>.
  Answer (<team>, <date>): <reply>.
```

## To Ted's team (from Claude's team)

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
