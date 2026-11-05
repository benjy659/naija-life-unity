# Naija Life — Unity Migration: Game Design Inventory

**Source read (read-only):** `~/workspace/naija-life/docs/` (all spec files),
`~/workspace/naija-life/TASKS.md` (8,387 lines — the assignment board),
`~/workspace/naija-life/research/` (police, aviation, transport, phone, social specs), and
`~/workspace/goals/naija-life-game-gta-style-lagos-mmo-prototype/GOAL.md`.

**Scope note:** the browser HTML/JS game is FROZEN (owner decision Nov 5, 2026). No code
carries over — only the design below. Launch target carried over: **Lagos + Abuja playable
at minimum**; Port Harcourt first city after Lagos. Owner quality-first directive
(Oct 16, 2026) governs everything: stability and sense before new features; no fix may
break walking, driving, phone, jobs, or entering buildings.

---

## 1. WORLD

**What it is:** A GTA-style open-world MMO of Nigeria, built from real OpenStreetMap road
layouts and real photos. Compressed/recognizability mapping (not 1:1 scale) — real names,
game order/geography where they disagree.

### Lagos (launch city 1)
Districts, each with a required "must read" identity:

| District | Identity |
|---|---|
| **Oshodi** (start) | Transport Interchange Terminals 1–3 (curved glass), pedestrian bridges full of people, yellow danfos 3-deep at the park, conductors shouting routes, umbrellas/tables under bridges, NEPA poles with tangled wires, gutters with slab covers, posters on pillars |
| **Makoko** | Stilt houses (weathered timber, rusted zinc) over dark water, canoes everywhere, fish-smoking smoke, floating school, Third Mainland Bridge behind |
| **Ikeja** | Computer Village (Otigba St stall maze), Allen Ave banks/eateries, Ikeja City Mall (Obafemi Awolowo Way), Alausa Secretariat forecourt, Ikeja Under Bridge, MMIA airport |
| **Lekki / VI / Ikoyi** | Glass towers (Ozumba Mbadiwe, Adeola Odeku), Lekki toll plaza, Admiralty Way palms + gated estates, Lekki–Ikoyi Link Bridge (cable-stayed, pylon), Landmark Beach (sand, water, beach huts). Must read visibly richer/cleaner than the mainland |
| **Surulere** | National Stadium (weathered concrete bowl, floodlights), Ojuelegba roundabout/bridge, Bode Thomas St shops, National Theatre (Iganmu) |
| **Yaba** | UNILAG main gate, YabaTech, Tejuosho Market, tech-hub row |
| **Lagos Island** | Balogun Market, Tafawa Balewa Square (TBS), Eko Atlantic coast |
| Key roads/structures | Third Mainland Bridge, Ikorodu Rd, Herbert Macaulay Way, Agege Motor Rd, Apapa–Oshodi Expressway, Lekki–Epe Expressway, Ojuelegba flyover, footbridges |

**Rule (L-1):** every landmark sits in its real district — no landmark outside its district;
each reachable by road. Every road has kerbs + pavement both sides; nothing in a driving
lane, ever.

### Abuja (launch city 2 — launch blocker)
Projection from OSM (§A.1, verified vs OSM node 2176898978). Zones: Three Arms Zone (NE),
Central Area/CBD, Maitama (N), Wuse + Wuse II (NW/W), Garki Areas 1–11 (S), Asokoro (E/SE,
Aso Rock foothills).

Landmarks L1–L19 (each one GLB + real name on sign/plinth):
L1 Aso Rock (400 m granite dome, backdrop only, not climbable) · L2 Presidential Villa
(walled, guarded gate — **player cannot enter; guards turn away**) · L3 National Assembly
(white complex, large green central dome) · L4 Supreme Court · L5 Eagle Square (vast paved
parade ground, grandstand, flagpoles) · L6 Federal Secretariat (mid-rise office campus) ·
L7 National Mosque (golden dome, 4 slim minarets) · L8 National Christian Centre (white,
steep gables, tall spire) · L9 NNPC Towers (4 linked dark-glazed towers, Herbert Macaulay
Way) · L10 CBN HQ (tall dark-glass tower, Tafawa Balewa Way) · L11 Transcorp Hilton
(Maitama, Aguiyi Ironsi St) · L12 Millennium Park · L13 Wuse Market (dense corrugated
sheds, aisles ≥2.4 m, the one place that feels like Lagos) · L14 Banex Plaza
(phone/electronics, Wuse II) · L15 Ship House / Defence HQ (ship-shaped, Garki Area 10) ·
L16 Ceddi Plaza · L17 Garki Area 1 roundabout/shopping · L18 Nnamdi Azikiwe Intl Airport
(arrival terminal, taxi rank, car park) · L19 Abuja City Gate (monumental gateway on
Airport Rd).

Abuja feel: green-gold savanna grass (≥60% of open ground), red laterite verges,
neem/mango/palm/acacia trees, rolling hills + granite rocks on horizon, harmattan haze
(warm grey-beige fog, visibility ~1–3 km). CBD offices 6–20 storeys (white concrete,
blue/green glass, set back 8–15 m); Maitama/Asokoro villas 2-storey behind 2.4 m walls;
Wuse/Garki 3–5 storey commercial blocks with shopfronts. **No okadas/kekes in CBD,
Maitama, Wuse or Garki** (edge of map only). FCT taxis green/white. Arrival: flight lands
at terminal → walk out to taxi rank → drive in past City Gate (arrival point on
Independence Ave pavement; a proper fenced travel park with bays/kiosks/shed/hawkers is a
TODO at (60, −1943)).

Key Abuja roads: Airport Rd (Umaru Musa Yar'Adua Expy, 3+3), Nnamdi Azikiwe Expy, Shehu
Shagari Way, Independence Ave, Constitution Ave, Ahmadu Bello Way, Herbert Macaulay Way,
Tafawa Balewa Way, Aguiyi Ironsi St, Aminu Kano Cres, Adetokunbo Ademola Cres, Mike
Akhigbe Way. Roundabouts: Area 1, Berger, Eagle Square approach, Banex — drivable around,
never through.

### Later cities (post-launch, in priority order)
- **Port Harcourt (first after Lagos):** Old GRA, GRA Phases 1–5, D-line, Diobu Mile 1–4,
  Peter Odili corridor, Trans-Amadi (industrial/container yards), Woji/Artillery/Garrison/
  Waterlines, Rumuola/Rumuomasi/Rumuokwuta/Rumuokoro, Choba/Uniport, Borokiri/Town/Eagle
  Island/Abuloma (waterfront, stilts, canoes), Onne/Eleme (refinery skyline, flare stacks).
- Then other states (Kano, Rivers, South-East/South-South state specs exist in research;
  only the owner opens cities).

### World rules / budgets (carry over as design constraints)
- GLB budgets: landmark ≤50k tris; filler ≤8k tris; textures ≤1024 px (2048 only for Aso
  Rock, National Mosque dome); instancing for repeated props; each district ≤350 draw
  calls in view (streaming spec: <250 at Oshodi); 30 fps at 390×844 with 4x CPU throttle;
  no frame >200 ms entering a district.
- Ground truth: Y-up, origin at footprint centre, ground y=0, forward −Z; nothing
  floats/sinks (lowest vertex within 5 cm of ground); no z-fighting; no non-uniform scale
  on curved primitives.
- Day/night + weather follow **real WAT time** and real local weather; night = streetlights,
  lit windows, landmark floodlights (Mosque dome, Assembly).
- **Unity mapping:** URP, Addressables + scene streaming (districts as tiles, wake within
  600 m of edge; one city loaded at a time), LOD groups (far = skyline shell beyond 300 m),
  occlusion culling, NavMesh for peds/traffic, Timeline/Cinemachine for flyovers. Terrain +
  SpeedTree-equivalent instanced vegetation for Abuja savanna.

---

## 2. PLAYER

**What it is:** Third-person character, created before onboarding questions; one base mesh
per sex with morph-target variety (skin: Nigerian range; outfits as real garments over the
body; hair, beards, headwear, face presets; 130-row starter wardrobe catalogue, fabrics,
fit presets).

- **Movement:** walk <2.6 m/s, run above; default 4.2 m/s is a jog. Walk cycle driven by
  ground speed — planted foot never slides (foot-slide <15% of body speed), stride grows
  with speed (walk 0.36–0.5 rad, run 0.6–0.85 rad), feet under hips, ~15 cm step width,
  knee bends only in swing, arms swing opposite legs. Locomotion 1D blend
  (idle/walk/jog/run/sprint), upper-body layer for phone/carry/wave, head look-at within
  4 m (clamped 60°).
- **Camera — OWNER HARD RULE:** one steady damped follow angle locked behind the player,
  walking and driving; no sudden swings, orbit drift, or automatic angle changes;
  occlusion pull-in/fade so a building/wall can never hide the player. Camera-relative
  movement; bias-corrected follow direction + dead zone so strafing/backpedalling never
  swings the lens. Interiors use the player's floor as camera floor (no "never below 0.5"
  world clamp).
- **Character budgets:** player body 18k tris LOD0 / 8k LOD1, ≤65 bones, ≤1.5 MB; NPC
  10k/4.5k/1.5k, ≤700 KB; outfit 4–8k tris; hair 0.5–3k. Crowd: LOD0 <12 m, LOD1 12–35 m,
  LOD2 35 m+; anim update every frame <12 m, every 2nd frame to 35 m; caps 12 fully-skinned
  NPCs on screen (phone), 30 (laptop).
- **Needs (survival-lite):** hunger, energy, **Pawa** (stamina/energy for work — need Pawa
  ≥25 to clock in to a shift). Sleep restores energy (in own bed); food (mama-put, gala
  ₦150) restores hunger/Pawa. Needs drain over time; jail/cell time decays hunger/energy.
- **Health/damage:** vehicle damage model matters in chases (tokunbo Corolla vs patrol
  Hilux); crash damage billed if caught. No detailed HP numbers in the specs — design as
  a Unity health/stamina pair.
- **Onboarding ("Lapo or Nepo"):** character creator → ~60 s of questions (one per
  screen) → start band → reveal card "Your life don set" → city flyover → journey home
  (no teleport) → first home + first contact. Bands (wallet / weekly rent / stipend /
  hustle rate / street smarts / respect): Lapo ₦5,000 / ₦1,500–2,500 / none / +10% / 70 / 5 ·
  Lapo+ ₦6,500 / ₦2,000–2,500 / none / +10% / 55 / 10 · Middle ₦8,000 / ₦3,500–4,000 /
  none / +0% / 40 / 15 · Middle+ ₦10,000 / ₦4,500–5,500 / none / +0% / 30 / 20 · Nepo
  ₦15,000 / ₦8,000–12,000 / ₦20k mo.1 ×0.85/mo, floor ₦2k / +0% / 20 / 30 · Nepo Baby
  ₦20,000 / ₦15,000–18,000 / ₦30k mo.1 ×0.85, floor ₦2k / +0% / 10 / 40. Starter anchor
  ₦8,000 "waka money". Nobody starts with a car. First contacts by path (School/Trade/
  Hustle) and city (e.g. Musa the Oshodi park tout; Sister Ronke; Oga Emeka; Malam Sani in
  Abuja). Family household adds partner + child NPC needing weekly food money. Signup:
  18+ DOB step, Terms checkbox before "Create my hustle"; one device per account (second
  signup on a registered device shows popup: Sign in / Forgot password). **Guests pass the
  same gates; never a fake/shared user.**
- **Unity mapping:** CharacterController + root-motion-stripped Animator (1D speed blend,
  layered upper body), Cinemachine FreeLook/3rd-person follow with collision pull-in
  (custom, damped, no auto-orbit), BlendShapes for body variety, ScriptableObject wardrobe
  catalogue, URP.

---

## 3. POLICE / B-39 RULES (CRITICAL — owner-verified)

**B-39 origin:** owner got a full-screen ARRESTED overlay ("Repeat hit-and-run… spent the
night in a cell") when he says he hit nobody. His rule: *"The police have to chase and
catch you before any arrest, or something else equally real — never an arrest popup out of
nowhere."* Implemented in browser (LOAD_V 20261102e/20261103c, verified at 390×844 +
1280×800); live-car iPhone check was still owed at freeze. **Carry the rules, not the bug.**

Hard rules (L-123: "Never punish from a counter or a radius"):
1. **A hit requires REAL body contact** — the ped's body overlapping the vehicle's body
   (spec: vehicle footprint halfLen/halfWid + 0.3 m) at ≥15 km/h, one report per impact
   (1.5 s window). A ped who steps aside is a **near miss, not a crime, and must never
   count**. No proximity radius (the old 2.9 m radius turned kerbside near-misses into
   hits). Cooldown starts only after the ped steps aside or is hit — never from first
   approach.
2. **No arrest from a counter alone.** No jail overlay, record entry, or fine deduction
   may fire from an offense counter (the old "3rd hit = jail" path is deleted). Arrest
   happens ONLY after police chase + catch: wanted stars bring pursuit; pursuit ends in a
   stop or foot bust; only then the arrest plays. Vehicle arrest: officer within 3 m AND
   vehicle speed <1.5 m/s (stopped). On foot: officer within ~1.3 m for ~1.2 s (bust).
   Keep driving and stars still decay — the chase must stay escapable (star decay /
   lose-them path stays).
3. **Children never count** (never a hit, never a star).
4. **A genuine hit still earns its star and fine:** verified example — 1 real hit = 1 star
   + ₦10,000 fine; 3 hits = 4 stars, no jail. Near miss at 54 km/h = 0 hits, 0 stars.
   Theft adds a star, no jail. Suspended-licence doubling exists (3 hits = 18 points,
   12+ doubles fines → ₦20k) but is now rare because phantom hits are gone.

Wanted system (police-spec, 1–5 stars, Lagos-flavoured): ★ "Dem dey find one boy" (petty:
pickpocket, shoplifting, danfo fare-dodge, slap) · ★★ "Police dey around o" (hit-and-run
no injuries, phone snatch, resisting LASTMA, driving without papers) · ★★★ "Area F is
looking for you" (armed robbery, serious assault, hit-and-run with injuries, carjacking,
ramming a checkpoint) · ★★★★ "Special squad don enter matter" (bank robbery, shooting at
police, kidnapping, jailbreak) · ★★★★★ "Na national news" (cop killed, multiple homicides;
NaijaNews runs your face).

- **Witnesses matter:** no witnesses = no heat; NPCs who saw you can report later;
  masks/helmets reduce ID chance. Heat decays by laying low (change district/clothes,
  stay indoors, sleep; each clean in-game day drops heat); ★★★+ leaves a permanent
  **case file** (never fully decays). ★★★+ police hunt last-known position; wanted poster
  on NaijaNews + phone alerts; NPCs can ID you.
- **Gates by heat:** ★★+ checkpoints stop you specifically; ★★★+ can't enter clubs, can't
  take legit jobs, can't sleep at home (raid risk); ★★★★★ danfo drivers refuse you.
- **Police AI:** state machine patrol → suspicious → pursuit → search → arrest; vision
  cones + hearing radius; foot pursuit (stamina; Fitness matters; break line of sight
  ~20 in-game s to lose them); patrol Hiluxes (white/black, light bars) pursue in traffic,
  PIT at ★★★+; helicopters at ★★★★★ (spotlight at night; only buildings/tunnels break
  lock); roadblocks at ★★★+ on chokepoints (Third Mainland Bridge, Lekki–Epe toll plaza,
  Apapa–Oshodi Expy — spike strips + 2 cars + 4 officers; ramming = ★★★★). Regular
  officers average, will take bribes; MOPOL (★★★+) faster, no negotiation; plainclothes
  at ★★★★+.
- **LASTMA (separate heat, no stars for fines):** traffic stops (expired papers, no
  seatbelt, phone-while-driving, okada on restricted routes, one-way, broken tail light).
  Fine schedule: minor ₦20k–50k; one-way ₦100k+ or impound; okada on Island = bike seized.
  Pay at bank within 7 in-game days or doubles + warrant. Resisting/bribe-gone-wrong/ramming
  escalates to police stars. 3+ impounds/month = "habitual offender" (fines double).
- **Arrest flow (lived, no fade-to-black):** cuffed on spot (NPCs gather/gist) → ride in
  back of patrol Hilux to the district's real station (Oshodi→Mosafejo; Island→Area A/Lion
  Building; Ikeja GRA→Area F/Lagos State Police Command HQ; Surulere→Area C; Apapa→Area B;
  Festac→Area E; Yaba→Adekunle; Ikoyi→Ikoyi station; Alausa) → booking (mugshot can leak
  at ★★★+, fingerprints, belongings in property bag + paper receipt) → cell (concrete
  bench, barred window, 2–6 NPC inmates with stories/quest hooks; 1 in-game hr per real
  min; hunger/energy decay; gist, one phone call, behave/misbehave). Missed shifts (fired
  after 2 missed), rent, esusu, birthdays cascade — the game doesn't pause.
- **Bail:** ★–★★ bail available; ★★★+ bail hearing; ★★★★★ no bail. Bail = fine ×
  multiplier (×2 for ★★); petty theft ₦50k–200k; paid by the contact you call from THEIR
  wallet (debt + relationship consequences); duty solicitor free, 50/50 shows up; lawyer
  retainer ₦500k (out in an hour, charges reduced). Bail-jump = warrant, heat locked
  ≥★★★, contact furious, "fugitive" flag (any ID check can re-arrest).
- **Court:** arraignment 1–7 in-game days after arrest (serious crimes); plea guilty
  (shorter, record stands) / not guilty (trial, weeks, lawyer needed, acquittal possible
  if no witnesses/goods + good lawyer); sentences: fine (scales with crime AND visible
  wealth), community service (3–10 days, 4 hrs/day, repairs rep), jail, discharged (rare).
  **Bribery minigame:** offerable at stops/checkpoints/booking; officer personalities
  (hungry / straight / watched); too little = insulted; too much = remembered as rich;
  backfire (honest/watched officer, bodycam sting) = bribery charge +1 star + original
  charge stands. Game warns: "this one look like correct officer o."
- **Criminal record (Life Record, permanent):** blocks FOREVER — police/LASTMA/FRSC jobs,
  bank teller, civil service, immigration/passport fast-track, visas, chieftaincy titles.
  Complicates landlord refs, bank loans, school admissions. Doesn't block street economy
  (street rep UP), clubs, influencer career. Expungement: 5 in-game years clean +
  community service + lawyer + friendly magistrate — rarest quest in the game.
- **Impound:** triggers — no papers, one-way, okada on Island, DUI, getaway vehicle,
  warrant. LASTMA yard/police pound; release fee = fine + admin + ₦5k/day storage (meter
  runs); pay at bank → receipt → officer "can't find" file → settle/wait → drive out (or
  "special release" bribe, risky/fast). Left too long: parts stripped (mirrors, battery,
  tyres). Unclaimed vehicles auctioned monthly — you can buy back your own car.
- **Player police career (Phase 2 / mirrored game):** apply at Command HQ (clean record,
  NIN, education, fitness, medical); patrol shifts, checkpoints, distress calls
  (including other players' crimes); bribe window from the other side (take it = side
  income + risk meter: citizen reports, stings, lifestyle audit); caught corrupt =
  dismissal + record + government door closed forever. Ranks constable → corporal →
  sergeant → inspector.
- **Unity mapping:** Physics overlap test (vehicle OBB vs ped capsule — never distance
  radius) for hits; NavMeshAgent + state machine for officers (vision cone via
  raycast/trigger, hearing radius); wanted level as a server-authoritative state with
  decay timers; Timeline/Cinemachine for arrest/booking/cell sequences; ScriptableObject
  fine schedule + station registry.

---

## 4. VEHICLES

- **Driving:** enter any parked drivable vehicle; drive chase camera (same steady
  behind-player rules as on-foot); pedals 64–104 px touch targets; damage model; fuel/
  dues/maintenance for owned danfos; collisions tracked (damage deducts from lease
  deposits).
- **Vehicle types:** private cars (tokunbo Corolla etc.), danfo (yellow VW T3/T4-style bus,
  5.6 m, 14 seats, two black stripes, sliding side door, route painted on front, slogan
  sticker "No Food For Lazy Man"), keke napep (Bajaj-style, 2.6 m, 3+driver), BRT bus
  (12 m, 36+40 standing), city bus (10 m, 30+25), Red Line train (4 cars, 60 m, 120+300),
  FCT taxis (green/white, Abuja), gov SUVs, patrol Hiluxes. Liveries/badges always
  fictional — no real car maker, airline, bus company or brand names/logos.
- **Ownership:** garage is server truth (assets table, type `car`, written by RPC only —
  client cache never source of truth). One active car parked at home door. Every owned car
  parks at a catalog parking spot of the active home, up to garage_slots; overflow on the
  street outside. Walk up and get in through the normal drive path.
- **Buying/finance (player-economy-law §11):** buy outright OR lien: 20% down, rest over
  12–52 weekly instalments, server-set flat 8% total interest, auto-debit on due day if
  wallet covers. Miss 2 payments → warning + late fee; miss 4 → repo team (NPC van +
  officers) repossesses in-world → auction; ex-owner gets surplus minus owed/fees. Lease
  to others: daily/weekly rate, upfront, damage deducts from deposit. Danfo (tokunbo)
  ₦8,000,000 (retuned to real band ₦3–8m); luxury coach ₦35,000,000.
- **Licences/driving school:** nobody drives for hire without a verified licence earned by
  passing the in-game simulation test (server-scored; client streams inputs, server
  grades; attempts ₦2,000 each) or graduating driving/flying school (lessons ₦5,000
  driving / ₦25,000 flying; hire UI refuses unlicensed players: "Needs Class D — Eko
  Driving School, Oshodi"). Licence classes: Class D (danfo for hire), CPL (hired jet).
- **Aviation (top tier):** MMIA Lagos is a full explorable location (check-in, security,
  lounge, gates, delay board); ambient air traffic on fixed glide paths (visible from
  Oshodi/Ikeja/Surulere; landing lights at night). Passenger flights: get to MMIA
  (Uber/danfo/own car — the journey IS gameplay), buy ticket, ride. Pilotable: aviation
  school at MMIA ₦2.5M full course (scholarship: top of class + high rep = 50% off),
  4 in-game weeks of lessons; rent Cessna 172 ₦150k/hr wet or buy ₦45M (name on hangar).
  **Helicopter ₦2.5bn / private jet ₦9bn** — bought at MMIA hangar; parks on the mansion
  helipad when the active home has one, otherwise at the hangar. Flying: no teleport;
  compressed long legs with progress shown. Pilot career: student → captain.
- **Intercity travel:** flights Lagos↔Abuja (and later PH/Kano) via the flight sim (travel,
  not teleport); return flight counter in terminal; Red Line rail (see §6/transit); ferry
  Makoko Docks → Five Cowries Terminal (planned).
- **Unity mapping:** WheelCollider-based vehicle controller (arcade-tuned, per-vehicle
  ScriptableObject: mass, gears, damage), garage as server-synced inventory, flight as a
  separate flight-model controller + compressed-time travel sequence with progress UI,
  Addressables for vehicle GLBs.

---

## 5. PROPERTY — "Own your home, actually live in it"

Owner quote (Oct 16, 2026): *"I should be able to buy properties and new home at fine
areas in the game and actually be able to live in them, drive my cars, own a mansion and
see all my cars at home in garage and able to drive at any time."*

- **Rules (PR-1–PR-7):** ownership is server truth (RPC-written rows only; client is a
  cache). Money moves on the server inside the same RPC that writes the ownership row,
  one transaction. Prices come from a server catalog table (client catalog is display
  only; RPC reads price from the table, never the request). Owning never gates the world
  — every district stays open. Progression is real and slow; each tier earnable from
  jobs/business over time; nothing free; **Naira Coins (real money) never buy property**.
  One home is the active residence — spawn, sleep, "MY CRIB" sign, garage all follow it.
  No teleport — you get home by walking, driving, or flying; moving in is a phone action
  but you still travel there yourself.
- **Tiers & prices (seed v1):**

  | Tier | Example | Price | Garage |
  |---|---|---|---|
  | starter | Room & parlour, Oshodi/Agege (home-faceme / home-selfcon) | ₦450k–900k | 1 (compound) |
  | mid | Mini flat Yaba, 2-bed Surulere, duplex Ikeja GRA | ₦3M–18M | 2 |
  | fine | Lekki Phase 1 terrace, VI apartment, Ikoyi duplex (home-luxe) | ₦45M–150M | 4 |
  | mansion | Banana Island mansion (estate x 672–744, z 350–410; home-mansion) | **₦150M** (owner decision Oct 17 — replaced ₦850M) | 6 + helipad |
  | aircraft | Helicopter / private jet | ₦2.5bn / ₦9bn | n/a |

  Abuja rows (Gwarinpa → Maitama → Asokoro) follow in the same catalog once the owner
  confirms Abuja homes at launch. Only the owner opens cities.
- **Paying (owner: "₦150M pay small small"):** 20% deposit to move in; rest in weekly
  instalments — 12 weeks (room/flat), 26 weeks (VI duplex), 52 weeks (Banana Island
  mansion, ~₦2.31M/week). Paying in full at once allowed. Missed instalment: 3 days'
  grace, then the gate locks until arrears paid; home is never taken away silently. Homes
  you own but don't live in are **let to tenants at 0.7% of price/week, paid daily**.
- **Living in it:** active home's door registers with the interiors system using the
  catalog plan; sleep in your own bed (advances time, restores energy); cook through the
  food system; respawn at active home; "MY CRIB" sign follows the active home. Interiors
  must be furnished and lit warm at night — never an empty box. Plans: home-faceme,
  home-selfcon, home-2bed, home-bq, home-duplex, home-stilt, home-luxe, home-mansion.
- **Garage:** every owned car parks at the active home (up to garage_slots); overflow on
  street. Mansion garage shows ALL owned cars; take any car out and drive anytime.
- **Customization (P-6):** place furniture from a list; pick colours/finishes; saved
  server-side (design ≤16 KB, ≤200 items, only known item ids); visitors see the owner's
  design; mansion wings unlock by spend.
- **Estate agent offices:** one enterable office per tier area ("View homes" prompt);
  closing a deal = one tap → progress state → result. Rented starter homes (~25 homes,
  6 bands from Makoko stilt to Ikoyi serviced + VI/Lekki hotels; weekly rent) stay as they
  are; a bought home replaces rent.
- **Net worth:** Forbes list adds property value; `netWorth()` = sum of price_paid over
  owned rows.
- **Unity mapping:** server-authoritative ownership service (RPC pattern preserved: price
  read server-side, balance checked server-side, single transaction); interiors as
  separate streamed scenes/prefabs per plan with furniture placement system (grid/
  free-place + validation); garage as a parking-slot system at the active home; helipad
  as a landing zone for the flight controller.

---

## 6. JOBS

**Core design (jobs-world-spec, owner rule Oct 16):** *"We have to build this game
dynamic. It has its own play, but real players can do jobs if they are available."* The
city works on its own — **bots do every job you can see** (danfo conductors/drivers,
okada riders, ride drivers, POS agents, mechanics, tailors, hawkers, traders). A real
player can take any job a bot is doing when that job is free. **JW-1–JW-7:** every job has
visible bot workers (a job with no bot in the world isn't built yet); a job is a slot (one
bot = one slot); taking over is visible and gentle (walk up, tap one prompt "Take over
from this conductor" — bot steps a couple of steps aside and waits, never
vanishes/teleports; walks back when you let go); **one job at a time**; the work is real
(same work-before-pay loop: shout + change for conductors, drive the route for drivers);
pay follows the hard economy (skill, rep, quality); other real players' held slots show
as taken (holder `remote`) and can't be claimed. One mechanism for every job (job-slots
system).

- **Jobs with player take-over built:** danfo conductor (5 bot conductors, Oshodi park
  bays, Terminal 3) · ride driver (Naija Ride app — go online claims slot; requests from
  other devices offered to real players first for 12 s, then a bot; only when a driver
  announced in last 45 s) · okada rider (4 bot riders at Oshodi okada park — haggle +
  weave the go-slow).
- **Jobs TODO at freeze:** danfo driver, POS agent, mechanic, tailor, hawker, trader
  (bots exist; take-over to build).
- **Formal career jobs (story-mode, 10 jobs, 5 sectors — application + interview +
  practical):** Graduate Engineer (Shell) ₦45,000/shift · Bank Teller ₦12,000/shift ·
  Police Constable ₦8,000/shift · LASTMA Officer ₦6,000/shift · Nurse ₦10,000/shift ·
  Teacher ₦7,000/shift · Danfo Driver (NURTW) ₦3,000–12,000/shift · Market Trader
  (Balogun) ₦2,000–20,000/day (haggle skill; ₦5,000 starting stock) · Roadside Mechanic
  ₦2,000–8,000/job · Tailor ₦1,500–6,000/order. Interviews: role-specific questions,
  60–80% to pass; mechanic/tailor practical only. Application flow: browse Job Center
  (physical, Ikeja + phone "Jobs" app) → apply → interview → probation → shifts.
  Consequences: criminal record blocks police/bank/civil-service jobs; missed shifts
  (fired after 2 missed in jail).
- **Shift economy (balance sheet):** rung-1 formal shift ~₦12,800 gross/hr active play
  (₦2,800–4,000 per 15-min session); 3 shifts/in-game-day cap; Pawa ≥25 to clock in; net
  after living ≈₦5–7k/day. Rung-5 ~₦240,000/hr (40+ shifts, ★≥3.5, skill gates,
  2 exams, probation week). Job pay in property-spec reference: ~₦300–700 per 1–2 min
  task at skill 1, ~₦10k–20k/hr, rising with skill/business.
- **Other earning:** side gigs (player-to-player, poster-funded escrow, max 2 active)
  ₦2k–30k · busking tips ₦50–300/song · club/owambe gigs ₦3k–25k/night · city treasure
  ₦5k–15k/day max (1 win/7 days) · "Call Papa" pocket money ₦500–2k/day · shop net
  ~₦72k/week · danfo net ~₦244k/week (retuned) · luxury coach net ~₦390k/week · rental
  income ₦8k–120k/mo per unit · Invest app 0.3–1%/week · record-deal advance ₦150k
  one-time (Music 10 + Fame 80+) · Paddy Mi emergency loan ₦5k/week max (auto-repaid over
  next 3 paydays; bond 80).
- **Transit as a job + a system:** danfo/conductor/okada/ride-driver above; plus riding
  as a passenger (see §7 fares). Rush hour: danfo/BRT peak extras, go-slow, "Rush hour
  don start" toast (T-5).
- **Unity mapping:** job-slot manager (ScriptableObject slot registry + bot AI handover:
  NavMeshAgent step-aside/walk-home), shift system (server-verified payout RPCs),
  interview dialogue/quiz system, career progression (rungs, exams, probation flags).

---

## 7. ECONOMY

- **Currency:** ₦V (virtual naira). **Nothing here is real naira, nothing cashes out.**
  All ₦ are virtual play money. Real money (Flutterwave) buys only two things: **Naira
  Coins packs** and **ad space** — never property, never registration/buses/shops/stock
  (ad top-ups nerfed to ₦2,000/day, needs/consumables only).
- **Three laws (balance sheet, binding):** (1) Money is earned, never granted — the client
  NEVER mints ₦V; every credit is a server-verified consequence of a real action; if it
  didn't happen on the server, the money doesn't exist. (2) Scale feels like real Lagos —
  rung-1 monthly-equivalent earnings near real entry wages (₦50–150k); big things cost
  real-Nigeria multiples of those wages. (3) Big money needs big play — every curve gated
  by time, skill, or capital; never a faucet ("Could a player get rich doing only this
  with no effort?" — if yes, nerf it).
- **Ledger:** hash-chained `economy_ledger`, balance = latest `balance_after` per player,
  computed server-side under an advisory lock; every money move goes through a
  security-definer RPC; client batch RPC rejects server-only sources. Starting money is
  server-paid (never from client constants — the old quiz.html ₦500k client grant is
  retired). Wallet reads from the server ledger, not local storage.
- **Grind math (intended arc):** week 1 — room + first shifts; month 1–2 — rung 2, maybe a
  flat or company registration (₦50,000); month 3–5 — first financed danfo (30% down on
  ₦8m = ₦2.4m); month 6+ — fleet, shops, landlord milestones; year 1+ — coaches, empire.
  Saving ~₦3k/day (rung 1–2) → ~₦20k/day (rung 3) → ~₦45k/day (rung 4+). Face-me room
  (rent + 10% agent + 1-mo caution ≈ ₦16,800) ~1 week; Oshodi flat buy ₦400k ~2 months;
  Lekki duplex ₦2.4M 3–4 months; luxury coach ₦35M 12–18 months. **Nobody gets rich in a
  weekend.**
- **Living costs (game scale):** jollof ₦1,200; danfo ₦200–500; coffee ₦300; room rent
  ₦8,000/mo (Oshodi); staff wages ₦15k–60k/week (a luxury flex).
- **Sinks (deliberate, fight inflation):** casino (RTP 94%, negative EV, hard caps, 18+),
  LagosBet (negative EV, loss cap ₦20k/week), rent, fuel/dues/maintenance, fines, impound
  storage ₦5k/day, taxes.
- **Businesses:** types at launch (all playable, real loops) — shops, danfo fleet, market
  stalls, etc. Registration ₦50,000 (server-priced); name on public business registry
  (searchable in phone); weekly plot rent by district tier (Ikoyi ₦25k/wk … Oshodi ₦3k/wk);
  miss 2 weeks → "SEALED BY GOVERNMENT", stock frozen until rent + fine paid. Staff: hire
  NPCs or players (wage, schedule, vehicle assigned; wages auto-pay from business/owner
  wallet). Business withdraw applies tax.
- **Loans (player-to-player, economy-law §2b):** lender sets amount, payback 7/14/30 days,
  optional "appreciation" max 10%; late = agreed appreciation + "OVERDUE" on both phones;
  default after 2× term → lender can report → small-claims path: agency garnishes future
  earnings + public "owes" note. 3+ defaults = fraud pattern flag.
- **Anti-crime agency (EFECC-equivalent):** server-side detection → investigation → arrest,
  all logged; convictions published in news EVERYWHERE ("EFECC ARRAIGNS [player] OVER
  ₦[amount] [crime] — [sentence]"). Structuring 6× ₦90k transfers in an hour → crime flag
  → investigation → arrest → trial → conviction on news + billboards. Taxes: honest side
  of the same system.
- **TRANSFERS — B-38 (transfer-spec.md, spec approved by Benjamin Nov 2, 2026 for BUILD;
  applying migration 0040 needs his SEPARATE OK; depends on B-27 profiles + B-29 username
  lock going live first; Send tab stays hidden until RPC is live and two-account test
  passes):**
  - Scope: player-to-player only, inside the same game (shops/NPCs/crews/businesses keep
    their own RPCs). Sender names receiver by username. Game NIN, email, DOB never
    used/returned. Money lands at once (new ledger row); receiver sees toast next balance
    sync ("Tunde sent you ₦5,000") + Bank history line. Optional note, 40 chars, plain
    text, escaped, textContent only.
  - **Caps:** min **₦100** / max **₦500,000** per transfer · sender per WAT day:
    **10 transfers and ₦1,000,000** · receiver per WAT day: **₦1,000,000** received total ·
    sender account age **≥7 days AND ≥3 shifts or sales ever**.
  - **Anti-abuse:** self-transfer refused (server user-id check) · same-device refused
    (shared non-null `device_id`) — backs one-account-per-device · 24h hold on re-sending
    received money (last 24h of `transfer_in` subtracted from sendable — breaks A→B→C
    loops) · pair flag: one pair moving **>₦2,000,000 in 7 days** → row in
    `nl_transfer_flags` for admin review (money still moves; nothing auto-blocked beyond
    caps) · Forbes list ranks by balance; transfers don't create money (total wealth
    never changes).
  - **Ledger:** one call writes exactly two rows in one transaction — sender
    `transfer_out` −amount "Sent to <username>", receiver `transfer_in` +amount "From
    <username>", both `client_ref = 'tr:' + transfer id` (retry with same id = no-op;
    idempotency via unique `(user_id, client_ref)`). One `nl_transfers` row records
    sender/receiver/amount/note/time. Bank history: "Sent to Tunde, ₦5,000" / "From Ada,
    ₦5,000" with WAT time.
  - **Failure:** clean no-op; both players locked (advisory locks in user-id order, no
    deadlock); client never debits locally — shows "Sending…", waits for
    `{ok, balance_after}`, adopts server balance. No outbox, no auto-retry; one transfer
    id per Send tap. Errors: `insufficient` ("You no get reach that amount. Your balance
    is ₦X."), `not_found` ("We no fit find that player. Check the username."), `limit`
    (which cap + reset at midnight WAT), plus `self`, `same_device`, `too_small`,
    `too_much`, `too_new`.
  - **Send tab UX:** username field with live "Found: Tunde" check, amount + quick chips
    (₦1k/₦5k/₦20k), optional note, confirm sheet "Send ₦5,000 to Tunde? This cannot be
    reversed." + **hold-to-send (600 ms)** so a stray tap never sends. Success: receipt
    card with amount counting down from wallet, "Sent to Tunde", time, new balance. Must
    work touch + mouse at 390×844 and 1280×800.
  - Note: an earlier economy-law draft had different numbers (min ₦100/max ₦500k, ₦1M/day
    sent; gift ≤₦50k) — **transfer-spec.md (B-38) is the authority**; it supersedes.
- **Property lock / grants:** see §5 (server-only `property_buy`/`property_instalment`/
  `property_rent` sources; client batch RPC must reject them). All grants (starter money
  etc.) computed and paid by a server function writing one ledger row.
- **Anti-cheat (from day one):** server validation points per RPC; invariants
  (non-negotiable builder contract); anomaly thresholds; ban ladder. Prices verified on
  the server only; no client-picked amounts; no `eval`/`new Function` on data; every new
  table RLS-on, owner-only policies; money/item/rank changes only via security-definer
  RPCs; player text escaped before innerHTML.
- **Unity mapping:** server-authoritative economy service (ledger as append-only event log;
  balance derived; idempotency keys on every mutating call); client wallet is a read cache
  + `adoptServerBalance`; RPCs preserved in shape (same caps, same error codes, same
  ledger sources).

---

## 8. MULTIPLAYER / SOCIAL

- **Scale directive (owner, top priority):** players must see each other in the world at
  launch. Architect netcode for a transport swap to dedicated regional game servers
  without rewrite. Supabase Pro plan to be bought before launch, spend cap off (purchase
  itself still needs owner's yes on exact terms).
- **Netcode (netcode-spec):** world split into square **cells of 200 m** per city; client
  joins own cell + 8 around it (up to 9 topics); joins new ring before leaving old.
  `nearby()` returns at most the **40 nearest** players. Movement messages binary
  **20 bytes** (`mv`): flags (moving/driving/running/indoors/anim id) + position/heading.
  Send rates: **driving 4 Hz, walking 2 Hz** (owner decision Oct 23, B-34), plus one
  immediately on start/stop. Events (`ev`) JSON ≤256 bytes. Receiving: interpolate —
  render drivers ~300 ms in the past, walkers ~600 ms. Remote drawing LOD (fewer updates
  for far players). Identity: shown names/crews/badges never taken from payload — looked
  up once per player via street-card RPC, cached 10 min. Payload limits: movement ≤64
  bytes. Reconnects, one-tab rule, offline play. Phase 2: own WebSocket game server
  (`WsTransport`); transport interface (`send(topic, kind, bytes)` / `on('msg')`) is the
  swap seam — game code never assumes one server has everyone.
  > **Migration note (Nov 5, 2026):** the transport named here is the browser design.
  > The Unity multiplayer workstream recommends **Photon Fusion 2 (Shared Mode) +
  > Supabase** instead — see `MIGRATION-PLAN.md` §4. The cell/AoI design, send rates,
  > interpolation, and identity rules above still carry over; the transport does not.
- **Friends:** request + accept (like a contact swap); **cap 300 friends**; rate limits 30
  requests/day, 10/hour. Friends list in phone (People tab inside Contacts): online now,
  "Met at" line (server records where two friends first hailed each other). Real players
  only — never seed fake players, NPC friends, fake counts; an empty list says so in game
  voice.
- **Street card:** avatar portrait, username, home city/district, "In Lagos since…", crew
  badge + role, office held (gold seal), police rank if any, street rep tier, three public
  Life Record lines. Buttons: **Hail** (wave emote + friend request), **Message**,
  (Send ₦ — in-chat action).
- **Chat (chat-protocol):** real-time; lobby/presence channel (one per world); **DM
  channels, one per conversation pair, ephemeral** (private `d:<lo>:<hi>` topic per friend
  pair); crew chat channel; bus chat rooms (TR-7, planned). Messages list Friends then
  Street contacts. Moderation: blocklist for crew/candidate names; no real party
  names/politicians/party colours/logos, no cult/confraternity names in any player
  text/generated art/ad. Player text escaped. Chat privacy: device-local chat logs allowed
  (never leave the device) WITH plain-language disclosure in phone Settings ("Your chats
  stay on this phone — saved on this device only, never sent anywhere"). **No voice chat**
  (owner boundary Oct 10) — text only.
- **Crews:** types — Owambe Crew (aso-ebi/party squads) · Park Union / Garage & Park
  Workers' Union (danfo/keke shifts, fares) · Market Association (trading, stall sales) ·
  Hustle Crew (any shift/gig/business revenue) · Area Crew (clean-ups, guiding new players,
  street events). Rules: create in person at a Registration Office (Lagos: Alausa; Abuja:
  Federal Secretariat); **20 members v1**; roles Oga (founder) + others; **one crew per
  player**; leaving starts 24 h cooldown before joining another; rename once/30 days;
  disbanded name held 30 days; crew chat (same moderation as all chat); crew base v1.1
  (rent a room/shop/park office). **Crew wallet: NOT in v1** (shared money =
  laundering/minting risk; v1.1 earliest, only after money is fully server-side). Rep:
  street rep (per player) + crew rep (per crew per district), computed server-side from
  recorded events (shifts, gigs, sales, events hosted, people guided) — never a client
  "add rep" call. Weekly Area Board per district: top 5 crews per type, reset Sunday
  00:00 WAT; top crew gets banner on district crew post + badge stripe; no money prize.
  Rivalry = competition, not violence (both Ogas agree; side-by-side weekly rep; win
  streaks on street card; v1.1 rivalry weeks).
- **Respect tiers + real rewards (S-13):** Trusted (50): every lender's weekly rate drops 1
  point, caps rise 25% while respect ≥50. Big Name (75): agberos/area boys never charge
  you; street vendors 10% off. (Copy must name exactly what the code gives — never
  promise a reward the code doesn't give, L-119.)
- **Elections/government (post-launch, low priority):** offices, weekly WAT election
  cycle, running for office (crew endorsements count), policies/directives/enforcement
  focus, treasury (no minting — fees only), in-person voting at Polling Units with inked
  thumb, campaigning (posters, rallies), recall/accountability. **Real players only** —
  the old local simulation with NPC candidates/electorate is banned.
- **Player police:** see §3 (Phase 2: player cops arrest player criminals, shared case
  files, prison visits).
- **Unity mapping:** netcode behind a transport abstraction (per `MIGRATION-PLAN.md` §4);
  interest management = 200 m cells → NetworkObject visibility; server-authoritative
  identity lookup + caching; chat via a messaging service with the same channel model
  (lobby + ephemeral pair DMs + crew channels); presence via heartbeat.

---

## 9. UI / HUD

- **Phone (in-game smartphone — "your second life"):** apps — Bank (wallet, history, Send
  tab), Property, Jobs, Naija Ride (book rides; driver mode), Flights, Shop, Invest,
  Chat/Messages, Contacts/People, Nearby, Map (GTA-style map + radar), Events, Gov House +
  Ballot (elections), My Crew, Settings (incl. chat-privacy disclosure), NaijaGram (social
  feed), NaijaNews (news/wanted posters). Phone FAB 58 px; phone close 44 px; back button
  44 px. One action prompt top-left under the radar; one toast lane right of the radar;
  **nothing fixed over the middle of the screen**; toasts capped 3 live / 5 queued,
  deduped. Panels animate in/out (respect `prefers-reduced-motion`).
- **HUD:** location chip (follows player district — "Lagos"/"Abuja"), clock (WAT — one
  shared time value; readers add +1h themselves; never `getHours()`/`getDay()` for game
  time), wallet balance (server balance), vitals/needs pills (tap opens needs), wanted
  stars (top-right slot), story objective chip, goal chip, ride chip/offer — **stacking
  rule:** top-right slot rows at +52/+100/+148/+196, one element per row, goal hides while
  a story objective exists; no overlaps in any of 32 visibility combinations. Joystick
  124 px, camera 60 px, sprint 56 px, map button 56/46 px, drive pedals 64–104 px,
  pause/mute 44 px. All core actions ≥44 px touch targets; no hidden gesture required;
  every state you can tap into has a tappable way out (kind `exit` prompt for the whole
  duration — bus, building, card, job).
- **Prompts:** one prompt system only (`NLPrompt.offer`); locked/informational offers use
  kind `other` and explain on tap; no stacking yellow buttons; prompts never fight (stops
  >6 m from danfo stops so two prompts never overlap).
- **Notifications rules (owner UX):** no spam — banners/popups/toasts cut or consolidated;
  nothing interrupts play repeatedly. Network calls: pressed state at once, progress state
  within 300 ms ("Starting your hustle…"), 8 s timeout ending in a friendly retry, never a
  frozen button. Game-like minimal UI text: short, lively, Nigerian-flavoured copy
  (pidgin); no raw separators (`__`, `--`, `—`), no debug strings, no explanatory
  paragraphs a player doesn't need. No emoji in UI copy per pro-look rule (icons via SVG
  icon tiles instead).
- **Map:** GTA-style map + radar; stops show 🚏 pins with lines; district labels; building
  pins audited (no pin without a building under it). City tabs (Lagos/Abuja).
- **Brand:** Nigerian-flag palette — green (`--nl-green-flag`), gold gradient CTAs
  (`#ffe7a0` → gold → gold-deep, dark text `#2a1a00`), indigo night panels, glass cards;
  wordmark: small green-white-green flag, NAIJA in gold-soft, LIFE in white. Official logo
  files in `assets/brand/` — never redraw. Lagos-sunset orange is NOT the brand colour.
- **Cinematic language:** one cinematic language across the whole game — boot flyby,
  multi-shot city flyover, letterbox moments; palette pass beyond green/yellow.
- **Accessibility:** WCAG pass (aria-labels on phone apps/payments/auth; 44 px targets;
  reduced-motion respected).
- **Unity mapping:** Unity UI (UGUI/UI Toolkit) with a single prompt manager, toast lane
  manager, HUD stack manager (row-allocation rule as a layout group), Cinemachine for
  flyovers, TextMeshPro with the brand font; phone as a full-screen Canvas with app
  navigation stack.

---

## 10. PROGRESSION / ACHIEVEMENTS + OTHER SYSTEMS

- **Progression:** goal chip + Progress screen + unlock ladder with fanfare (E-7/E-8).
  Every way to earn must move the progress system (after one session, paths + goal must
  change — match on the source tag `job_payout`, never free-text reason strings).
  Unlocks: districts, vehicles, licences, property tiers, crew roles, offices. Rep tiers
  (see §8) gate loans/partnerships/areas — copy must match the code.
- **Life Record:** the game's permanent memory — charges, convictions, served time,
  expunged flag, three public lines on the street card; every interview/application/
  proposal checks it. Also tracks "In Lagos since", firsts, milestones.
- **Story mode ("Fresh for Lagos" first-day story + career arcs):** beginner storyline;
  Abuja first-day story; career consequences matrix (record gates jobs; jail cascades into
  missed shifts/rent/relationships).
- **Family (post-launch):** relationship status, marriage the Nigerian way (proposal;
  ceremonies — pick one or more; wedding planner phone app), household + children
  (accelerated growing up), separation, care meter (gentle, never cruel). Ground rules
  never broken (see family-spec).
- **Events calendar (not built):** Detty December, Sallah, street football, owambe hosting
  (spraying money — tap to spray ₦ notes that arc and land, picked up by celebrant's
  wallet, capped; hosting costs ₦80k–300k+).
- **World life:** rush hour go-slow + fuel queues; food spots per district (mama-put, suya,
  shawarma); clubs/nightlife (Club Shayo etc.); in-game artist music placements (original
  music; NPC voice lines — conductor/hawker calls, greetings, danfo stop calls, NEPA
  shouts — through one voice system, never raw speech synthesis); ambient Lagos beds
  (loops ≥60 s), UI sound kit, vehicle sounds. Street events: agberos/area boys,
  pickpockets, vendors (vendors give 10% off at Big Name rep).
- **Transit (riding as a passenger):** the clock is the timetable. Lines at launch: BRT 1
  (Oshodi→Ikeja Bus Terminal, 8 min, 05:30–22:00, ₦300+₦50/stop max ₦700) · Danfo Oshodi
  Oke→UNILAG Gate (5 min, 05:00–23:00, ₦200+₦50 max ₦350) · Danfo Arena→Stadium (5 min,
  ₦200+₦50 max ₦300) · Keke Jibowu→Makoko Docks (3 min, 06:00–22:00, ₦200 flat) · LagBus 1
  Oshodi East→Lekki Phase 1 via 3rd Mainland (12 min, 06:00–21:00, ₦300+₦75 max ₦800) ·
  Red Line Oyingbo→Agbado (20 min, 05:30–21:30, ₦350/₦500/₦750/₦1,000 by zone) · Abuja
  Bus 1 Area 1→Banex (10 min, ₦300+₦50 max ₦500) · Abuja Bus 2 Area 1→Eagle Square
  (12 min, max ₦450). Planned: Blue Line (Marina→Mile 2), ferry, Abuja Rail Yellow Line.
  Rider journey: walk to stop (sign pole/shelter, name on shelter) → read departure board
  (live countdown) → pick destination + fare → board (fare paid) → ride (conductor calls
  stops; exit prompt always there; tap early = next stop, no refund) → step down onto the
  kerb, never a teleport. Stops: on line centreline within 3 m, ≥20 m apart, never on a
  bridge deck, >6 m from danfo stops. Fares move to a server RPC (TR-12). Bus chat rooms
  (TR-7) planned.
- **Interiors:** enterable buildings (walk-in entry/exit, no teleport card); plans per
  building type (homes, offices, shops, towers with lifts — e.g. Eko Heights, NNPC Towers
  floors G/12/27/35); decor in every plan (windows, art, skirting, ceiling lights, wall
  plaques); greeting staff; lifts. Interiors at y = −40 in the old build — Unity
  equivalent: separate interior scenes/prefabs with their own lighting, not a world-Y
  offset.
- **Real-player ads:** player-bought ad space (Flutterwave) shown in-world (billboards,
  posters); parody names for private businesses (real names only for real landmarks/
  places); ad creative upload + campaign create after payment.
- **Monetization:** Flutterwave only; Naira Coins packs + ad space. (See §7.)
- **Streaming/LOD (B-35):** one city loaded at a time; districts wake within 600 m of
  edge; people/cars stream 150 m walking → 250 m at speed (ahead of travel direction);
  far = skyline shell beyond 300 m (one draw call per district); fixed memory backpack
  with a budget monitor (sleeps farthest districts over budget). Measured win: freeing
  far cities.
- **Unity mapping:** progression = ScriptableObject unlock ladder + server-recorded event
  feed; Life Record = player profile document; transit = timetable-driven vehicle spawner
  on splines (routes as ScriptableObjects with stop lists/fares/hours); interiors =
  additive scenes; ads = remote-config billboard textures; streaming = Addressables +
  Unity's built-in LOD/occlusion + a budget monitor.

---

## 11. WHAT DOES **NOT** CARRY OVER (browser-specific)

- **Everything in `js/`** — Three.js scene code, `nl-boot.js`/`LOAD_V` versioning, bundle
  builder (`tools/build-bundles.js`, `check-bundles.js`), `localStorage` keys
  (`nl-garage-v1`, `nl-hangar-v1`, `naija-life-startup-home-v1`, `nl-property-v1` — all
  were client caches, several were security holes), `quiz.html` client start grant (up to
  ₦500k — retired), `nl-chat-mock.js`, standalone `.html` pages (drive/ride/interior/
  chat/quiz) — all fold into the Unity game or are removed.
- **Supabase Realtime as the launch transport** — keep the *design* (cells, 200 m, 20-byte
  `mv`, 2/4 Hz rates, topic model) but re-implement on the Unity multiplayer stack (see
  `MIGRATION-PLAN.md` §4 — Photon Fusion 2 recommended). Supabase remains the *backend
  pattern* (Postgres RPCs, RLS, ledger); the RPC *contracts* (names, caps, error codes)
  carry over, the JS client does not.
- **Three.js specifics:** GLB Y-up/−Z conventions become Unity import conventions
  (convert on import); `world-sky.js` fog/sky-dome pinning, `nl-perf.js` draw-distance
  culling, `nl-camview.js` occlusion code — re-implement natively (URP fog, occlusion
  culling, Cinemachine).
- **Browser QA harness** (Playwright/Chromium, `tools/qa/*`, `transit-check.js`,
  `secret-scan.js` as a JS tool) — replaced by Unity PlayMode tests + device testing
  (owner checks on real iPhone/Android remain the gate; emulation alone never counts as
  DONE).
- **CSS/brand tokens** (`css/nl-brand.css`) — the *palette and logo assets* carry over;
  the CSS itself does not.
- **Known browser bugs to NOT re-implement:** phantom-hit radius (B-39), counter-based
  jail, `NL.cloud.client` lookup (use JWT fetch pattern — in Unity: authenticated HTTP
  client with the player token), `getHours()`/`getDay()` for game time, client-side money
  minting, `public.assets` "owners only FOR ALL" policy, client-posted `purchase`/`sale`
  ledger rows.
- **Frozen-state caveats (what was NOT verified at freeze, so Unity must verify fresh):**
  night lighting, helipad lights/prompts, Makoko gate re-fire, Banex/Garki drive-throughs,
  phone HUD overlap, wallet changes, rush-hour evidence, phone-width check, phone FAB tap,
  duplex interiors (camera), church/school cards, B-39 live-car iPhone drive, A-9/A-10
  Abuja street-life/night screenshots, Wuse Market night frames. Nothing in this inventory
  should be treated as "verified in-game" unless the source says Claude verified it in the
  browser at both sizes.

---

## 12. OPEN / OWNER-GATED DECISIONS THE UNITY TEAM MUST NOT ASSUME

1. **B-38 transfers:** spec approved for build; **applying migration 0040 needs
   Benjamin's separate OK**; B-27 (profiles) + B-29 (username lock) must go live first;
   Send tab stays hidden until the two-account test passes.
2. **Supabase Pro purchase** (before launch, spend cap off) — direction confirmed; actual
   purchase needs his yes on exact terms.
3. **Abuja homes at launch** — owner to confirm; only the owner opens cities.
4. **Property prices** — mansion ₦150M is an owner decision; economy ceiling note: the old
   daily caps made the ₦30M mansion deposit take 100+ days of maxed play — that was *with
   Benjamin as a decision* (Oct 17). Re-confirm caps in Unity.
5. **Voice:** Spirited Canyon is the *BenoBibleChronicles channel* voice, not the game
   voice — do not conflate. Game NPC voices go through the single voice system.
6. **Real-money scope:** Flutterwave = Naira Coins packs + ad space only. No other
   real-money purchases without his explicit yes on exact terms.
7. **"Real people only, no real politics"** is a standing owner rule (§0.9): no fake NPC
   players/votes/counts; no real party names, politicians, party colours/logos,
   cult/confraternity names anywhere.
8. **No voice chat; no teleport** (except the cinematic flyover/journey-home sequences,
   which are travel, not teleport — and property rule PR-7: you travel home yourself).
9. **MudWorks is a separate, stopped project** — nothing from it carries into Naija Life.

*No code was written, no files edited, no builds made — read-only inventory as tasked.*
