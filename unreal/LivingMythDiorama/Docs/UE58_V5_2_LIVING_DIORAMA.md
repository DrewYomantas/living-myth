# UE 5.8 — V5.2 Living-Diorama Pass (Living Myth)

_2026-06-20. Pure Python + **Geometry Script**, no C++. A SOURCE-SHAPE pass over the V5.1 island
([UE58_V5_1_INTEGRATION_POLISH.md](UE58_V5_1_INTEGRATION_POLISH.md)). V5.1 proved an earthy land
patch; V5.2 pushes the **form vocabulary** toward the three GPT North-Star reference sheets
(`LM_V5_*_ref01.png`) while staying editable + data-driven. Built as a SEPARATE `/Game/LivingMyth/V5_2/`
path so the V5.1 baseline survives side-by-side. V2/V3/V4, the V5 baseline, the V5.1 proof set, and the
`M_LMV5` master are byte-untouched; nothing here reads or writes sim state._

---

## Reference analysis (the three GPT sheets, art-direction only — NOT imported assets)

These are internal GPT-generated reference sheets. Treated as **art direction only**: analyzed for a
shape/material vocabulary, never copied as textures or imported as game assets.

### 1. `LM_V5_EditableTerrainBrushKit_ref01.png` — terrain brush vocabulary
- **Layered cutaway island sides.** Every tile is a thick chunk whose *side* reads as horizontal
  strata: a mossy grass cap, a thin warm-soil band, a grey stone/cliff mass, and a pale wet
  shore/sand lip where water meets land. The readable layering — not the top texture — is what sells
  "a piece of editable world."
- **Material breakup.** Grass→soil→stone→shore are distinct *zones*, not a gradient: mottled mossy
  green, warm brown earth, cool desaturated grey rock, and lighter sand/wet-stone at the waterline.
  Within a zone there's painterly two-tone mottle (we already get this from `M_LMV5`'s world-noise
  lerp).
- **Plant clutter density.** Tiles carry sparse, *varied* clutter — grass tufts, a few flowers,
  scattered pebbles/rocks, mushrooms, a fallen log, dead twigs. Density is low and clustered, never a
  uniform lawn; clumps read at a glance.
- **Readable macro-shapes at atlas distance.** Silhouettes stay legible zoomed out: irregular
  (non-circular) outline, a clear cap/cliff/shore banding, and rock outcrops that punch the rim. The
  detail supports the macro-shape; it never dissolves into noise.

### 2. `LM_V5_MemoryMarkKit_ref01.png` — memory mark vocabulary
- **Cairn forms.** Stacked weathered stones — balanced spire stacks and squat piles — almost always
  on a **dark mossy/earth base ring** for contrast. The stack silhouette is deliberately
  hand-balanced, not a uniform pyramid.
- **Contained warm glow.** A single warm amber/gold heart seated *inside* the stack or at its base —
  small, contained, sacred. It reads as memory, never as a lamp; the surrounding stone stays cool and
  dark so the glow has somewhere to land.
- **Future motif candidates.** Standing stones with carved runes, hung banners/cloth, skulls,
  candles, offering bowls, trilithons. These extend the *vocabulary* of "a place that is remembered"
  beyond the cairn — each keeps the same cool-stone / warm-contained-glow contract.
- **Keeping marks mythic, not flashlights.** The discipline: cool dark surrounding material + a
  *small contained* emissive + soft bloom, under locked exposure assisted by one tiny local light.
  The glow is a coal, not a flood. Bigger emissive ≠ more sacred; contrast and containment do the
  work.

### 3. `LM_V5_PathRoadBridgeKit_ref01.png` — path/road vocabulary
- **Worn dirt with irregular grass edges.** Paths are trodden earth lumps with *broken, grass-fringed*
  borders — never a clean board with parallel ruts. Grass tufts blend the edge in irregularly; the
  centre is compacted/pebbled.
- **Branching road shapes.** The kit is modular: straights, curves, **Y-forks**, T-junctions, crosses.
  A road *branches* — a fork toward a second destination is part of the language.
- **Cobble/stone path language.** A second, "civilised" register: fitted flagstone/cobble segments
  (same modular straights/curves/forks) with grass creeping between stones — for settled/sacred
  approaches vs wild dirt trails.
- **Bridges/docks as later candidates.** Plank bridges, stone arch bridges, docks, fences, lamp posts,
  signposts, milestone markers. Out of scope for V5.2 (later asset candidates); only tiny *proof*
  props are safe now, and we keep to memory marks + path rather than adding bridge/dock geometry.

---

## What V5.2 builds (the achievable subset)

A separate namespace — `/Game/LivingMyth/V5_2/`, `MI_LM_V5_2_*` (reparented to the **existing**
`M_LMV5`, read-only), and `GeneratedAtlasV5_2_LivingDiorama`. The subset deliberately excludes the
full kits (no bridges, docks, cobble set, banner/skull/candle props) — this is a *proof of language*,
not an asset pack.

| Goal (from the brief) | V5.2 move |
|---|---|
| Improve land/edge **material zones** | `LandPatch_C` gains a 4-strata cutaway side — **grass cap · warm-soil band · cool-stone cliff · pale shore lip** at the waterline (V5.1 had 3, no shore). Band heights tuned so each stratum reads from the side. |
| Add small **clump/decal surface details** | Baked into the cap: **rock clusters** (grouped 2–3 stones), flat **moss-patch decals**, scattered pebbles, grass tufts and flowers — sparse + clustered, not a uniform scatter. |
| Replace **uniform tree cones** with **varied silhouettes** | Three distinct species assets — **broadleaf** (visible trunk + asymmetric 3-ball round crown), **conifer** (trunk + ragged stacked fir spire), **shrub** (low ball cluster) — scattered as a mixed grove with per-instance yaw/scale jitter. No more identical cones. |
| Improve **cairn / memory mark readability** | `HomeCairn_C`: hand-balanced cool-stone stack on a dark moss ring, a *contained* amber heart + apex coal, tamed emissive + bloom + one proof light. **New `StandingStone_B`** memory motif (carved menhir + faint rune glow + offering stones) — proving the mark *vocabulary* extends past the cairn. |
| Improve **dirt path edge breakup** | `Path_DirtBranch_B`: worn-dirt lumps with irregular grass-fringed edges **and a Y-branch** toward the second memory mark (the kit's "roads branch" language). |

Plus `Water_Disc_C` with a pale shore-foam ring where the island meets the sea.

### Honesty
Presentation-only. Reads/writes no sim state; invents no place/person/event. No third-party packs, no
runtime generative AI (the GPT sheets are art *reference*, never imported). The two proof-only point
lights (cairn heart + menhir rune) and the authored composition are review scaffolding — real
placement must be driven by sim truth (terrainType / region cell / site truth / event RegionId / home
`HomeRegionId`), out of scope here. V2/V3/V4 + V5 + V5.1 + `M_LMV5` are byte-untouched.

## Commands
```bash
# build assets + scene (headless, no viewport)
UnrealEditor-Cmd <uproject> -run=pythonscript -script=tools/ue_v5_2_living_diorama.py -unattended -nosplash -nopause
# capture the three cameras (GUI editor; settles exposure ~150 frames/cam; needs -unattended)
UnrealEditor <uproject> -ExecCmds="py <abs path to tools/ue_v5_2_capture.py>" -nosplash -unattended
```
Captures → `Saved/Screenshots/V5_2_atlas.png` · `V5_2_region.png` · `V5_2_inspect.png` (gitignored).

## Files
- `tools/ue_v5_2_living_diorama.py` — asset + material + proof-scene builder (deterministic, idempotent).
- `tools/ue_v5_2_capture.py` — editor-side three-camera settle capture.
- `Content/LivingMyth/V5_2/SM_LM_*_C|_B` — the StaticMesh assets.
- `Content/LivingMyth/Materials/MI_LM_V5_2_*` — zone instances on the reused `M_LMV5`.
- `Content/LivingMyth/Maps/GeneratedAtlasV5_2_LivingDiorama.umap` — proof scene.
- `Saved/v5_2_living_diorama_verdict.json` — machine-readable build verdict (gitignored).

## Honest verdict
_Rendered + judged 2026-06-20 (UE 5.8, headless build + settle-capture; `Saved/Screenshots/V5_2_{atlas,
region,inspect}.png` at 2560×1440). One fix iteration after the first look: cairn stones → rough-hewn
faceted + cooler/darker + tighter pooled proof light; path dirt darkened + lumps widened for contrast;
foam halo toned down + narrowed. **Overall: PASS — a real but MODEST advance over V5.1; the trees + second
memory mark + layered shore are the wins, the cairn form + paths are the unfixed weaknesses (art labor).**
Lead score ~6.5→7/10. An independent leashed read-only verifier was more conservative: **PARTIAL PASS,
V5.2 5.5/10 vs V5.1 5.0/10**, naming the cairn the single weakest element and flagging paths as still
unconvincing vs the reference — both consistent with the "still fails" list below. Both agree V5.2 > V5.1._

| # | Criterion | Verdict |
|---|---|---|
| 1 | Terrain reads as authored living-diorama vs smooth clay | **Partial.** More composed/inhabited; cutaway sides + grove help. The cap *surface* is still smooth low-poly clay — needs hand-painted texture (art labor). |
| 2 | Believable layered cutaway island sides | **Pass.** Grass cap → warm soil/stone band → pale shore lip read on the edge (the new Shore zone). Stone-cliff band doesn't separate strongly from soil. |
| 3 | Paths worn into the land vs pasted on | **Modest pass.** Darkened path dirt now reads as a worn trail from the cairn; the Y-branch is legible but subtle. |
| 4 | Trees/foliage silhouette variety | **Strong pass — the headline win.** V5.1 had *zero* trees (only tufts); V5.2 has tiered conifer firs, rounder broadleaf crowns, low shrubs, clearly distinct in region/inspect. |
| 5 | Cairn/memory mark mythic + contained vs snowman/lighthouse | **Partial.** Glow is *contained* (not a flood — the core ask passes), and the standing-stone menhir reads as a clean second marker. The cairn stones still lean snowman-ish: the base proof light washes the lower (now faceted, cooler) stones pale. Fully fixing the stone form = sculpt/art labor. |
| 6 | Small props improve readability without cluttering the atlas | **Pass.** Grove + two marks + rock clusters add legibility; pale pebbles are slightly noisy up close but fine at atlas distance; foam halo softened. |
| 7 | Clearly beats V5.1 from atlas / region / inspect | **Pass.** Decisive on region + inspect (trees, second mark, composition, layered shore); modest on the near-top-down atlas. |

**What still fails (next pass, all art-labor not plumbing):** (1) smooth low-poly clay cap surface — the
painterly leap is texture; (2) cairn stone *form* still reads snowman-ish under the base light — wants
sculpted angular slabs or a relit, light-free contained heart; (3) the path Y-branch is subtle.
**Honesty:** presentation-only. Reads/writes no sim state; invents no place/person/event; no third-party
packs; no runtime generative AI (the GPT sheets are art reference, never imported). V2/V3/V4 + V5 + V5.1 +
`M_LMV5` confirmed byte-untouched (git: only new `V5_2/`, `MI_LM_V5_2_*`, and the new umap are added).
