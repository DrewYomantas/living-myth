# Harvest Economy V1 — design

_2026-06-13. Status: built (sim + read-models + gate + viewer copy). Baseline moved deliberately._

## Goal
Make famine and plenty belong to **the land that starved or flourished**, not an abstract
faction number — so a famine carries `Event.RegionId`, famine's-end becomes a real
chapter-closing event, and the chronicle can say *where* a hunger fell. This is the paired half
of the M4 economy and the first baseline-moving sim contract since M8 (Battle Sites was zero-Rng;
this one adds real new draws).

## Core shift
The harvest random-walk moves from `Faction.Prosperity` to a new per-`Region` `Harvest`.
`Faction.Prosperity` becomes a **derived compatibility surface** (the mean of a people's
controlled-region harvests), so every existing consumer — births, culture, trade, famine death
pressure — keeps reading `f.Prosperity`/`f.InFamine`/`f.InBoom` unchanged; only the source moved
to the land.

## Decisions (approved)
- **Region is ground truth.** Each region carries `Harvest` and walks every tick.
- **V1 scope: famine + plenty both land-anchored**, plus `famine_end` and a place-keyed echo.
- **Retire faction `InFamine`/`InBoom`/`FamineEvent` as independent state** → they are derived
  rollups recomputed each tick (worst controlled region starves / any controlled region feasts /
  worst region's onset event). `Faction.LastBoomYear` removed (region owns boom-beat timing).
- **Worst-controlled-region trigger** for faction-wide famine death pressure.

## Constraints (acceptance criteria, from the approval)
1. Every region carries `Harvest`; **only controlled regions** (`ControllingFactionId != null`)
   emit `famine`/`boom`/`famine_end` events. Wilderness harvest walks silently.
2. `Faction.Prosperity` stays as the compatibility surface = controlled-region harvest **mean**.
3. Channel honesty: `famine`/`boom`/`famine_end` use **RegionId, never SiteId**. Famine deaths
   may **cause-link** to the famine event, but death events stay home-memory anchored
   (`HomeRegionId` set, `RegionId == null`). The four anchor channels never mix.
4. Deterministic gates for: harvest derivation, event anchoring, `famine_end`, landless-faction
   neutrality, no-SiteId leakage.
5. Baseline movement documented with exact before/after gate output + changed assumptions in
   `PROJECT_STATE.md`.

## Mechanics
- **Economy() rewrite**: walk each region's `Harvest` (list order == id order, deterministic),
  `Harvest += RandInt(-1,1)*step + revert`. The god-hand protect/doom bias applies to each
  controlled region's walk — additive on the existing draw, **no new divine draw**. On threshold
  crossings, a held region records:
  - `famine` onset (`regionId: r.Id`; keeps the doom/protect divine cause-links),
  - `famine_end` (region-anchored, cause-linked to the onset it answers),
  - `boom` / "plenty continues" beat (region-anchored).
  Then `DeriveProsperity` rolls each people's controlled regions up into `Prosperity`
  (mean) + the famine/boom flags + `FamineEvent`. Trade lifts the trading peoples' lands
  (`BumpHarvest`) and re-derives the two of them at once (no RNG), so end-of-tick `Prosperity`
  equals the current controlled-region mean **exactly** (gate-checkable) and trade compounding is
  preserved from the M4 walk.
- **SiteId stays null** for all three economy types: a famine spans a region, it isn't *at* the
  ford — so `SiteAnchors.Expected` is **not** extended; the harvest + sites gates both prove it.
- **Death pressure**: a people feels famine when its **worst controlled region** starves; the
  death cause-links to that region's `FamineEvent` (which carries RegionId). The death event
  itself stays `HomeRegionId`/`RegionId==null`.

## Read-models
- `StoryGrammar`: `famine-breaks` connector (`famine_end` ← `famine`, Therefore).
- `Scoring`: `famine_end` = 35, `recovery` tag = 10.
- `Echoes`: **The Barren Years** — one land that starved ≥3 times in a single age (≤25-year gaps),
  the first famine echo keyed on `Event.RegionId` (mirrors The Field of Bones, clustered like The
  Long Famine).

## Gate: `harvest`
Proves derivation (Prosperity == mean; rollups correct), landless neutrality, land-anchoring,
no-SiteId-leakage (incl. the convention table agreeing), `famine_end` pairing (answers an earlier
famine in the same region), life-event channel honesty, and double-run determinism of harvest
state. Non-vacuous across the suite.

## Baseline (the deliberate move)
- **Before** (Battle Sites V1, 8 gates): `894/705/574/715` (seeds 1/18/42/7).
- **After** (Harvest Economy V1, 9 gates): `823/559/910/632`. Per-seed delta
  `-71/-146/+336/-83` — moves in **both** directions because per-region draws + faction-mean
  prosperity reshape births/trade/war RNG. Determinism holds (byte-identical double run).
- **Balance** (5000 yr, cap 300): living `168/157/306/150` — all stable, **no extinction**
  (seed 42, the canary, holds 306). No param tuning needed; `carrying_capacity` stays 300.

## Deferred (V1 emits the sim signal; viewer is a follow-up)
- Terrain-typed harvest (highland vs coast volatility).
- Famine land-mood scar on the map (`ClassifyMark` leaves famine unmarked by design).
- Viewer wiring of the `famine_end` chapter-close and The Barren Years echo carding.
