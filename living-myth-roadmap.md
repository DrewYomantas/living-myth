# Living Myth Sandbox — Build Roadmap

Sequenced by dependency, not by calendar. The order is "what has to exist before the
next thing is buildable," so it holds regardless of how fast we move.

Rewritten 2026-06-12, after Myth Authorship + Causal Chronicle V1. This supersedes the
early scoping draft: Phase A of that draft shipped (and overshot), culture shipped, and
two truths emerged that re-order everything else — see "The logic of the order."
PROJECT_STATE.md is the detailed state log; this file is direction.

## Where things stand

Committed and pushed through M8 plus four arcs the original plan never scoped: the
Living Atlas visual foundation, the Focus Time arc, the Anchoring arc, and Myth
Authorship V1. The sim is a standalone C# library with a Godot viewer on top,
deterministic and byte-reproducible per seed, guarded by four console gates
(verify / homes / story / canon) that must pass on every change.

Engines running each year: family, government (leadership, succession), crime (murder,
revenge feuds), war, religion (prophets, schisms, conversion, persecution, friction),
economy (prosperity, famine, boom, trade), culture (value axes → named customs →
clash/diffusion), gossip (rumors shift reputation, feed war). Spatial layer: regions
with terrain, adjacency, ownership through war, wilderness on extinction. Memory layer:
every person knows the home of their line; life events carry a home-memory anchor kept
strictly apart from "where it happened"; anchored events scar the map (stones, war
scars, cairns, ribbons).

Player-facing: the ranked saga feed (Yours/Loud/Rising), inspectors for people,
factions, and places (Region Lens), the causal catch-up that now *explains* — proven
connectors (therefore / but / unresolved-until), real year gaps, honest unknowns ("the
chronicle does not record what first stirred her") — 13 myth echoes, four kinds of
follow (soul, bloodline, people, land), the focus guard with memorial cards, chapter
recaps with unresolved threads, and the canon writing desk: the player authors
tellings, chronicler's notes, inscriptions, place legends, and what-the-people-say
into honest gaps, persisted per seed, never sim truth. One god tool exists: the curse.

So the spine works, the chronicle explains itself, and the player can write into it.
Everything below is depth, reach, and the climb toward the era arc.

## The logic of the order

Three things gate everything else now.

First, **a feel-test debt**: the entire pacing/focus/authorship stack has shipped
gate-green but almost none of it has been *watched running*. Tuning before feeling it
is guessing.

Second, **the world's stories have outgrown its places**. The honesty contract (never
render what the sim doesn't model, never invent a location) turned out to be the real
bottleneck: people have no locations, wars have no battlefields, power has no seat,
prosperity has no land. The anchoring audit produced a concrete unlock map of sim
contracts — each one deliberately moves the verify baseline, and each one unlocks
viewer payoffs that are already built and waiting (place-memory mark kinds, arc
closures like war's-peace and famine's-end, the entire "forbidden to render" list).
Places before forces: most of what the old Phase B wanted lands harder once events
can truthfully say *where*.

Third, **the era arc still rides channels that don't exist**. Culture — the old
prerequisite — is in. Movement is not: nothing migrates, and ideas diffuse but never
*fork* into named variants. Those channels come before the arc is buildable.

## Look and feel

The look crystallized: a warm mythic pixel diorama presented as a **living atlas** — a
sacred map-table wrapped in a parchment-and-ink chronicle UI (DESIGN.md +
docs/VISUAL_STYLE.md are binding). Never a SaaS dashboard, never a glossy mobile
god-game. What survives from the WorldBox reference is the *hands*: tactile,
low-friction input, powers in clear categories, the pause-tweak-observe loop, helping
and just-watching both first-class. Shorthand still holds: WorldBox at the fingertips,
deeper than WorldBox in the chronicle.

The sim remains deliberately ahead of the visuals. The doctrine ("the sim should be
seen before it is read") has its first slice — watched souls live in the diorama, real
events scar the land — but the full expressive-agent layer (agents visibly doing what
the feed describes) stays sequenced *after* species and culture content exist, because
that's what gives an agent something to look like. Animating agents before we know
what they are means redoing it.

## Phase 0 — Identity and attention (now)

The first feel-test (2026-06-12) returned its verdict: causality lands, investment is
real, but the player can't hold the cast — names are the only identity handle, people
are introduced at their deaths, and your story shares one channel with the world's
churn. Fixing that beats adding anything new.

- **The Cast** — SHIPPED 2026-06-12 (viewer-only): sigils, the dramatis-personae panel,
  living introductions + mid-life tale-so-far, your-story/world feed channels,
  why-you-care-first person cards, and the followed-land life-event flood fix. Awaiting
  its own feel-test; portraits remain the aspirational upgrade (Batch 2).
- Surface culture and gossip in the viewer — both engines are nearly invisible today.
  Value axes, held customs, the rumor mill as a readable social weather. (medium)
- Timeline scrubbing. The one Phase A leftover — and north-star Batch 2's Chronicle
  Replay screen shows where it's going: the shipped causal grammar is already the data
  layer for the numbered-turning-points path. Close the one audit hole first
  (historical faction membership) per docs/TIME_AND_STORY_PACING.md. (medium)

## Phase 1 — Places: the sim contracts

Deliberate baseline-movers, ranked by unlock value (from the anchoring audit):

- Per-region economy — famine/plenty become land moods; famine's-end can close a
  chapter. (medium)
- Battle sites — wars gain battlefields; battles become events; war scars mark where
  blood was actually shed. (medium)
- Seat-of-power — successions and custom origins anchor to a true seat instead of the
  disclosed seat-proxy convention; treaty sites; peace events gain faction ids so
  war's-peace can close arcs. (medium)
- Settlements/sites — 3–7 deterministic sites per region, then people at sites. The
  gateway contract: everything on the visual forbidden list (populations, buildings,
  people-at-places) graduates only through this. (large)
- In parallel, viewer-side: terrain geometry (region polygons, atlas as landforms) —
  the diorama gateway, deterministic from seed. (medium, viewer-only)

## Phase 2 — Forces and movement (finish the living world)

- Disease and plague. The one missing natural force; structurally a famine-pattern
  clone, cross-couples with religion, economy, succession — and once per-region
  economy exists, a land mood. (small to medium)
- Migration, two flavors. Person-level life-paths (leave for the prospering region,
  return with knowledge, return in failure) and group movement (flee famine, war,
  persecution). Pays off the map, and deliberately completes the home contract: home
  stays immutable heritage, migration adds *where someone is now* — the distinction
  the anchoring arc left open on purpose. Just as important, this is the channel ideas
  will travel for the era arc. (medium to large)
- Attitudes and prejudice. Coarse, cheap per-agent regard toward other groups, O(living)
  lean. Three inputs: culture ethos sets the baseline, parents transmit with a real
  chance the child rejects it, lived events nudge it. Private feelings stay invisible;
  an attitude earns a chronicle event only when it drives a consequential act. Couples
  with the existing reputation layer (public standing) without duplicating it.
  Oppression lives in the world, never in the blood. (medium)

## Phase 3 — The god hand grows

Both playstyles are core; the hand can't sit at one verb while the world deepens.

- Blessing — the curse's mirror, same butterfly discipline (consumes nothing until it
  flips an outcome). (small)
- Prophecy as promise. The big one, and it now has a second parent: the canon layer's
  documented promotion path. A player telling promoted to a *structured* prophecy the
  sim is allowed to answer is Myth Authorship V2 — freeform text stays the third
  ledger; only the structured promise touches mechanics. (medium to large)
- Taboo and bloodline-seeding; placing agents and peoples directly. (small to medium each)
- Live terrain editing moves to the horizon: it needs terrain to be sim-real (routes,
  geometry) before rerouting a river can honestly bend trade and war. (deferred)

## Phase 4 — Identity: species and expressive agents

- Species as an axis. A tight authored cast (three to five) layered on top of culture
  and bloodline, never replacing them. Hard rules unchanged: species is a seed, never
  a destiny; no species innately evil, superior, or servile; subraces are cultures —
  forks of an ethos, not separate biology. (medium)
- The expressive-agent layer. Agents visibly act out what the feed describes — readable
  motion you can lean into, not a swarm. Sequenced here because species + culture +
  sites finally give an agent a look, a style, and a place to be. This is the full
  payoff of the watch playstyle and the diorama doctrine. (large)

## Phase 5 — The era arc (the North Star)

The big bet, unchanged: the world climbs from tribes toward nation, tech and society
advancing together, in the game's own idiom — emergent thresholds, never a tech tree.

- Domestication as a threshold (the prototype). One cheap, one-time fork a people
  crosses when conditions line up. Build first; it validates the pattern before the
  roadmap bets on it. (small to medium)
- Era and tech as emergent thresholds. Advances emerge from conditions (population,
  stability, contact, a rare gifted figure) and change what stories are possible. (large)
- Diffusion *and forking*. Half exists: M7 customs already travel and ease tension.
  What's missing is the fork — a practice adapting to the receiving culture's axes
  into a discrete, *named* variant traceable through its whole descent. Discreteness
  is the discipline: named forks at thresholds, never continuous drift. (large)
- Social and political progression. Tribe → empire → feudalism → nation as era-states
  of the government engine (builds on the seat-of-power contract). (medium to large)
- Graceful balance — the acceptance test, not a task: no runaway civ, no one people in
  a future age while neighbors sit in stone. Earned by diffusion being real. (criterion)

## Horizon and opportunistic tracks

- Map editor + world templates. Still a real product surface, now with a data ally:
  the canon store already scopes future world-template canon. Gated on terrain
  geometry and sites being worth authoring. (large)
- Creator features. Seed picker + shareable seeds (determinism supports it today, and
  canon files already key per-seed); a viewer surface over the existing console
  divergence machinery (two timelines, one curse apart); Workshop-style canon sharing.
- Faith surfaces — a religion panel, which also unlocks the reserved `rel:` canon notes.
- Rename system — display-layer only, schema already reserved; original names immutable.
- Echo packs — more archetypes in Echoes.cs whenever there's room.

## Guardrails that hold the whole way

The originals all survived; several grew teeth (gates and binding contracts):

- Legible causality is the product — now enforced: a connector renders only with
  deterministic evidence (the `story` gate proves it).
- Simulation truth first — now the four-ledger truth model: Recorded Fact / Causal
  Claim / Player Telling / Mechanical Truth. Player text never becomes mechanics
  without an explicit, structured promotion.
- The honesty contract: never render what the sim doesn't model; no fake locations;
  the two anchor channels (where it happened vs where it's remembered) never mix.
- Authored over infinite. Discrete, named, bounded variants — never free mutation.
- Species is a seed, never a destiny. The test is the orphan: a child of any species
  raised elsewhere carries none of its stereotypes.
- Determinism stays sacred: byte-reproducible per seed; all four gates pass on every
  change; the baseline moves only on purpose (sim engines re-baseline deliberately,
  viewer-only work never moves verify). Hot paths stay O(living), never O(history).
- No generative AI anywhere in the stack.

## The immediate next step

Phase 0, the F5 feel-test. Months of shipped pacing, focus, memory, and authorship
work have never been watched at play speed. One sitting of following a soul and a land
through two centuries tells us more about what to build next than any plan — and
everything after it is more rewarding to build once you've actually sat and watched
the world move.
