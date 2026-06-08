# Living Myth Sandbox — Project State

A no-generative-AI 2D god-sim. C# port of a proven Python prototype, rendered in Godot 4.6 (.NET).
Architecture rule: `src/LivingMyth.Sim/` is a standalone class library with ZERO Godot dependency;
`godot/` only renders it. See `.claude/CLAUDE.md` for commands, gotchas, and invariants.

## Milestones
- [x] M0 — sim port + console proof (run/divergence/surface/verify); determinism gate green.
- [x] M1 — viewer: map, time controls (play/pause, 1–8×), live rising feed, click-to-inspect.
- [x] M2 — god hand (curse tool) + catch-up (clickable feed → causal thread, Quick/Full depth).
- [x] Longevity — logistic carrying_capacity (300) + O(living) hot paths; stable ~450 living over 5000 yrs.
- [ ] M3 — marking + the three channels (Yours / Loud / Rising). ← NEXT
- [ ] Later — visual/UX pass; more pressure engines (economy, culture) + echo packs; gossip distortion layer.

## Session log
- [2026-06-07] Session: built M0→M2 + longevity pass (carrying capacity + perf refactor, proven
  identity-preserving). 6 commits pushed to DrewYomantas/living-myth. Set up isolated nested git repo
  (home dir was an accidental repo). Next: M3 Yours channel.

## Next session starts with
**M3 — the "Yours" channel.** Let the player mark a person / bloodline / people to follow, and blend a
YOURS source into the live feed alongside LOUD/RISING.
- The sim already supports it: `Feed.BuildFeed(world, markedPeople, markedFactions, echoes, limit)`
  expands a marked person to their whole bloodline and tags rows YOURS.
- Wire in the viewer (`godot/Main.cs`): add a "Follow" button to the person + faction inspectors that
  records the mark; surface YOURS-tagged rows in the feed (distinct color/tag), and mark followed dots
  on the map (e.g. a ring/outline in MapView).
- Note: the current live feed uses `Scoring.ImportanceFast` + incremental consequence counts and does
  NOT compute the YOURS term — add a marked-set check in `StreamNewHeadlines` (touches a marked person
  or their bloodline / a marked faction → boost + YOURS tag) rather than calling the heavier
  `Feed.BuildFeed` per tick. Keep it O(living).
