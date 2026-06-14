# North Star Diorama Prototype — Pass V1 (2026-06-14)

A sandboxed, toggle-gated **isometric region-diorama** prototype, built to test how close the
viewer can get to the locked North Star — *stylized semi-realistic fantasy diorama, a living
atlas* — and to honestly measure the gap. This is a **prototype**, not production: it proves a
direction and an asset pipeline, it does not ship North Star visuals.

## How to run it
- **Production bridge (the real path):** inspect a region (or a site) in the atlas, then click
  **"⛰ Enter the Diorama"** in the Region Lens. It opens as a **read-only overlay** over the live
  world for that selected region. **Esc** or **"← Back to the Atlas"** closes it — the atlas,
  follows, and save are untouched underneath (it is an overlay, never a scene swap).
- **F3** opens the diorama for the currently selected region (or the most-built held region if
  nothing is selected) — handy dev shortcut.
- **Standalone/dev:** `<godot-mono> --path godot res://DioramaView.tscn` (builds its own seed-7
  world). Self-capture: `LM_DIORAMA_SHOT=<dir> <godot-mono> --path godot res://DioramaView.tscn`.

## Production Bridge V1 (2026-06-14)
The sandboxed F3 prototype became an honest bridge for *any* region of the *live* world:
- **Wired to the live world** — `DioramaView` now takes `SourceWorld` + `SourceRegionId` from
  Main and renders the currently selected region at the live year (souls/tales/holder/harvest all
  read live). No more seed-7-only.
- **Entry/exit** — a "⛰ Enter the Diorama" button in the Region Lens (region & site context);
  `Main.OpenDiorama`/`CloseDiorama` add/free it as a full-rect overlay (no scene swap, save intact).
- **Honest controls** — the fake 7-disc action bar (Inspect/Follow/**Curse/Bless/Prophecy/Plague/
  Terrain**) is **gone**. The real god-hand verbs stay in the atlas inspector where they journal to
  the save; the diorama bar now reads "READ-ONLY CHRONICLE VIEW · ART IN PROGRESS" + a real
  "← Back to the Atlas". No mock tool is presented as real.
- **Fallbacks** — wild/unclaimed regions render with **no banner** and honest "unclaimed country"
  copy; no-sites regions show "an unwritten country"; sparse regions still frame on their centroid.
- **Art-fidelity pass (small):** fuller, sun-kissed, multi-lobe broadleaf canopies (better
  silhouette/texture) and a better-reading keep (slate roof, arrow-slit windows, two-tone stone).

## Evidence files
- `01_atlas.png` — the live atlas (production viewer).
- `02_region_lens.png` — the selected region's lens in the atlas.
- `03_diorama_bridge.png` — **the SAME selected region in the diorama bridge** (real overlay flow,
  live world, captured in-engine).
- `04_diorama_fallback.png` — a wild/unclaimed region in the diorama (honest fallback: no banner).
- `diorama_prototype.png` — standalone showcase (seed-7, year 462, richest region).
- `diorama_assets_contact.png` — the Blender miniatures (with the canopy + keep improvements).

## What it is
- A dedicated Godot view that builds its own deterministic `World` (seed 7, ticked to year 462),
  picks the most-built held region, and renders it as an **isometric diorama landmass on a dark
  sea**: a tilted per-cell ground plane (terrain-coloured, NW raking-light relief from the
  elevation field), with **Blender-rendered diorama miniatures** billboarded and depth-sorted on
  top at **real `Sites` positions**, settlement clearings, exposed water/earth, faction banners,
  and parchment **label callouts**. Wrapped in North Star **parchment/brass/ink chrome** (framed
  Year plate, serif title, inspector card with house chip, the region-anchored "Saga" feed,
  legend, brass action bar) and a warm-grade + grain + vignette post shader.
- 100% **viewer-only / read-model**: it reads regions/sites/chronicle, never writes sim state,
  never saves. The `verify` baseline is unmoved (823/559/910/632).

## The asset pipeline (`tools/art/render_diorama.py`)
Headless Blender 5.1 → Cycles (96 spp, denoise) → shadow-catcher-grounded transparent PNGs →
loaded into Godot at runtime (`Image.LoadFromFile`, no import step). A real step up from the
flat-cone spike: clustered/layered organic foliage, 3-point lighting, bevelled edges, per-object
colour jitter, procedural material mottle, and 3 cluster variants per species so the scatter
never reads as a repeated stamp. Re-render:
`& "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" -b -P tools/art/render_diorama.py -- godot/assets/diorama`

## Files
| File | What it shows |
|---|---|
| `diorama_prototype.png` | The prototype in the real Godot viewer (in-engine self-capture). |
| `diorama_before_after.png` | The production atlas (before) vs the prototype (after), same source. |
| `diorama_assets_contact.png` | Contact sheet of the 13 Blender diorama miniatures (+ tree variants). |
| `01_atlas.png` | The current production atlas viewer (the bar this beats). |

## Honest rating (independent North Star judge, brutal-by-design)
- Production atlas viewer vs North Star: **3/10** (an atmospheric debug/strategy map).
- This prototype vs North Star: **5/10** — judged a clear leap over production and a "legitimate
  prototype proving the diorama direction": a deliberate, framed isometric diorama *place* with
  depth, relief, settlement clearings, water and earth showing through the canopy. **Not** North
  Star yet. (The judge moved it 2→3→4→5 across four iterations as camera, variety, projection,
  and legibility were fixed in turn.)
- **The ceiling (~5/10 without dedicated art):** the miniatures are stylized flat-shaded
  low-poly, not the hand-painted, material-rich, semi-realistic illustration of the North Star
  references. Closing the last gap needs authored/painterly art (textured albedo, hand-finished
  silhouettes), not procedural code over primitives. The camera/projection, chrome, data wiring,
  and pipeline are solved; the **art fidelity of the assets themselves** is the remaining work.

## Sources / licenses
No third-party assets, packs, shaders, or tools were imported or studied. All geometry is
authored procedurally in Blender (`render_diorama.py`); all rendering/layout is original C#/Godot
code; the post shader is original. Only dependency is Blender 5.1 (GPL tool, output is the user's
own work) and Godot's built-in `Image`/shader APIs. No Kenney/CC0/third-party material used. No
runtime generative AI.
