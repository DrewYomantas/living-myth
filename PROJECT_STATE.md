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
- [ ] Later — visual/UX pass; culture pressure engine + echo packs; gossip distortion layer. ← NEXT

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
