# Imported Assets — provenance

Source: the FROZEN browser version, `~/workspace/naija-life` (read-only reference).
Imported 2026-11-05 by the asset-import workstream (branch `asset-import`).
Importer: glTFast (`com.unity.cloud.gltfast` 6.15.1) — Unity has no built-in GLB importer.

## What is here (235 GLBs, 45 MB)

| Folder | Count | Contents |
|---|---|---|
| `Buildings/` | 95 | All 94 building GLBs from `models/buildings/` (abuja/ikeja/lekki/makoko/ph + root: Oshodi terminals, National Mosque, National Assembly, Transcorp Hilton, Makoko stilt set, 45-part Lagos kit as `Lagos-kit.glb`) |
| `Characters/Player/` | 2 | `body_m.glb`, `body_f.glb` — v2 player bodies, 59-bone skinned rigs. `body_m` is wired into `Assets/Prefabs/Player/Player.prefab` |
| `Characters/Bodies/` | 10 | v2 NPC bodies (m/f, average/big/fit/older/slim/curvy/full) |
| `Characters/Hair/` | 45 | Hair, braids, wigs, hats, fila, gele, hijab |
| `Characters/Outfits/` | 44 | Ankara, adire, agbada, asooke, corporate, uniforms, jerseys — pattern textures embedded in the GLBs |
| `Characters/Accessories/` | 16 | Glasses, watches, bags, coral beads, walking sticks |
| `Characters/Shoes/` | 16 | Sneakers, sandals, boots, heels, loafers |
| `Vehicles/Transit/` | 5 | danfo, keke, brt-12m, bus-10m, redline-dmu (train) |
| `Vehicles/` | 2 | okada, police_pickup |

## Known state (from the Nov 5 asset audit)

- **Buildings: ZERO embedded textures** — flat named materials only
  (`A4_glass`, `A4_gold`, `MK_wood`, `MK_rust`, …). The URP material pass
  (`Assets/Editor/AssetImportMaterialPass.cs`) upgrades them by material name.
  **v2 result (Nov 6): glTFast does NOT generate URP/Lit materials — it
  generates its own `glTF/PbrMetallicRoughness` Shader Graph materials even
  under URP** (1,491 materials, all that shader). The pass targets the glTF
  properties (`baseColorFactor` / `roughnessFactor` / `metallicFactor` /
  `emissiveFactor` / `_Surface`): **557 materials changed** — 115 glass →
  transparent (a=0.35), 115 signage → emissive 0.6×base, 100 metal → 0.9,
  122 masonry → rough 0.85, 67 wood → rough 0.5, 14 rust, 14 lights, 7 tire,
  3 skin. **Reimport safety:** glTFast regenerates materials on reimport,
  which would wipe the pass — `Assets/Editor/AssetImportPostprocessor.cs`
  re-applies it automatically whenever a `.glb` under `Assets/Imported/` is
  (re)imported.
- **Meshopt (Nov 6): 72 of the 133 character GLBs use
  `EXT_meshopt_compression` (+ `KHR_mesh_quantization`) and FAILED their first
  import** — glTFast needs the separate free package
  `com.unity.meshopt.decompress` (0.3.0-exp.1, Unity registry) to decode them.
  It is now in `Packages/manifest.json`; the 72 files were force-reimported
  (the wire run) and all import cleanly now. The GLBs stay byte-identical to
  the frozen source — no decompression was needed.
- **⚠️ Player prefab CharacterController class-ID defect (found Nov 6, ON
  MAIN too): the prefab's "CharacterController" was saved with YAML class ID
  61 — but 61 is `BoxCollider2D`; `CharacterController` is 143** (Unity YAML
  Class ID Reference). On load, Unity instantiates class 61, i.e. a
  BoxCollider2D, and drops the CharacterController properties — so on main,
  `GetComponent<CharacterController>()` returns null and M1 movement cannot
  work. The wire run's re-save EXPOSED this (it wrote the honest
  `BoxCollider2D:`); the asset-import branch hand-fixes it to `!u!143` with
  the original values (slope 45, step 0.3, skin 0.08, center (0,0.9,0),
  radius 0.35, height 1.8). **UNVERIFIED in the Editor** (license died before
  the fix could be loaded — see below); M1 verification MUST confirm the
  CharacterController loads and the player moves before this branch merges,
  and main needs the same fix.
- **Characters: ZERO animation clips exist anywhere** — `anims/` was empty in
  the source repo. Walk/idle/run must be authored from scratch (Mixamo or
  hand-made) — do not expect the rigs to move on their own. **Also: glTFast
  did NOT generate an Avatar for body_m** (verified Nov 6) — the Player
  prefab's Animator.avatar is unset and the rig renders in bind pose until
  an Avatar is generated for the 59-bone skeleton.
- **Materials are placeholder-flat** on v2 bodies (skin/eye); outfits carry
  real embedded pattern textures.
- **Deliberately NOT imported:** v1 part-doll characters (`models/*_<part>.glb`,
  36 files — junk, unskinned, no UVs), hero danfo/sedans/SUVs
  (`vehicles/danfo.glb`, `uber_sedan*.glb`, `uber_suv*.glb` — boxes, they belong
  to a rebuild workstream), external texture PNGs (patterns are embedded in
  the outfit GLBs; building/vehicle wear PNGs stay in the frozen repo until a
  material pass needs them).

## Conventions

- GLB scale: Blender metres, Z-up → glTFast converts to Unity Y-up. Check
  forward axis per model in the test scene (`Assets/Editor/AssetImportVerify.cs`
  places samples); vehicles face -Z in the source convention.
- Do not edit the imported `.glb` files in place. Material overrides live in
  `Assets/Editor/AssetImportMaterialPass.cs` (re-runnable) — the GLBs stay
  byte-identical to the frozen source so they can be re-imported cleanly.
