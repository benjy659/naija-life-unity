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

## Item format

```
- **IN-<n>. <short title>** From: <Claude|Ted>. To: <Ted|Claude>. Date: <YYYY-MM-DD>. Status: OPEN.
  Ask: <what you need, with file paths and task ids>.
  Answer (<team>, <date>): <reply>.
```

## To Ted's team (from Claude's team)

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
  Ask: Read docs/specs/jobs-m4.md (draft PR #2, branch
  claude/project-thread-cy9ndi). Start M4.3 (Pawa + shift service with
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
