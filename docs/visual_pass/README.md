# Visual North Star Push V1 — evidence

Screenshots proving the atlas moved toward the locked North Star —
**stylized semi-realistic fantasy pixel diorama, a living atlas**. All renders are the real
pipeline: the headless `paint` images and the in-engine captures share one source of truth
(`SurfacePainter`), so a screenshot is byte-faithful to what the viewer draws.

| File | What it shows | How it was made |
|---|---|---|
| `atlas_before.png` | The atlas **before** the pass — flat terrain fills, a single-cell shore, a washed highland. | `paint` with the parity painter (faithful replica of the old MapView render). |
| `atlas_after.png` | The same world (seed 7, year 120) **after** — painted depth-sea + surf line, warm coast, NW-lit relief, contour + inked political borders. | `dotnet run --project src/LivingMyth.Console -- paint --seed 7 --years 120 --out … --scale 4` |
| `atlas_seed1.png` / `atlas_seed18.png` | The treatment generalizing across worlds. | same `paint` command, seeds 1 / 18 |
| `01_atlas.png` | The new surface **in the real Godot viewer** with the full chronicle UI (Saga feed, dock, place tags, banners, shadowed markers). | viewer self-capture: `LM_SHOTS=<dir>` launches, fast-forwards a fresh world, shoots, quits — never touches the player's save. |
| `02_region_lens.png` | The Region Lens (Inspect mode) over the new atlas: heraldic holder-colored header stripe, sites with contact shadows + banners, gold lens ring. | same self-capture run. |

Reproduce the headless renders any time with the `paint` console command; reproduce the
in-engine shots by launching the viewer with the `LM_SHOTS` environment variable set to a
directory.
