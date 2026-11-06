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
  (`Assets/Editor/AssetImportMaterialPass.cs`) upgrades them by material name:
  glass → transparent, signage → emissive, the rest → tuned URP Lit.
- **Characters: ZERO animation clips exist anywhere** — `anims/` was empty in
  the source repo. Walk/idle/run must be authored from scratch (Mixamo or
  hand-made) — do not expect the rigs to move on their own.
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
