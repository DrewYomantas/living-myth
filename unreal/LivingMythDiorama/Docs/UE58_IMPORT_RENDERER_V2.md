# UE 5.8 Import Renderer V2 — readable generated atlas (pure Python, no C++)

_2026-06-20. Follows Import Smoke V1. Python-first; C++ off the critical path. Claude-runnable end-to-end._

Turns the honest Living Myth snapshot from a debug-draw **blockout** (V1) into a **saved, readable
generated atlas level** built entirely from JSON by deterministic editor scripting — no C++, no
external/Fab/Marketplace/generative assets (engine basic shapes + one authored material only).

## What changed vs V1

| | V1 (smoke) | V2 (renderer) |
|---|---|---|
| Output | ephemeral `DrawDebug*` lines (needs live viewport, not saved) | **persistent actors in a saved `.umap`** |
| Layout | raw normalized x/y (regions overlapped) | uniform-fit footprint + **deterministic de-overlap** (3 regions moved) |
| Silhouettes | one debug box per region | **role-distinct prop clusters** (forest cones / highland peaks / settlement keep+houses / coast dock-posts / ruins-pillars / grassland shrubs) |
| Sites | points, could stack | all 100 imported + **within-region de-stack**; seats larger; colored by `displayRole` |
| Markers | lines | **kind-distinct silhouettes** (cairn stack / pulse cone / place-mark sphere), ring-arranged per region |
| Chronicle | — | **all 7 beats** as gold pillars + connecting spine; shared regions offset-arced + lifted; **unanchored beat railed honestly** |
| Labels | `DrawDebugString` all | **importance-scored `TextRenderActor`s, culled** (28 shown / 11 hidden) |
| Color | vertex debug colors | **authored master material + per-role/-kind `MaterialInstanceConstant`s** (persist in the saved map) |
| Verdict | basic | full: counts, marker kinds, render-only offsets, label cull, actor counts, self-ratings |

## Source JSON

`Content/LivingMyth/Data/imported_seed1_year250_snapshot.json`
(byte-identical copy of the committed bridge sample `Content/Snapshots/reference_seed1_year250.json`;
schema `1.0.0`, world **Ysmere**, seed 1, year 250).

## Command (Claude-runnable, headless, alongside an open editor)

```bash
"C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe" \
  "C:/dev/LIVING MYTH/unreal/LivingMythDiorama/LivingMythDiorama.uproject" \
  -run=pythonscript \
  -script="C:/dev/LIVING MYTH/unreal/LivingMythDiorama/tools/ue_import_renderer_v2.py" \
  -unattended -nosplash -nopause
```
Also runs verbatim in the open editor's Python console (`py ".../ue_import_renderer_v2.py"`).
Deterministic + idempotent: deletes any prior `GeneratedAtlasV2` and rebuilds from JSON every run.

## Generated level

`/Game/LivingMyth/Maps/GeneratedAtlasV2` (`Content/LivingMyth/Maps/GeneratedAtlasV2.umap`, ~835 KB,
393 actors). Materials: `Content/LivingMyth/Materials/M_LMAtlas` + 19 `MI_*` instances.

## Verdict file

`Saved/import_renderer_v2_verdict.json` — last run:

```
regions 22 · factions 3 · sites 100 · memoryMarkers 60 · chronicleBeats 7
markersPlaced 60/60 · markersSkippedUnplaceable 0 · honestyViolations 0
markerCountsByKind: faction_pulse 18 · home_memory_cairn 18 · true_place_mark 18 · chronicle_beat 6
renderOnlyRegionOffsetsUsed 3 · unanchoredBeatsRailed 1 · labelsShown 28 · labelsHidden 11
generatedActors: region_tiles 22 · region_props 122 · sites 100 · markers 60 · beat_pillars 7 · beat_links 54 · labels 28  (total 393)
visualReadabilitySelfRating 6/10 · dataTruthSelfRating 10/10
```

## Honesty checks (the load-bearing contract)

- **`home_memory_cairn` ⇒ regionId must be null**, anchored at `homeRegionId`. All 18 cairns passed;
  a non-null regionId would be counted, logged, and the marker dropped (never rendered "happened here").
  **honestyViolations: 0.**
- **RegionId (where it happened) vs HomeRegionId (where remembered) never conflated** — separate code paths.
- **Nothing fabricated.** Chronicle beat 0 is fully unanchored (`regionId` & `homeRegionId` both null);
  it is lifted to an honest "unanchored" rail above the centroid, **not** given a fake region
  (`unanchoredBeatsRailed: 1`).
- **Render-only offsets are counted, not hidden.** 3 near-duplicate region centers (e.g. regions 0/2,
  the 7/12/18 highland cluster) were pushed apart deterministically for legibility; sites move with their
  region and then de-stack locally. These change pixels, never which region/anchor a thing belongs to.

## Failures / workarounds (the headless capability map — proven this pass)

Discovered by isolated capability probes (each writing progress before every call so a native crash
names its own culprit):

- **`EditorActorSubsystem.spawn_actor_from_object(mesh, …)` FATALS** in a `-run=pythonscript` commandlet
  (native crash, bypasses Python try/except) — both with and without `-nullrhi`, in the transient world
  and in a freshly created level. **Workaround / proven recipe:** `spawn_actor_from_class(StaticMeshActor)`
  then `static_mesh_component.set_static_mesh(...)`. This path is rock-solid headless.
- **`-nullrhi` is irrelevant to the spawn crash** (RHI was not the variable); but the final renderer runs
  **with** RHI so `MaterialEditingLibrary.recompile_material` produces correct shaders.
- **`new_level` refuses if the asset already exists** ("Failed to validate the destination"); a silent
  failure then leaves the current world as the transient untitled and `save_current_level` fails ("no
  filename"). **Workaround:** delete the prior level asset first, then assert the active world is the
  expected package before spawning/saving.
- **`unreal.HorizontalTextAligment` does not exist** — the engine enum is the misspelled
  `HorizTextAligment`. The renderer probes several spellings and degrades to default alignment rather than
  abort.
- **Headless visual capture: not available.** A commandlet has no render viewport, so an in-engine
  screenshot yields no frame (same reason V1 debug-draw needed a live editor). Not attempted live this
  pass to avoid a false "captured" claim. **Practical capture path:** open `GeneratedAtlasV2` in the
  editor (top-down) and screenshot, or wire a `-game` capture map in V3.

## Why no C++ (and how V2 stays off it)

C++ in Import Smoke V1 was over-engineering — it forced a compile + Live-Coding + reflection rebuild +
DLL lock that fought headless runs. V2 is **100% editor Python**: load JSON → author material →
`new_level` → `spawn_actor_from_class` → `save_current_level` → write verdict. The whole thing is
rebuildable from JSON and runnable by Claude end-to-end with zero editor clicks.

## Next recommendation (V3)

1. **Capture**: add a `-game`/PIE capture map (player start + top-down `SceneCapture2D`) or an editor
   `HighResShot` path so a screenshot is produced automatically — the only thing V2 can't self-verify.
2. **Readability 6→7.5**: instanced-static-mesh batching for props (cut 393 actors → a few ISM actors);
   region boundary outlines / a ground plane tinted by controlling faction; leader lines from labels to
   anchors; a faction-tint pass on settlement tiles.
3. **Parameterize** the renderer over `--seed/--year` snapshots (regenerate the bridge JSON, drop into
   `Content/LivingMyth/Data/`, rerun) to prove it generalizes beyond seed 1.
4. **Promote to C++ only if** a runtime/Blueprint diorama (not an editor diagram) is wanted — after the
   data-driven layout is locked. The Python renderer stays the authoring path.

## Files

- `tools/ue_import_renderer_v2.py` — the renderer (pure Python, deterministic, idempotent).
- `Content/LivingMyth/Data/imported_seed1_year250_snapshot.json` — source snapshot.
- `Content/LivingMyth/Maps/GeneratedAtlasV2.umap` — generated level.
- `Content/LivingMyth/Materials/{M_LMAtlas, MI_*}` — authored master + instances.
- `Saved/import_renderer_v2_verdict.json` — machine-readable verdict.
