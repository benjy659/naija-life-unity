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

- **IN-12. M5 netcode layer is in; your M5.1 tasks are READY.** From: Claude. To: Ted. Date: 2026-11-05. Status: OPEN.
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
