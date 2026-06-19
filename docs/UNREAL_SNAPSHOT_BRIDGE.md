# Godot Snapshot Bridge V1 — the Unreal-facing export contract

_Shipped 2026-06-19. Sim read-model + console gate. Baseline-inert (verify held 598/751/809/1065)._

A deterministic, honest JSON export of a real Living Myth world, designed to **drive** the
separate Unreal Engine 5.8 diorama sandbox. The next step after the UE smoke-test map was not
more hand-built Unreal art — it was proving Unreal can render **honest Living Myth sim data**.
This is that data feed.

## 1. What the export does

- Runs a real, deterministic Living Myth simulation (default seed 1, 250 years) and writes a
  single small JSON snapshot of the finished world state.
- The snapshot is produced by `UnrealExport.Build(World, seed)` in
  `src/LivingMyth.Sim/UnrealSnapshot.cs` — a **pure Sim read-model** in the Sites/Replay/
  SurfacePainter family: it draws **zero Rng**, is never read by `Tick`, and is a deterministic
  function of world state. So it **cannot move the verify baseline**, and two exports off the
  same `(seed, year)` are **byte-identical**.
- Every field is real recorded data or a deterministic rule over it. Nothing is invented.

```bash
dotnet run --project src/LivingMyth.Console -- unreal-snapshot --years 250 --out artifacts/unreal_snapshot_seed1_year250.json
# flags: --seed N (default 1), --years N (default 250), --out PATH (default artifacts/…), --cap N
```

The same command is also the **gate** (see §6): it writes the file, then validates it and exits
non-zero on any failure.

## 2. What it does NOT do

- It does **not** modify, read, or know anything about the Unreal project. It is a one-way data
  feed: Living Myth → JSON → Unreal.
- It does **not** run any generative AI, and it fabricates **no** lore, geography, events, or
  people. Missing optional data is `null` plus an `exportWarnings` entry — never a guess.
- It is **not** a renderer migration. The Godot viewer remains the production renderer; this is an
  export contract so a separate UE sandbox can render the same honest data.
- It does **not** expose a person's current location, faction colours, or anything else the sim
  doesn't actually model. Those come back as `null` with a warning.

## 3. Snapshot schema summary

Top-level (`schemaVersion` `"1.0.0"`):

| Field | Meaning |
|---|---|
| `schemaVersion`, `generatedBy`, `seed`, `year` | provenance |
| `worldName` | the island name (`World.Island`), or `null` |
| `counts` | regions / factions / sites / peopleAlive / peopleEver / events / memoryMarkers / chronicleBeats |
| `regions[]` | `id`, `name?`, `terrain`, `x`, `y`, `controllingFactionId?`, `homeMemoryCount`, `trueEventCount`, `suggestedUnrealRole` |
| `factions[]` | `id`, `name`, `color`(null), `symbolicColor`, `seatRegionId?`, `prosperity`, `leaderPersonId?`, `traits[]` |
| `sites[]` | `id`, `regionId`, `name`, `type`, `typeLabel`, `isSeat`, `x`, `y`, `displayRole` |
| `peopleHighlights[]` | bounded: living leaders + prophets. `id`, `name`, `factionId`, `homeRegionId?`, `currentRegionId`(null), `roleTags[]`, `alive`, `birthYear`, `deathYear?`, `age` |
| `memoryMarkers[]` | bounded (≤60) anchored events: `eventId`, `year`, `type`, `regionId?`, `homeRegionId?`, `markerKind`, `label`, `description?`, `involvedFactionIds[]`, `involvedPersonIds[]` |
| `chroniclePath[]` | 3–7 beats: `beatIndex`, `eventId`, `year`, `type`, `regionId?`, `homeRegionId?`, `label`, `causalHint?` |
| `cameraHints` | `preferredMode` (`"atlas"`), `regionFocusId?`, `bounds?` (`minX/minY/maxX/maxY`) |
| `exportWarnings[]` | honest notes about missing/derived optional data |

`x`/`y` are normalized `[0,1]²` map coordinates (same space `Region.X/Y` and `Site.Nx/Ny` live in).

**Deterministic derived hints** (clearly labelled, never claimed as authored lore):
- `region.suggestedUnrealRole` ∈ {`forest`, `highland`, `coast`, `grassland`, `ruin_or_sacred`,
  `settlement`, `unknown`}. Rule: a **held** region → `settlement`; else an unheld region whose
  sites are ≥ half sacred/funerary → `ruin_or_sacred`; else terrain maps directly
  (`plains`→`grassland`); else `unknown`.
- `site.displayRole` ∈ {market, dock, fortification, sacred, ruin, ford, farm, camp} — a marker
  hint derived from `SiteType`.
- `faction.symbolicColor` — a deterministic `#RRGGBB` from an FNV hash of the faction id, for
  Unreal to tint with. `faction.color` stays `null` because the sim authors no colour.
- `marker.markerKind` — see §4.

## 4. Honesty rules: RegionId vs HomeRegionId (load-bearing)

Living Myth keeps two anchor channels strictly apart, and the snapshot preserves them verbatim:

- **`RegionId` = where it actually happened** (a true place anchor). Wars, battles, famines,
  plagues, migrations, territory changes carry it.
- **`HomeRegionId` = where a life is remembered** (the lineage's home-root). Births, deaths, and
  murders carry **only** this — they are memory anchors, **never** a claim about where the event
  physically happened. Life events never carry a `RegionId`.

The exporter never conflates them. Concretely, `markerKind` encodes the channel deterministically:

| markerKind | when |
|---|---|
| `chronicle_beat` | the event is also one of the `chroniclePath` beats |
| `home_memory_cairn` | `HomeRegionId` set **and `RegionId` null** — a remembered home, not a place |
| `faction_pulse` | `RegionId` set, type ∈ {famine, famine_end, boom, plague, plague_end, migration, prejudice} — a land-fortune pulse |
| `true_place_mark` | any other true place anchor (`RegionId` set) |

So a murder surfaces as a `home_memory_cairn` with `regionId: null` (you must **not** render it
as "happened in" that region); a war surfaces as a `true_place_mark` with a real `regionId`.

When source data is missing it is `null` and a line is added to `exportWarnings` (e.g. faction
colour, person current location). The gate (§6) enforces these rules.

## 5. How the Unreal sandbox should consume it

1. Generate the file on the Living Myth side:
   `dotnet run --project src/LivingMyth.Console -- unreal-snapshot --years 250 --out <path>.json`.
   (A committed reference sample lives at
   `docs/UNREAL_SNAPSHOT_BRIDGE/reference_seed1_year250.json`.)
2. Parse it in UE (it is plain UTF-8 JSON). Treat `schemaVersion` as a compatibility gate.
3. Lay out the atlas from `regions` (`x`/`y` + `suggestedUnrealRole` → terrain mesh/biome) and
   `sites` (`x`/`y` + `displayRole` → which marker mesh).
4. Place markers from `memoryMarkers`, **switching on `markerKind`** — render `home_memory_cairn`
   as a remembrance cairn at the **home** region (never as an in-place event), `true_place_mark`
   / `faction_pulse` at the real region/site.
5. Drive an intro fly-through from `chroniclePath` (already ordered by year) and frame the camera
   with `cameraHints` (`regionFocusId` + `bounds`).
6. Honour `null` as honest absence; surface `exportWarnings` in a dev overlay rather than papering
   over them.

## 6. The gate

`unreal-snapshot` is registered in the console (and CI) as a gate. It writes the file, then asserts:
`parses` · `schema-version` present and equal · `regions-present` (and array length round-trips) ·
`no-fabricated-fields` (region ids/terrain/role real, controllers real, sites in real regions) ·
`region-home-distinction` (every marker/beat copies its event's two anchors verbatim; a
`home_memory_cairn` never carries a `regionId`; a birth/death/murder never becomes a place mark) ·
`deterministic` (two builds off the same `(seed, year)` are byte-identical). Exit code is non-zero
on any failure.

## 7. Current limitations

- **Person location is not modeled** → `currentRegionId` is always `null` (warned). Only
  `homeRegionId` (lineage memory) is real.
- **No authored faction colour** → `color` is `null`; `symbolicColor` is a derived render hint.
- **`peopleHighlights` is intentionally tiny** — living leaders + prophets only. There is no
  follow/importance-of-souls signal in a headless export (that lives in the viewer's session).
- **`chroniclePath` ranks purely by existing importance scoring**, so a war-heavy age yields
  war-heavy beats. It is honest, not curated for variety.
- **Seat is the founding seat** (`seatRegionId` recovered from the founding-territory anchor); a
  people that later lost that land still reports it as its origin seat.
- Schema is **v1** and additive-only by intention; consumers should ignore unknown fields.

## Files

- `src/LivingMyth.Sim/UnrealSnapshot.cs` — the read-model + DTOs (pure, zero-Rng, deterministic).
- `src/LivingMyth.Console/Program.cs` — the `unreal-snapshot` command/gate.
- `.github/workflows/ci.yml` — runs the gate in CI.
- `docs/UNREAL_SNAPSHOT_BRIDGE/reference_seed1_year250.json` — committed reference sample.
