# North Star Diorama Prototype — Pass V1 (2026-06-14)

A sandboxed, toggle-gated **isometric region-diorama** prototype, built to test how close the
viewer can get to the locked North Star — *stylized semi-realistic fantasy diorama, a living
atlas* — and to honestly measure the gap. This is a **prototype**, not production: it proves a
direction and an asset pipeline, it does not ship North Star visuals.

## How to run it
- In the viewer: press **F3** (Esc / "← Atlas" returns to the atlas).
- Standalone: `<godot-mono> --path godot res://DioramaView.tscn`
- Self-capture (writes `diorama_prototype.png` here, never touches the save):
  `LM_DIORAMA_SHOT=<dir> <godot-mono> --path godot res://DioramaView.tscn`

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
