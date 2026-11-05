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

- **IN-3. Crew schedule and contact rule.** From: Claude. To: Ted. Date: 2026-11-05. Status: OPEN.
  Ask: Write under this item when your crews run (Winnipeg time), which lanes
  each crew covers in the Unity work, and how you want urgent items flagged.
  Claude's review loop runs 8 times a day and reads this file each run.

- **IN-2. What carries over from the web repo?** From: Claude. To: Ted. Date: 2026-11-05. Status: OPEN.
  Ask: Say which parts of benjy659/naija-life you plan to reuse here: the GLB
  models under models/, the Supabase project jdumcxvqjtgnbeuvlufq and its
  migrations (0001 to 0040), the specs under docs/ (character, transit,
  property, ride-booking, transfer, netcode), and the "Lessons for Ted's
  team" in TASKS.md. Claude's team will copy the specs and lessons that still
  apply into docs/ here once we know.

- **IN-1. Write the Unity plan down.** From: Claude. To: Ted. Date: 2026-11-05. Status: OPEN.
  Ask: This repo has only a README so far ("Naija Life, GTA-style Lagos MMO,
  Unity version (WebGL + multiplayer)"). Please add the plan as
  docs/PLAN.md: Unity version and render pipeline, WebGL target and phone
  support, multiplayer stack, which cities ship first and the launch date
  (the web plan said Lagos + Abuja on 2026-11-15), the folder layout, how to
  build and deploy the WebGL player, and who owns what between the two teams.
  Claude's team will turn it into the task board (TASKS.md) and review loop
  for this repo as soon as it is here.

## To Claude's team (from Ted's team)

(No items yet. Ted's team: add yours at the top of this section.)

## Archive

(Empty.)
