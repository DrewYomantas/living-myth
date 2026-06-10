# Living Myth Sandbox — Game Design Notes

A no-generative-AI living myth sandbox / god sim about watching authored procedural histories unfold across peoples, places, families, faiths, customs, rumors, wars, disasters, and remembered causes.

This document captures high-level design direction that should guide future Claude Design, Figma, Claude Code, and implementation passes. It is not a sprint checklist. `PROJECT_STATE.md` remains the current milestone tracker.

## Core Pillars

- **No generative AI for storytelling or content generation.** The game should use authored procedural systems: event templates, tags, conditions, cause-links, memory, and pacing.
- **Legible causality is the product.** A player should be able to ask why something happened and follow the thread through people, places, customs, rumors, faiths, and prior wounds.
- **Authored richness over infinite mush.** Prefer compact systems that combine meaningfully over unlimited text generation.
- **The world should feel lived in at multiple scales.** Island-scale events matter, but so do regions, settlements, families, shrines, roads, ruins, refugee camps, and local memory.
- **Time should serve attention.** The player should be able to watch history at god-scale, then descend into a place or event and naturally slow down enough to follow what is happening.

## Region Lens / Settlement Layer

The game should eventually support a deeper view beneath the current island/region map.

At island scale, the player sees peoples, regions, borders, wars, migrations, faith spread, culture/gossip pulses, and the rising chronicle. When the player clicks a region, they should have an option to enter a deeper **Region Lens** view. If they zoom in far enough, the game may also offer a seamless transition into this view.

Region Lens is not a city-builder. It is a story-resolution layer. Its purpose is to make places feel specific, remembered, and emotionally legible.

A region may contain:

- towns
- villages
- hamlets
- camps
- shrines
- temples
- forts
- markets
- roads or crossings
- ruins
- sacred groves
- plague pits
- refugee camps
- mixed settlements
- abandoned places

Each site can become a home for local memory: murders, rumors, plague outbreaks, migrations, hybrid families, famine, prophets, customs, wars, and old grudges.

### Scale Layers

1. **Island View**
   - Current macro view.
   - Shows regions, peoples, major event pulses, faction state, war/peace, large-scale faith/culture/gossip events, and global feed.

2. **Region View**
   - New middle layer.
   - Shows the selected region as a local constellation of settlements, shrines, roads, ruins, camps, and important sites.
   - Lets the player see who lives where, which peoples are mixed, what faiths/customs dominate, and what local wounds are active.

3. **Site Detail**
   - Inspector or vignette for a single settlement/shrine/fort/etc.
   - Shows population makeup, dominant faith, controlling faction, local customs, notable people, recent rumors, memory events, and current pressure.

### Hybrid Peoples and Local Identity

The Region Lens is the natural home for hybrid peoples.

Instead of hybridization being only an abstract faction/culture mechanic, the player should see it emerge from places:

- a border market where Shorefolk and Wood families have intermarried for generations
- a refugee camp that becomes a village
- a shrine shared by two peoples after a war
- a town ruled by one faction but populated by another
- children of a truce who eventually become named as a new people

A settlement might show population makeup like:

```text
Saltmere Town
Shorefolk: 54%
Wood Tribes: 31%
Highland Clans: 15%
```

Over time, tags or named local identities can emerge:

- Shore-Wood households
- children of the truce
- Greenfen folk
- Ashroad descendants
- Antlered Deep Mother cult
- Highland-Bay descendants

The design rule remains: species/people/culture are **seed, never destiny**. Hybrid identity should emerge from history, proximity, marriage, migration, faith, shared crisis, and memory, not from innate moral categories.

## Dynamic Local Time

Region and local detail modes should not simply pause the world by default. They should shift into a **hybrid dynamic slowed time** mode.

This is not cinematic slow motion. It is attention-aware pacing: time still moves, but at a cadence that feels natural for following an event as it plays out in a place.

At island scale, years can move quickly and the player reads broad historical movement.

At region scale, the viewer should slow enough that the player can follow local cause and effect:

- a rumor spreading from a market
- a feud crossing from one village to another
- refugees arriving at a camp
- a plague beginning in a port town
- a prophet gathering followers near a shrine
- a mixed settlement becoming culturally distinct

At site detail scale, time should become even more intimate when the player is inspecting an active event, but it should still feel like history is moving rather than frozen.

Possible pacing rules:

- Island View uses the existing speed ladder and drama mode.
- Region View applies a local-time multiplier when the selected region has active events.
- Site Detail applies a stronger local-time multiplier while an event chain is playing.
- Player can override time controls at any point.
- Important events can temporarily nudge the view into a readable cadence without taking control away from the player.

The goal is to let the player say: “I want to follow this place for a while,” and have the game meet them there.

## Replay / Chronicle Playback Mode

The current “How We Got Here” popup is valuable because it explains causality. A future official replay mode should go beyond reading the cause chain and **show the chain playing out**.

Instead of only listing events that led to a war, murder, plague, migration, or prophecy, the player should be able to trigger a short replay/timelapse:

- camera moves to the relevant region or site
- event pulses replay in order
- paths/links briefly draw between sites
- people/factions involved highlight as the chain advances
- the feed shows only the replay beats
- the player can scrub, pause, step forward/back, or jump to the full thread

Example: **War of Whispers replay**

1. A killing occurs at a shrine.
2. A rumor spreads through a town.
3. Tension rises between neighboring peoples.
4. A border market erupts in violence.
5. War is declared.

The replay should be compact. It should feel like watching a mythic map-table retell the past, not like a full cinematic cutscene.

### Replay Modes

- **Quick Replay**: 5–15 second timelapse of the most important beats.
- **Full Thread**: slower chronological playback of every cause-linked event.
- **Regional Replay**: shows where the chain happened inside the selected region.
- **Island Replay**: shows broader movement across regions.
- **Split Scale Replay**: starts at island scale, zooms into region/site when the cause chain becomes local.

### Design Principles for Replay

- Replays use existing chronicle events, causes, participants, region ids, and future settlement/site ids.
- Replays should never invent events.
- Replay should be deterministic presentation, not simulation rollback.
- The sim does not need to reverse time. It only needs enough event history and spatial anchors to visually retell what happened.
- Replay mode should become one of the main ways players understand and emotionally attach to the world.

## Implementation Order Idea

Do not jump straight into a giant settlement sim. Build this in slices.

1. **Viewer surfacing pass**
   - Surface culture, gossip, reputation, and cause chains better in the existing island view.

2. **Claude Design / Figma Region Lens mockup**
   - Design the region view, local feed, site inspector, transition language, local-time UI, and replay concept.
   - Must be Godot-implementable using shapes, text, tints, icons, panels, and simple pulses.

3. **Region Lens V1: visual/prototype only**
   - Deterministically generate 3–7 local sites per existing region.
   - Show site nodes in a deeper region view.
   - Allow click-to-enter and zoom-to-enter behavior.
   - Filter the feed to selected region.
   - No major sim coupling yet.

4. **Event anchoring slice**
   - Add optional site/settlement ids to events.
   - Let existing events attach to local sites where appropriate.
   - Show replay paths between event anchors.

5. **Local memory slice**
   - Sites remember important events, rumors, faith changes, deaths, migrations, and conflicts.
   - Site inspector becomes emotionally meaningful.

6. **Hybrid peoples / migration / plague integration**
   - Settlement population makeup becomes useful.
   - Refugees, plague, local faith, mixed families, and emergent hybrid peoples use the region/site layer.

## Design Handoff Rule

For visual/UI work, Claude Design, Figma, or another design tool should define the visual direction first. Claude Code should implement the design handoff, not invent the visual language while coding.

Claude Code implementation passes should preserve sim determinism, run `verify`, build the Godot project, and report whether the verify baseline moved. Viewer-only work should not move the baseline.
