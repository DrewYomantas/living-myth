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

## Next session starts with
**Visual/UX pass, then more pressure engines.** The three core loops (watch → mark → trace) are done.
- Visual/UX: the map is deliberate placeholder art (three columns of dots). Consider real island
  geography, faction territory shapes, settlement clustering, and a cleaner feed/inspector skin.
- More pressure engines: a culture system in `World.cs` (alongside religion/war/economy) for richer
  event types — keep all randomness through `Rng` and every result-feeding iteration explicitly
  ordered, or `verify` will break.
- Echo packs: more archetypes in `Echoes.cs` beyond the current 10.
- Gossip distortion layer: a stretch goal — events get retold/mutated as they spread.
