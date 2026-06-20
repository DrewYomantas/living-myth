# UE 5.8 Generated Atlas Visual V3 — coherent myth-atlas (pure Python, no C++)

_2026-06-20. Follows Import Renderer V2. Python-first, Claude-runnable headless end-to-end. New
level `GeneratedAtlasV3`; V2 is left untouched._

Turns the V2 debug-plate blockout (black void, separated square plates, identical vertical sticks)
into one **coherent island atlas** — ocean, irregular landmasses, role-distinct silhouettes, a
distinct marker language per kind, a golden chronicle spine, and leader-lined labels — while keeping
**100% data honesty** (0 violations). Engine basic shapes + authored materials only; no
external/Fab/Marketplace/generative assets, no C++.

## What changed vs V2

| Area | V2 | V3 |
|---|---|---|
| Background | black void | **dark-blue ocean plane** under the whole atlas |
| Cohesion | scattered plates | gentle centroid **contraction (0.90)** + **10 shore/bridge strips** between nearby regions |
| Region shape | one square plate | **irregular landmass** (5 overlapping rotated planes) + sand/rock **fringe ring** |
| Silhouettes | simple props | forest canopies (cone+sphere) · highland crags · settlement huts+roofs+keep · coast rocks · standing-stone rings · muted mounds |
| Markers | near-identical poles | **distinct languages** — true_place = ring+flag · cairn = low stone stack · pulse = banner · beat = gold node |
| Chronicle | thin spine | thicker **golden pillars + node + 12-seg ribbon**; shared regions offset-arced & lifted; unanchored beat railed off-world |
| Labels | 28, no leaders | **fewer (20), leader-lined**, importance-ranked (seat/role/events/sites/marker-count) |
| Materials | fixed-emissive master | new **`M_LMAtlasV3`** (BaseColor + **EmissiveStrength** scalar) → matte ocean, glowing gold, tinted land |
| Camera | none | **`CAM_GeneratedAtlasV3_AtlasView`** framing the whole world |

## Source JSON

`Content/LivingMyth/Data/imported_seed1_year250_snapshot.json` (schema `1.0.0`, Ysmere, seed 1, yr 250).
Rebuildable: rerun the command and the level regenerates byte-for-byte from this file.

## Command (headless, Claude-runnable, alongside an open editor)

```bash
"C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe" \
  "C:/dev/LIVING MYTH/unreal/LivingMythDiorama/LivingMythDiorama.uproject" \
  -run=pythonscript \
  -script="C:/dev/LIVING MYTH/unreal/LivingMythDiorama/tools/ue_import_renderer_v3.py" \
  -unattended -nosplash -nopause
```
Also runs in the editor Python console. Idempotent: builds a fresh untitled world and `save_map`s it
as `GeneratedAtlasV3` (overwrite-safe — no `new_level` "asset already exists" fragility).

## Generated level / assets

- **Level:** `/Game/LivingMyth/Maps/GeneratedAtlasV3` (`…/Maps/GeneratedAtlasV3.umap`, ~1.5 MB, **854 actors**).
- **Materials:** new master `M_LMAtlasV3` + 14 `MI_v3_*` instances (ocean, gold, fringe, leader, roof,
  6× `role_*`, 3× `mk_*`). Reuses V2's `MI_site_*` for site pins where present.
- **Camera:** `CAM_GeneratedAtlasV3_AtlasView`.

## Verdict file

`Saved/import_renderer_v3_verdict.json` — last run:

```
imported: regions 22 · factions 3 · sites 100 · memoryMarkers 60 · chronicleBeats 7
markersPlaced 60/60 · markersSkippedUnplaceable 0 · honestyViolations 0
markerCountsByKind: faction_pulse 18 · home_memory_cairn 18 · true_place_mark 18 · chronicle_beat 6
renderOnlyOffsetsUsed 5 · renderOnlyContraction 0.90 · bridgesDrawn 10 · unanchoredBeatsRailed 1
labelsShown 20 · labelsHidden 13
actors total 854  (ocean 1 · region_land 110 · region_fringe 136 · region_props 221 · bridges 10 ·
                   sites 100 · markers 162 · beats 14 · beat_links 66 · labels 20 · leaders 13 · camera 1)
visualReadabilityRating 7/10 · northStarTargetRating 3/10 · dataTruthRating 10/10
captureAttempted false · captureProduced false
```

## Honesty checks (unchanged contract, still 0 violations)

- **home_memory_cairn ⇒ regionId null**, anchored at `homeRegionId`; all 18 passed; a non-null regionId
  would be counted, logged, and dropped. It renders as a low **stone stack**, never a place spike, and is
  never labelled as an event location.
- **RegionId vs HomeRegionId never conflated** (separate code paths).
- **Chronicle beat 0 (regionId & homeRegionId both null)** is railed off-world (`unanchoredBeatsRailed: 1`),
  **not** given a fabricated region.
- **Render-only layout is counted, not hidden:** contraction 0.90, 5 region de-overlaps, 10 bridge strips —
  all documented as DISPLAY LAYOUT; none change which region/anchor anything belongs to.

## Failures / workarounds (this pass)

- **`take_high_res_screenshot` native-crashes** a `-run=pythonscript` commandlet (no render viewport;
  bypasses Python try/except). Removed from the headless path — capture reported honestly as unavailable.
- **`new_level` refused** with "asset already exists" when a prior run left a `.umap` the fresh process's
  asset registry still listed (even after deleting the file). **Fix:** switched to
  `EditorLoadingAndSavingUtils.new_blank_map(False)` + `save_map(world, path)`, which overwrites by path.
- **`save_directory(dir, only_if_is_dirty=False, …)` errored** trying to re-save V2's `MI_*` while the open
  editor held them locked. **Fix:** `only_if_is_dirty=True` — save only this run's new assets.

## Visual problems that remain (honest)

- It's still **primitive geometry** — a clean diagram, not the painterly North Star hero look
  (`northStarTargetRating 3/10`). Closing that is custom meshes/textures/lighting, barred this pass.
- **Flat unlit feel:** no directional light/sky/post in the generated level, so colour reads come from
  emissive only; a lighting + sky + simple post pass would lift it a lot.
- **854 individual actors** (no instancing) — fine to view, heavy to edit; ISM batching is wanted.
- **TextRender labels** are still floating plates; readable but not "cards." Leader lines help but the type
  is engine-default.
- **Bridges are naive** (straight sand strips between centroids) — they imply adjacency that is proximity,
  not sim-authored adjacency; kept few (10) and documented as display-only.

## Recommendation for V4

1. **Lighting/atmosphere pass** (biggest cheap win): add DirectionalLight + SkyAtmosphere + SkyLight +
   ExponentialHeightFog + a subtle PostProcessVolume to the generated level — turns the flat diagram into a
   lit world without any new art.
2. **Automated capture:** a `-game`/MovieRenderQueue or editor-utility capture path so a real PNG is produced
   headlessly (the one thing V3 still can't self-verify).
3. **ISM batching** for props/fringe/sites (854 actors → a handful of instanced-static-mesh actors).
4. **Real adjacency** for bridges from a sim-authored neighbour signal (if exported), instead of proximity.
5. Keep C++ off unless a runtime/Blueprint diorama is wanted; the Python renderer stays the authoring path.

## Files

- `tools/ue_import_renderer_v3.py` — the V3 renderer (pure Python, deterministic, idempotent).
- `Content/LivingMyth/Maps/GeneratedAtlasV3.umap` — generated level.
- `Content/LivingMyth/Materials/{M_LMAtlasV3, MI_v3_*}` — authored master + instances.
- `Saved/import_renderer_v3_verdict.json` — machine-readable verdict.
- `Content/LivingMyth/Data/imported_seed1_year250_snapshot.json` — source snapshot (unchanged).
