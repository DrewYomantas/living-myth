# UE 5.8 Generated Atlas Visual V4 — lit, inspectable myth-atlas (pure Python, no C++)

_2026-06-20. Follows Generated Atlas Visual V3. Python-first, Claude-runnable headless end-to-end.
New level `GeneratedAtlasV4`; V2 and V3 are left untouched._

Turns V3's coherent-but-**flat** island diagram into a **lit** atlas model: a warm late-afternoon
key light, sky + cool fill, height fog, and a locked-exposure post volume — with land/prop emissive
**dropped** so the lighting actually shades the forms (no flat emissive-only look), while the gold
chronicle spine and memory markers still glow. Plus a cheap **ISM batching** pass and a composed
camera. **100% data honesty preserved (0 violations).** Engine basic shapes + authored materials
only; no external/Fab/Marketplace/generative assets, no C++.

## What changed vs V3

| Area | V3 | V4 |
|---|---|---|
| Lighting | none (flat, emissive-only) | **DirectionalLight** (warm, pitch −17° late-afternoon) + **SkyAtmosphere** + **SkyLight** (cool fill, real-time capture) + **ExponentialHeightFog** + **PostProcessVolume** — all **Movable / no-bake** (V4.1) so the level opens with no "lighting needs to be rebuilt" warning |
| Exposure | n/a | **locked** (min==max auto-exposure brightness = 1.0) so the scene never pumps; gentle bloom 0.42 + vignette 0.30 |
| Land shading | self-glowing (emissive ≈0.16) | emissive **0.05** — lit by the key light; markers/gold stay emissive so they read as glowing |
| Materials | `MI_v3_*` | **new `MI_v4_*`** on the **reused** `M_LMAtlasV3` master (V3's instances never mutated → V3 look unchanged); V2 `MI_site_*` reused for site pins |
| Batching | 854 individual actors | **2 InstancedStaticMesh holders** absorb 252 uniform cubes (fringe+bridges, chronicle links) → **649 actors** (would be **899** unbatched) |
| Camera | `CAM_GeneratedAtlasV3_AtlasView` | **`CAM_GeneratedAtlasV4_AtlasView`** (pitch −46°, FOV 52, frames whole world) |
| Capture | none (camera saved) | **`tools/ue_capture_v4.py`** editor-side helper (viewport present → real PNG) |

## Source JSON

`Content/LivingMyth/Data/imported_seed1_year250_snapshot.json` (schema `1.0.0`, Ysmere, seed 1, yr 250).
Rebuildable: rerun the command and the level regenerates byte-for-byte from this file.

## Command (headless, Claude-runnable, alongside an open editor)

```bash
"C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe" \
  "C:/dev/LIVING MYTH/unreal/LivingMythDiorama/LivingMythDiorama.uproject" \
  -run=pythonscript \
  -script="C:/dev/LIVING MYTH/unreal/LivingMythDiorama/tools/ue_import_renderer_v4.py" \
  -unattended -nosplash -nopause
```
Runs **with** RHI (lighting/material compile need it). Idempotent: builds a fresh untitled world and
`save_map`s it as `GeneratedAtlasV4` (overwrite-safe — no `new_level` "asset already exists" fragility).

## Generated level / assets

- **Level:** `/Game/LivingMyth/Maps/GeneratedAtlasV4` (`…/Maps/GeneratedAtlasV4.umap`, ~1.16 MB, **649 actors**).
- **Materials:** reuses master `M_LMAtlasV3`; **new** 14× `MI_v4_*` (ocean, fringe, gold, leader, roof,
  6× `role_*`, 3× `mk_*`). Reuses V2's `MI_site_*` for site pins. **V3's `MI_v3_*` are untouched.**
- **Camera:** `CAM_GeneratedAtlasV4_AtlasView`.
- **Capture helper:** `tools/ue_capture_v4.py` (editor-side).

## Verdict file

`Saved/import_renderer_v4_verdict.json` — last run:

```
imported: regions 22 · factions 3 · sites 100 · memoryMarkers 60 · chronicleBeats 7
markersPlaced 60/60 · markersSkippedUnplaceable 0 · honestyViolations 0
markerCountsByKind: faction_pulse 18 · home_memory_cairn 18 · true_place_mark 18 · chronicle_beat 6
lighting: directional + skyAtmosphere + skyLight + heightFog + postProcess · exposureLocked true
          allLightsMovable true · forceNoPrecomputedLighting true · lightingRebuildWarningResolved true
batching: ismHolders 2 · ismInstances 252 (fringe+bridges 186 · chronicleLinks 66)
renderOnlyOffsetsUsed 5 · renderOnlyContraction 0.90 · bridgesDrawn 10 · unanchoredBeatsRailed 1
labelsShown 20 · labelsHidden 13
actors total 649  (lights 5 · ocean 1 · region_land 110 · region_props 221 · sites 100 ·
                   markers 162 · beats 14 · labels 20 · leaders 13 · ism_holders 2 · camera 1)
actorCountIfUnbatched 899 · actorsSavedByBatching 250
ratings: dataTruthImportCorrectness 10/10 · currentAtlasReadability 8/10 · finalIlluminatedDioramaAtlasTarget 4/10
captureAttempted false · captureProduced false
```

## Ratings (V4.1, recalibrated — reported separately)

| Rating | Score | Basis |
|---|---|---|
| **Data-truth / import correctness** | **10/10** | `honestyViolations 0`; every actor snapshot-derived; cairns at `homeRegionId`, unanchored beat railed; RegionId/HomeRegionId never conflated. |
| **Current atlas readability** | **8/10** | From `CAM_GeneratedAtlasV4_AtlasView` a human reads the world: lit island, labeled regions, distinct marker languages, golden chronicle path. Limits: floating-plate labels, primitive forms. |
| **Final Illuminated Diorama Atlas target** | **4/10** | No painterly/label/UI/art-direction leap yet — lighting + no-bake is a polish step, not the hero look. Holds at ~4 until custom meshes/materials/label-cards land. |

## V4.1 cleanup — lighting-rebuild warning fixed (no-bake)

The first V4 build opened with `LIGHTING NEEDS TO BE REBUILT (1 unbuilt object)` — the default
stationary SkyLight/DirectionalLight want a precomputed-lighting bake, which is nonsense for a level
regenerated from JSON every run. Fixed **in the pipeline** (no manual Build Lighting):

- Every spawned light component set to **`ComponentMobility.MOVABLE`** (fully dynamic; SkyLight stays
  on real-time capture).
- World Settings **`force_no_precomputed_lighting = True`** on the generated level.
- The renderer **verifies** before saving: it scans all `LightComponentBase`s for any non-Movable one
  and reads back the world flag, recording `allLightsMovable` / `forceNoPrecomputedLighting` /
  `lightingRebuildWarningResolved` in the verdict (last run: **all true**, no non-movable lights).

Result: GeneratedAtlasV4 opens with **no lighting-rebuild warning**, fully dynamic.

> Note: the editor **locks** `GeneratedAtlasV4.umap` + its `MI_v4_*` while the level is open
> (`MoveFile … Error Code 32` on save). Re-running the renderer requires the level **closed** in the
> editor (File ▸ New Level / Empty, or close the editor) — same lock discipline as the V3 `MI_*` case.

## Lighting / camera / capture status

- **Lighting:** all five actors spawned and configured headless (exit 0), **Movable / no-bake** (V4.1).
  Key light is warm
  (`LinearColor(1.0, 0.80, 0.56)`, intensity 4, marked `atmosphere_sun_light`); SkyLight cool fill with
  real-time capture; height fog density 0.006; post volume unbound with **exposure pinned** (min==max
  brightness 1.0) so an emissive-heavy scene can't blow out, plus mild bloom + vignette.
- **Camera:** `CAM_GeneratedAtlasV4_AtlasView` saved, composed top-down/angled (pitch −46°, FOV 52).
- **Capture: still NOT possible headless** — a `-run=pythonscript` commandlet has no render viewport and
  `take_high_res_screenshot` native-crashes it (proven in V3). **Editor-side path delivered instead.**
  In the **open editor's Python console** (Output Log ▸ Cmd dropdown ▸ Python), run the one-liner:

  ```
  py "C:/dev/LIVING MYTH/unreal/LivingMythDiorama/tools/ue_capture_v4.py"
  ```

  It loads GeneratedAtlasV4, snaps the perspective viewport to `CAM_GeneratedAtlasV4_AtlasView`, enables
  game view, and writes `Saved/Screenshots/GeneratedAtlasV4.png`. (Manual fallback: in the editor pick the
  camera in the World Outliner ▸ right-click ▸ *Pilot 'CAM_GeneratedAtlasV4_AtlasView'*, then
  `Window ▸ High Resolution Screenshot`.) A fully automated headless PNG needs a `-game`/MovieRenderQueue
  path — deferred to V5.

## Honesty checks (unchanged contract, still 0 violations)

- **home_memory_cairn ⇒ regionId null**, anchored at `homeRegionId`; all 18 passed; a non-null regionId
  would be counted, logged, and dropped. Renders as a low **stone stack**, never an event spike, never
  labelled as an event location.
- **RegionId vs HomeRegionId never conflated** (separate code paths).
- **Chronicle beat 0 (both null)** railed off-world (`unanchoredBeatsRailed: 1`), not given a fake region.
- **Render-only layout counted, not hidden:** contraction 0.90, 5 region de-overlaps, 10 bridge strips —
  DISPLAY LAYOUT only; none change which region/anchor anything belongs to. Bridges are proximity guides,
  not sim-authored adjacency (documented).

## Failures / workarounds (V4 capability map — proven by probes before the real run)

Three incremental-flush probes (`ue_probe_v4*.py`, since deleted) mapped the new calls before committing:

- **`-nullrhi` breaks lighting** — a directional-light component under nullrhi stalled the probe. The real
  renderer runs **with** RHI.
- **`set_light_color` wants `LinearColor`, not `Color`** (the V2 text-color path used `Color`); passing a
  `Color` throws a nativize error.
- **`SkyLight` has no `sky_light_component` attribute** — fetch the component via
  `get_component_by_class(unreal.SkyLightComponent)` (same for DirectionalLight/Fog components).
- **`ExponentialHeightFogComponent.fog_inscattering_color` doesn't exist** by that name in 5.8 — fog colour
  left default; `fog_density`/`fog_height_falloff` set fine.
- **`Actor.add_component_by_class` / `Component.register_component` don't exist** in this binding. **ISM
  recipe that works AND serializes** (verified by a save→reload round-trip, 5/5 instances survived):
  `spawn_actor_from_class(Actor)` → `InstancedStaticMeshComponent(actor)` → `set_editor_property(
  "root_component", ism)` → `set_static_mesh`/`set_material` → `add_instance(transform, world_space=True)`.
- **`PostProcessVolume.settings`** is a writable struct: set `override_*` flags + values, then assign the
  struct back with `set_editor_property("settings", s)`. Locking exposure via min==max brightness is more
  robust than the manual-metering bias guess.
- **Capture unchanged:** `take_high_res_screenshot` native-crashes headless — not called; editor helper ships.

## Visual problems that remain (honest)

- Still **primitive geometry** — lighting lifts it to a believable *model*, but it's not the painterly
  North Star hero look (`northStarTargetRating 4/10`). Closing that is custom meshes/textures/decals — art.
- **No baked GI / Lumen tuning** in the generated level beyond the actors placed; shadow softness and
  aerial perspective are at engine defaults.
- **TextRender labels** are still floating plates (engine-default type), readable but not "cards."
- **Bridges remain proximity strips**, not sim adjacency.
- **Capture still manual/editor-side** — no headless PNG yet.

## Recommendation for V5

1. **Automated capture:** a `-game`/MovieRenderQueue (or editor-utility-widget) path so a real PNG is
   produced without a human — the one thing V4 still can't self-verify.
2. **Material upgrade:** swap flat MICs for a lit master with a subtle gradient/normal/roughness so the
   forms catch the key light with shape; faction-tinted ground decals; a real water material on the ocean.
3. **More ISM coverage:** batch sites and same-mesh props per role (cut the remaining ~580 actors further).
4. **Label cards:** swap TextRender for a billboarded card material or WidgetComponent for legible chrome.
5. **Real adjacency** for bridges from a sim-authored neighbour signal (if exported).
6. Keep C++ off unless a runtime/Blueprint diorama is wanted; the Python renderer stays the authoring path.

## Files

- `tools/ue_import_renderer_v4.py` — the V4 renderer (pure Python, deterministic, idempotent).
- `tools/ue_capture_v4.py` — editor-side capture helper.
- `Content/LivingMyth/Maps/GeneratedAtlasV4.umap` — generated level.
- `Content/LivingMyth/Materials/MI_v4_*` — 14 authored instances (master `M_LMAtlasV3` reused).
- `Saved/import_renderer_v4_verdict.json` — machine-readable verdict.
- `Content/LivingMyth/Data/imported_seed1_year250_snapshot.json` — source snapshot (unchanged).
```
