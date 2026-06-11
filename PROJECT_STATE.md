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
- [x] Living atlas visual style foundation (viewer-only): `docs/VISUAL_STYLE.md` style bible locks
      the Batch 1 north-star references (honest/aspirational/forbidden reading + staged roadmap);
      foundation slice — parchment map place tags (zoom-gated, always-on for the selected region),
      framed bottom dock groups, warmed atlas palette + shallows rim, medallion feed chips, shared
      gold/ember accents single-sourced in UiTheme. verify unchanged 884/699/567/706.
- [x] Time & story pacing model (docs-first): `docs/TIME_AND_STORY_PACING.md` — the four-clocks
      hybrid time model (World / Drama / Focus / Replay), event weight bands mapped onto the
      existing thresholds, focus-guard + chapter-recap designs (finally defining "era recap"),
      honest follow-subject table (prophecies forbidden until modeled), timeline-scrub
      feasibility notes, staged roadmap + later sim contracts. Viewer slice: pace-tier tooltips
      on the speed ladder + doc-anchor TODO at the Drama Time constants in `Main.cs`.
      verify unchanged 884/699/567/706.
- [x] Focus guard slice (viewer-only, roadmap 2 of docs/TIME_AND_STORY_PACING.md): pause-on-drama
      toggle (off / ★ followed / all) in the Time dock; gold guard card (Resume / How We Got Here)
      on Major+ YOURS events; followed-death "Their Tale Ends" card (fires even below the
      chattiness threshold — a follow is an explicit ask); per-soul "you last saw…" memory on the
      card + person inspector; "⛨ guard watches…" signal on the year card. All wall-clock
      presentation — pausing only gates whether _Process keeps calling Tick().
      verify unchanged 884/699/567/706.
- [x] Focus Guard V2 — followed soul + memorial moment (viewer-only): "Follow this soul" on the
      person inspector — a per-soul set, never expanded into kin, distinct from the bloodline verb;
      a followed soul's death upgrades the guard card to a memorial — dimmed atlas backdrop, larger
      ceremonial gold frame, event-class medallion, centered name/faction/born–died/age, reputation
      + children lines only when real, last six deeds clickable into catch-up, honest "no place
      recorded" when the death carries no RegionId (the map pulses only a real region); card
      language distinguishes soul-follow from bloodline-follow; year-card guard signal counts souls
      separately; warm-gold soul rings on the map (bloodline stays cyan).
      verify unchanged 884/699/567/706.
- [x] Chapter recaps (viewer-only, roadmap 3 in docs/TIME_AND_STORY_PACING.md): chapters of 25
      SHOWN years (or an arc closure — echo carding / followed-soul memorial, whichever first);
      closing queues a recap behind a "❖ Years X–Y — a chapter closed" chip by the year card,
      auto-opened on the next transition into pause, never over a guard card, never auto-pausing
      the stream; card = span + reason, top-3 by matured Importance (BuildReverse one-shot on
      open), Your Threads deltas (births into follows, losses, reputation band shifts, regions
      gained/lost — measured from chapter start or follow, whichever later), echoes carded; every
      line links into catch-up; only the latest unread chapter is kept; the return chip also
      covers the recap. verify unchanged 884/699/567/706.
- [x] Living Diorama V1 — watched souls made visible (viewer-only; Main.cs, MapView.cs): a
      followed soul reads as a marked life in the diorama before death asks the player to mourn.
      Map: "divine bookmark" — gold halo with a minimum findable screen size at fit zoom,
      soft breathing pulse (alive cue), flare when a newly shown saga event names the soul,
      gold-bordered ★-name tag at any zoom (overlap-skip); placement is still the existing
      deterministic scatter, no new precision implied. Clicking the marker opens a living soul
      glimpse — a small non-modal parchment card (never pauses, memorial always outranks it):
      name/faction/leader-or-once-leader, alive·age·born, reputation band, children count,
      "you last saw…" (real shown-event memory), last 3 recorded deeds as catch-up links;
      thread / the record / unfollow buttons. Saga rows whose participants truly include a
      watched soul carry a 4px gold side rule + tooltip note. Alive cues: guard-line tooltip
      names up to 2 watched souls; inspector says "★ you are watching this soul".
      Docs: Visual Storytelling Doctrine ("the sim should be seen before it is read") + per-
      subject visibility audit (✅/◐/✎/⛭/✖) + Place Memory V1 spec (documented, not built) in
      docs/VISUAL_STYLE.md. verify unchanged 884/699/567/706.
- [x] Place Memory V1 — anchored events scar the land (viewer-only; Main.cs, MapView.cs): real
      events that truly carry Event.RegionId leave persistent marks near the region centre —
      standing stone (territory founding), scorch + snapped pole (territory seized in war),
      cairn (abandonment after a people's extinction), violet ribbon (custom born/faded/clash/
      diffusion). Rumors anchor but don't mark by design (gossip is social, not a scar).
      Marks classified O(new events) in the existing stream loop, capped 4/region (oldest
      yields), fixed slot angles, alpha aged by sim-year (full → 0.30 floor over ~250 yrs, no
      RNG/wall clock), drawn beneath place markers + labels. Region Lens gained "Marks upon
      the land": every mark lists its real event as a catch-up link; "unmarked" stated honestly.
      VISUAL_STYLE.md Place Memory section flipped to SHIPPED with the as-built table; deferred
      rows (death cairns, battle sites, famine land mood, omens) wait on the anchoring contract.
      verify unchanged 884/699/567/706.
- [ ] Later — event-anchoring sim contract (broader Event.RegionId coverage: would extend place
      memory to deaths/battles/famine, let memorials and recaps name real places, and let
      war's-peace / famine's-end close chapters); followed regions (roadmap 4 in
      docs/TIME_AND_STORY_PACING.md); mythic glosses / entity links; relationship constellation;
      local site/tableau visuals; memorial tableau upgrade; visual/UX pass (surface culture +
      gossip in the viewer); echo packs; timeline scrubbing. ← NEXT

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

- [2026-06-10] Session: living atlas visual style foundation. Drew delivered the four Batch 1
  north-star concept images (renamed `Visual references/gpt-northstar-*.png`, concept-reference-only
  per that folder's README); wrote `docs/VISUAL_STYLE.md` — the implementation-facing style bible
  (reference interpretation split honest/aspirational/forbidden, component language tied to the
  `Ui.*` API, palette/typography direction, F5 visual check ritual, staged roadmap 1–6, viewer
  audit). Viewer foundation slice (viewer-only, 3 files): `Ui.ParchmentTag`/`Ui.DockBox`/`LensGold` +
  medallion-rim `ChipBox` in UiTheme; MapView — parchment place tags (region name + place-kind hint,
  zoom ≥ CaptionZoom, always-on gold-bordered for the selected region, skip-on-overlap vs faction
  tags), warmed sea/land/coast + shallows rim (derived geometry, zero extra rng draws), fainter
  adjacency web, shared accents now from `Ui.*`; Main — framed dock groups, feed breathing 6→8,
  brighter chip glyphs. Sim untouched; verify green at 884/699/567/706. Next visual milestone:
  Atlas Composition Pass (roadmap 2 in docs/VISUAL_STYLE.md).

- [2026-06-10] Session: time & story pacing model (docs-first milestone). Audited the whole
  time surface — sim clock (one Tick == one year, no sub-year unit), viewer pacing consts,
  importance thresholds, follow/catch-up loops — and measured the core problem: at 1× a full
  human life passes in ~84 wall-clock seconds and a war resolves in ~2.4 s, so the chronicle is
  legible after the fact but not followable as it happens. Wrote `docs/TIME_AND_STORY_PACING.md`:
  the four clocks (World Time sim truth / Drama Time shown pace / Focus Time protected attention /
  Chronicle Replay Time), event weight bands named over the existing thresholds (background <
  chattiness 60 / notable / major ≥ NotableBar 100 / turning point ≥ echo bar 80), focus-guard
  design (pause-on-drama, "you last saw…" memory, followed-death cards), chapter recaps (25 shown
  years or arc closure — "era recap" finally defined), quiet-years acceleration, replay/scrub
  feasibility (scrub is chronicle reconstruction, one audit hole: historical faction membership),
  alternate-path ghosts ruled forbidden until near-misses are modeled. Tiny viewer slice: pace-tier
  tooltips on the speed buttons (linger/watch/unfold/drift/hasten/sweep/ages) + doc-anchor TODO.
  Sim untouched; verify green at 884/699/567/706.

- [2026-06-10] Session: focus guard slice (viewer-only, Main.cs). GuardMode off/★ followed/all
  cycles on a new Time-dock toggle; MaybeArmGuard runs in the stream loop BEFORE the chattiness
  gate (a followed soul's death always cards; otherwise Major+ YOURS, or any Major in "all");
  a death outranks a same-tick recap; the armed trigger is consumed after the tick completes
  (and after the curse tool's immediate stream), pausing via the existing _running gate. Gold-
  bordered guard card: event line with band glyph + region, death cards add the record (born/
  died/age, reputation, children) + last-6-deeds thread (one-shot scan, inspector-click cost
  class), "you last saw…" from a per-marked-person last-YOURS-event dictionary (O(1) updates,
  also shown in the person inspector). Year card grew 14 px for the "⛨ guard watches…" signal
  (inspector panel shifted to match). Kill()/RecordMurder audit confirmed types "death"/"murder"
  with the victim as a participant cover every real death path (executions Kill first; martyr/
  justice are post-death commentary). Sim untouched; verify green at 884/699/567/706.

- [2026-06-11] Session: Focus Guard V2 — followed soul + memorial moment (viewer-only; Main.cs,
  MapView.cs). Drew's playtest verdict on V1: the followed-death card read as a normal info card,
  and only bloodlines were followable. Added `_followedSouls` — an explicit per-soul set consulted
  by IsYours, RememberSeen, the guard death trigger, and the inspector last-seen line, but never
  passed through `Feed.ExpandMarked` and never grown virally at birth (soul ≠ line). Two distinct
  inspector verbs: "Follow this soul" (living people; a dead soul can still be unfollowed) above
  "Follow this bloodline". Memorial path in ShowGuardCard/StyleGuardCard: ink-veil backdrop that
  swallows clicks, 640×500 frame, 3px gold border + gold rule + class-glyph medallion, centered
  [font_size] name with faction/born–died/age, "the world holds its breath" lead; a soul death
  outranks a bloodline death which outranks a recap within one tick. Honesty: no RegionId → gentle
  "the chronicle records no place for this passing" (never an invented place; pulse only a real
  region); "N children carry the line" only from real Children.Count; "once their leader" only from
  EverLeader; bloodline-only deaths say "a tale of a bloodline you follow closes". Future work:
  richer relationship cards, family tree view, portrait/token system, region/home/site anchoring,
  chapter recap cards, handling multiple followed deaths in one tick. Sim untouched; build + Godot
  build clean; verify green 884/699/567/706.
- [2026-06-11] Playtest fix: chasing a link off a guard card (a child, a deed) lost the card with no
  way back. Added a floating "↩ Return to the memorial / tale" chip (top-center) that reopens the
  held card — content survives CloseGuardCard, so reopening is just re-show + restore the dim. The
  chip lives only while the world is still paused from that card; once time resumes the moment has
  passed and it disappears (a later manual pause never resurrects a stale card).
- [2026-06-11] Session: chapter recaps (viewer-only; Main.cs). Chapters count SHOWN years (ticks
  displayed), default 25, closing early on an echo carding or a followed soul's memorial; the sim
  has no chapter state. Closing QUEUES the recap — chip by the year card, card auto-opens on the
  next transition into pause (never over a guard card; the stream is never auto-paused for a
  boundary, which at 16× would interrupt every ~2 s); latest unread chapter only. Card sections:
  Loudest of the Age (top-3 full Importance over the chapter slice, BuildReverse one-shot on card
  open — echo-scan cost class), Your Threads (births into follows, losses with event links,
  reputation BAND shifts for followed souls, region counts for followed peoples — all measured
  from chapter start or the follow, whichever later; "passed the age quietly" when nothing moved;
  section omitted entirely when nothing is followed), Myths That Echoed. The guard-card return
  chip generalizes to "↩ Return to the chapter". Honesty: war's-peace and famine's-end arc
  closures are NOT implemented — peace events carry no faction attribution and famine's end
  records no chronicle event; both filed under the region/sim contract. Verified live: boundary
  chip at Yr 25/50/…, auto-open on pause, link→catch-up→return loop, guard card outranking a
  queued recap, Your Threads with 41 births + 14→10 regions on a followed people. Sim untouched;
  verify green 884/699/567/706.

- [2026-06-11] Session: Living Diorama V1 — watched souls made visible (viewer-only; Main.cs,
  MapView.cs). Drew's lead-dev verdict after recaps: enough cards and text — the player must
  witness a followed soul living in the diorama before the game asks them to mourn. Map presence:
  the soul halo gained a minimum screen radius (findable at fit zoom where dots are 4px), a
  breathing outer ring (sin on a wall-clock accumulator — O(followed) per frame), a flare via
  `MapView.PulseSoul` when a displayed saga row truly names the soul, and a gold-bordered ★-name
  tag drawn in the label layer with overlap-skip; soul hit radius widened to the halo. Clicking a
  watched soul's dot routes to `SoulPicked` → the living soul glimpse: a small parchment card
  positioned near the marker (clamped to the map area), non-modal, never touches `_running`;
  every line is a real field (faction, IsLeader/EverLeader, age/born, reputation band, children
  count, `_lastSeenEvent` memory, last-3 deeds one-shot scan — inspector cost class); closes
  whenever an inspector, guard card, or recap opens (memorial precedence unchanged — guard
  backdrop is also built later, so it draws above). Saga: rows with a watched-soul participant
  get `BorderWidthLeft = 4` gold + tooltip prefix, computed O(participants) only when souls are
  followed. Honesty: nothing new is invented — no homes, routines, or locations; the glimpse
  states absence plainly ("nothing of them has crossed the saga…"). Sim untouched; verify green
  884/699/567/706.

- [2026-06-11] Session: Place Memory V1 — anchored events scar the land (viewer-only; Main.cs,
  MapView.cs). The first persistent-memory slice, built strictly against what's honestly
  anchored: only territory (founding/war/abandonment) and culture events truly carry
  Event.RegionId, so only those mark — `ClassifyMark` in the existing O(new events) stream loop
  feeds `MapView.AddPlaceMark`. Rumors anchor but are excluded by design. Marks: standing stone,
  war scorch + snapped pole, cairn, violet culture ribbon — drawn at layer 5a (beneath place
  markers and labels), fixed slot angles around the region centre, capped 4/region with the
  oldest yielding, alpha aged deterministically by sim-year (no RNG, no wall clock). Region Lens
  "Marks upon the land" lists the real event behind every mark as a catch-up link; unmarked
  places say so plainly. Docs: VISUAL_STYLE.md Place Memory section flipped to SHIPPED with the
  as-built table + deferred rows gated on the anchoring contract; audit lines updated (Scarred
  by events ✖→◐, Memory coverage note). Sim untouched; verify green 884/699/567/706.

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
The feel-test now also covers the focus guard (shipped): follow a young soul, let the guard
card fire, judge the pause/resume rhythm and the death card's weight — none of it has been seen
running. Then the next implementation pass: **chapter recaps** (roadmap 3 in
`docs/TIME_AND_STORY_PACING.md` — the old "era recap" item, now designed), with followed
regions (roadmap 4) behind it; timeline scrubbing per that doc's Replay design (item 3) once
the historical-faction-membership audit hole is closed.

### Backlog (after Phase A)
**More pressure engines, then richer surfacing.** The three core loops (watch → mark → trace) are done.
- Visual/UX: the map is deliberate placeholder art (three columns of dots). Consider real island
  geography, faction territory shapes, settlement clustering, and a cleaner feed/inspector skin.
- More pressure engines: a culture system in `World.cs` (alongside religion/war/economy) for richer
  event types — keep all randomness through `Rng` and every result-feeding iteration explicitly
  ordered, or `verify` will break.
- Echo packs: more archetypes in `Echoes.cs` beyond the current 10.
- Gossip distortion layer: a stretch goal — events get retold/mutated as they spread.
