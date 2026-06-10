# Living Myth Sandbox — Project State

A no-generative-AI 2D god-sim. C# port of a proven Python prototype, rendered in Godot 4.6 (.NET).
Architecture rule: `src/LivingMyth.Sim/` is a standalone class library with ZERO Godot dependency;
`godot/` only renders it. See `.claude/CLAUDE.md` for commands, gotchas, and invariants.

## Milestones
- [x] M0 — sim port + console proof (run/divergence/surface/verify); determinism gate green.
- [x] M1 — viewer: map, time controls (play/pause, 1–8×), live rising feed, click-to-inspect.
- [x] M2 — god hand (curse tool) + catch-up (clickable feed → causal thread, Quick/Full depth).
- [x] Longevity — logistic carrying_capacity (300) + O(living) hot paths; stable ~450 living over 5000 yrs.
- [x] M3 — marking + the Yours channel: Follow a bloodline/people, YOURS rows surface in the feed.
- [x] M4 — economy pressure engine: per-faction prosperity → famine/boom/trade events, famine death
      pressure (mirrors curse), prosperity-linked births; "The Long Famine" + "The Golden Age" echoes.
- [x] M7 — culture pressure engine: per-faction value axes (valor/piety/cunning/harmony) → named
      customs (adopt/fade hysteresis + self-reinforcement) → clash (tension) / diffusion (eases
      tension); "The Vanished Way" echo. verify 814/594/525/652.
- [x] M8 — gossip/reputation layer: `Gossip()` reads a bounded per-year chronicle cursor and rolls
      rumor events off notable deeds — shifting `Person.Reputation` (-5..5) and nudging cross-faction
      tension (rumor ids enter grievance memory). Couples to crime discovery, prophet credibility, and
      war. "The Blackened Name" + "The War of Whispers" echoes. verify 884/699/567/706.
- [x] Map readability pass (viewer-only): deterministic place-seed markers (PlaceSeeds.cs, FNV hash
      of seed+region id — never sim Rng), same-faction roads, label tags + de-overlap, territory
      tint reduction, fixed draw-order. verify unchanged 884/699/567/706.
- [x] Region Lens foundation (viewer-only): clicking a region opens a Region Lens inspector (land /
      neighbours / tales anchored here / honest not-modeled notes) instead of silently redirecting
      to the faction panel; RegionActivity read-model indexes Event.RegionId incrementally
      (O(new events), no history scans); inspector cross-links (e:/r:/f: via RichTextLabel meta)
      interconnect person ↔ faction ↔ region ↔ catch-up; gold lens ring on the selected region.
      verify unchanged 884/699/567/706.
- [ ] Later — visual/UX pass (surface culture + gossip in the viewer); echo packs; timeline scrubbing. ← NEXT

## Session log
- [2026-06-07] Session: built M0→M2 + longevity pass (carrying capacity + perf refactor, proven
  identity-preserving). 6 commits pushed to DrewYomantas/living-myth. Set up isolated nested git repo
  (home dir was an accidental repo).
- [2026-06-07] Session: M3 Yours channel. Follow button on the person + faction inspectors; YOURS rows
  gold-tagged + weight-boosted in the feed; followed dots ringed cyan in MapView. Marked-set check is
  inline + O(living) in `StreamNewHeadlines`; the bloodline grows virally at birth (mirrors the curse),
  so no per-tick `Feed.BuildFeed`. Build clean, `verify` green.
- [2026-06-08] Session: Yours-channel surfacing fixes (directed `Bloodline` lineage; YOURS feed-share
  cap). Then M4 economy engine: `Faction.Prosperity` random-walks (mean-revert to 1.0); threshold
  crossings emit `famine`/`boom` events (InFamine/InBoom hysteresis), trade between prospering factions
  eases tension; famine death multiplier mirrors the curse with a cause-chain; births scale 0.7–1.3×
  with prosperity. Two new echoes. Re-baselined: `verify` 720/455/527/677, cap=0 807/523/452/987.
  Population stable ~165–490 over 5000 yrs after softening famine_death_multiplier 1.8→1.4. Determinism
  green. NOT yet committed.

- [2026-06-09] Session: Visual/UX pass — Phase A. Pass 1 (viewer-only): readable 1× (BaseInterval
  0.5→1.2 s/yr), speed ladder 0.25×–16×, dramatic auto-slow on notable ticks, feed-row flash +
  map region pulse (`e166623`). Pass 2: (1, sim) schism rate cut — `schism_chance_per_year`
  0.02→0.006 + new `schism_min_members` 8→14; **re-baselined verify 934/704/292/621** (seeds
  1/18/42/7), prior was 678/363/383/558; schisms 80yr 4/5/4/9→2/0/2/0, ~13–20 per 600yr. (2, sim
  metadata) `Religion.OriginEventId` set at every faith creation; persecution now causes-links to the
  faith's founding so catch-up walks back to the heresy origin — counts unchanged. (3, viewer) echo
  rationing: per-archetype 60yr cooldown + significance bar (≥80) + window cap (2/40yr). (4, viewer)
  MapView camera: cursor-anchored wheel/button zoom + drag pan (clamped), hit-test correct via shared
  P() transform, drama-follow eases toward pulses unless player took manual control (`7adfd67`).
  Both commits pushed. Determinism green. NOT yet feel-tested in the Godot viewer.

- [2026-06-09] Session: M7 culture engine, then M8 gossip/reputation layer. M8 — `Gossip()` runs
  after `Culture()` in the tick, reads only events recorded since last year (`_lastGossipEventCount`
  cursor, no all-history scan), and for the notable few (importance ≥42, chance-gated, per-person 8yr
  cooldown, ≤2/yr) records a `rumor` event cause-linked to the real deed. Reputation shifts ±1
  (clamped -5..5); cross-faction rumors call `AddTension` so the rumor id lands in grievance memory
  and a war it helps cause traces back through the whisper. Couplings: blackened names are caught more
  easily (murder discovery scales with -reputation), respected prophets win one extra early follower.
  Caught a sentinel-overflow bug (`int.MinValue` LastRumorYear made `Year - LastRumorYear` overflow
  negative → cooldown always tripped → zero rumors; fixed with explicit sentinel guard). Re-baselined
  verify 884/699/567/706, cap=0 1145/1097/535/893. Determinism green; Godot builds.

- [2026-06-10] Session: two viewer passes. (1) Map readability + place seeds (`fa6fc88`): PlaceSeeds
  viewer hints, roads, label tags, layering. (2) Region Lens foundation: RegionActivity read-model
  over Event.RegionId, Region Lens inspector with honest "not yet modeled" copy, e:/r:/f: inspector
  cross-links, "Inspect <faction>" hand-off button, gold selected-region ring. No sim files touched
  either pass; verify green at 884/699/567/706 throughout. Feel-checked via window captures.

## Region Lens — data contracts still missing (design notes, not promises)
The viewer-side lens is honest about these; each needs a deliberate sim-side milestone because all
of them move the verify baseline (new RNG draws and/or new ordered iteration):
- **Event anchoring coverage.** Event.RegionId exists but only territory (exact region), culture, and
  rumor events (faction primary region) carry it. Personal events (birth/death/murder/marriage/
  romance/justice/war/peace/famine/boom/trade/prophet/schism/friction/divine) are unplaced. Contract:
  stamp regionId at Record() call sites from data the sim already has — no new RNG needed if the
  region is derived deterministically (e.g. participants' faction primary region), so this *may* be
  baseline-safe; verify must prove it.
- **Person ↔ site anchoring.** People have no home region/site; the atlas scatter (p.Id % regions)
  is presentation only. Contract: a Person.HomeRegionId assigned at birth/migration, deterministic.
- **Settlement/site state.** No sites exist in the sim. Contract per GAME_DESIGN.md slice 3: 3–7
  deterministic sites per region (id, kind, position), then optional Event.SiteId.
- **Terrain geography.** Regions are points (X/Y + radius circles). Contract: deterministic region
  polygons/bands so the atlas can read as landforms instead of circles.

## Next session starts with
**Open the Godot viewer (F5, mono build) and feel-test Phase A passes 1+2, tuning the named consts.**
None of the by-feel viewer work has been seen running yet. Watch a run and judge: is 1× readable and
the dramatic auto-slow the right depth/length (`BaseInterval`, `SlowdownWindow`/`SlowdownFactor` in
`Main.cs`); do echoes now feel like punctuation (`EchoArchetypeCooldown`/`EchoSignificanceBar`/
`EchoWindowCap`); does zoom/pan feel right and does the drama-camera lean in without yanking
(`FollowZoom`, `ManualCamCooldownSecs` in `MapView.cs`). Adjust consts by feel, rebuild, commit.
Then — the remaining Phase A item — **timeline scrubbing + era recap** (explicitly deferred from pass 1).

### Backlog (after Phase A)
**More pressure engines, then richer surfacing.** The three core loops (watch → mark → trace) are done.
- Visual/UX: the map is deliberate placeholder art (three columns of dots). Consider real island
  geography, faction territory shapes, settlement clustering, and a cleaner feed/inspector skin.
- More pressure engines: a culture system in `World.cs` (alongside religion/war/economy) for richer
  event types — keep all randomness through `Rng` and every result-feeding iteration explicitly
  ordered, or `verify` will break.
- Echo packs: more archetypes in `Echoes.cs` beyond the current 10.
- Gossip distortion layer: a stretch goal — events get retold/mutated as they spread.
