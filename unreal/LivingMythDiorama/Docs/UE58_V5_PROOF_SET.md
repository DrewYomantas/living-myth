# UE 5.8 — Editable Atlas Proof Set V1 (Living Myth V5 first asset modeling)

_2026-06-20. Pure Python + **Geometry Script**, no C++. A tiny THREE-asset proof set for the V5
"handcrafted, illuminated, mythic diorama" direction — reusable modular pieces, NOT a baked
illustrated map. Builds genuine `SM_` StaticMesh assets (material zones, noise-broken silhouettes,
ground-center pivots), a small proof scene, and three review captures. V2/V3/V4 untouched; nothing
here reads or writes sim state._

## Why Geometry Script (not primitive actor clusters like V3/V4)

The V3/V4 atlas composes engine-primitive StaticMeshActors. The V5 brief needs *reusable assets with
material zones and proper pivots*. So this pass uses the **GeometryScripting** plugin (enabled in
`LivingMythDiorama.uproject` — an Epic engine plugin for mesh authoring; it does not touch the import
contract): a `DynamicMesh` is assembled from `GeometryScript_Primitives.append_box/cone/cylinder/
sphere` (each append's `GeometryScriptPrimitiveOptions.material_id` becomes a **material slot**),
displaced with `GeometryScript_MeshDeformers.apply_perlin_noise_to_mesh` (broken outline + surface
undulation), normals recomputed, then written to a real asset with
`GeometryScript_NewAssetUtils.create_new_static_mesh_asset_from_mesh`. Append `origin=BASE` gives a
**ground-center bottom pivot** for free, so a piece can be placed on a generated cell and
raised/lowered/scaled/animated up from the surface.

## The three assets (`/Game/LivingMyth/V5/`)

| Asset | Zones (material slots) | Approx scale (cm) | Pivot |
|---|---|---|---|
| `SM_LM_Terrain_GrassStoneChunk_A` | GrassMoss · Dirt · Stone · FoliageAccent | 432×432 foot, ~110 tall | ground-center bottom |
| `SM_LM_Path_DirtStraight_A` | PathDirt · Pebble · GrassEdge | 560 long × 150 wide × 8 (X-forward) | ground-center bottom |
| `SM_LM_Memory_HomeCairn_A` | Stone · Moss · Ribbon · Glow (emissive) | 156×156 foot, ~150 tall | ground-center bottom |

- **Terrain chunk:** stone side wall + dirt band + overhanging mossy grass cap, perlin-broken outline,
  scattered stones, grass tufts, tiny flowers. Modular but visually natural (not a perfect tile).
- **Path:** dirt slab with two worn ruts, embedded pebbles, two grass-edge strips, edge weeds.
  Edge detail is kept off the ±X ends so straight segments connect cleanly to future curve/fork/bridge
  pieces. Forward axis = **+X**.
- **Home cairn:** moss/flower ground base + five stacked rounded stones (noise = hand-placed
  imperfection) + a torus cloth/ribbon wrap + a warm amber **emissive** glow core and apex finial
  (no light component — atlas perf). Reads distinct from a generic rock pile.

Future variants (death/leader/forgotten cairn, road curves/forks, water/coast chunks) are intentionally
NOT built — this pass uses three words from the reference dictionary.

## Materials

One painterly master `M_LMV5` (no textures, no AI): `Noise`-driven `LinearInterpolate(ColorA, ColorB)`
→ BaseColor for mottled, mossy variation; `Roughness` scalar; `EmissiveColor × EmissiveStrength` for
the cairn glow. Thirteen `MI_LM_*` instances drive the zones (e.g. `MI_LM_Terrain_GrassMoss_A`,
`MI_LM_Path_Dirt_A`, `MI_LM_Memory_Glow_A`) plus `MI_LM_Ground_Calm_A` for the proof-scene ground.
Material slots are baked onto each asset (best-effort) and assigned per-slot on the placed components.

## Proof scene + cameras

`/Game/LivingMyth/Maps/GeneratedAtlasV5_ProofScene` (separate from V2/V3/V4): a calmer ground plane,
one of each asset, the V4 no-bake lighting recipe (warm movable key + SkyAtmosphere + cool movable
SkyLight + height fog + locked-exposure post). Three review cameras:
`CAM_V5_Atlas` (elevated atlas read), `CAM_V5_Region` (painterly mid), `CAM_V5_Inspect` (modeling/material).

## Commands

```bash
# build assets + scene (headless, no viewport needed)
UnrealEditor-Cmd <uproject> -run=pythonscript -script=tools/ue_v5_assets.py -unattended -nosplash -nopause
# capture the three cameras (GUI editor; settles exposure, sequences cameras)
UnrealEditor <uproject> -ExecCmds="py <space-free copy of tools/ue_v5_capture.py>" -nosplash -unattended
```
Captures → `Saved/Screenshots/V5_atlas.png` · `V5_region.png` · `V5_inspect.png` (gitignored).
**Gotcha:** the GUI capture launch needs `-unattended` — after a force-kill, the editor otherwise blocks
on a disaster-recovery modal at boot. Capture also waits ~150 frames per camera so auto-exposure /
movable-skylight realtime-capture converge (else the first frame is near-black) — same lesson as the
GeneratedAtlasV4 capture fix.

## Honest verdict

- **Atlas distance (the gate):** PASS. The green chunk separates from the calmer ground; the path reads
  with direction; the cairn reads by silhouette + shadow. Clearly closer to the North Star than V4's
  primitive markers, and still compatible with an editable/terraformable map (pivots + modular pieces).
- **Region distance:** holds the painterly diorama look (mottled ground, raised grassy platform, ruts).
- **Inspect:** material zones + noise-broken forms read; honest stylized semi-realistic, not photoreal,
  not toy-plastic, not flat icons.
- **What still fails / compromises:** (1) the cairn's warm glow is washed out under the bright daylight
  exposure — it lives in the emissive material but doesn't pop at midday; a dusk grade or a real small
  light would sell it. (2) Surface "painterness" is procedural noise only (no hand-painted texture);
  the 7→8+ leap is texturing/sculpt, not plumbing. (3) A single ~4 m asset is inherently a small mark
  at true satellite altitude — atlas readability assumes pieces tile into a region (future placement).
- **Honesty:** presentation-only. Reads/writes no sim state, invents no place/person/event, no
  third-party packs, no runtime generative AI, V2/V3/V4 byte-untouched. Future placement must be driven
  by existing sim truth (terrainType / region cell / controlling faction / site truth / event RegionId /
  home `HomeRegionId`) — out of scope here.

## Files

- `tools/ue_v5_assets.py` — asset + material + proof-scene builder (deterministic, idempotent).
- `tools/ue_v5_capture.py` — editor-side three-camera settle capture.
- `Content/LivingMyth/V5/SM_LM_*` — the three StaticMesh assets.
- `Content/LivingMyth/Materials/{M_LMV5, MI_LM_*}` — master + zone instances.
- `Content/LivingMyth/Maps/GeneratedAtlasV5_ProofScene.umap` — proof scene.
- `LM_V5_*_ref01.png` — the GPT art-direction reference sheets (design refs, not shipped assets).
- `Saved/v5_assets_verdict.json` — machine-readable build verdict (gitignored).
