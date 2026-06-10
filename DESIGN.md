# Living Myth — Official Visual Design Direction

This is the visual source of truth for Living Myth. It should guide Claude Design, Figma, concept-art batches, Godot viewer work, and future art-pipeline decisions.

`GAME_DESIGN.md` defines what the game is. This file defines how the game should look and feel.

## Visual Thesis

Living Myth should look like a **mythic pixel diorama**: an ancient world that begins as a readable living atlas, then deepens into inspectable regions, settlements, shrines, roads, ruins, and remembered places.

The player should feel like they are watching history unfold on a sacred map-table, then zooming down into places where people actually live.

The target is not a cute mobile god-sim island. It is not a generic dark strategy dashboard. It is not a realistic 3D civilization map. It is a warm, readable, pixel-rooted historical diorama with enough place detail to make stories feel remembered.

## Reference Stack

### Primary influence: Metropolis 1998 translated into mythic antiquity

Use Metropolis 1998 as the strongest practical reference for:

- crisp true-2D / isometric readability
- inspectable places
- tiny agents who feel like they have lives
- streets, paths, gardens, parks, and buildings that remain readable at scale
- dense environments that still parse cleanly
- the feeling that you can zoom into a place and understand what is happening there

Do not copy modern city elements. Translate the clarity and inspectability into ancient-world equivalents:

- dirt roads instead of streets
- shrines instead of civic landmarks
- hamlets, villages, halls, forts, camps, and markets instead of modern buildings
- sacred groves, cairns, ruins, farms, fishing docks, and refugee camps instead of parks and urban lots

### Secondary influence: Parkitect

Use Parkitect for:

- diorama composition
- scenic clustering
- pleasant path layouts
- readable silhouettes
- cozy invitation to zoom in and look around
- staged places that feel designed but not sterile

Do not copy theme-park gloss, rollercoaster spectacle, or modern guest-service language. Pull the composition discipline, not the theme.

### Tertiary influence: RimWorld

Use RimWorld for:

- lived-in local density
- visible homes, rooms, work areas, camps, fields, and aftermath
- ecology and settlement sprawl
- local-event drama
- practical readability of small people and place-based stories

Do not copy RimWorld's utilitarian look directly. Living Myth should be more mythic, warmer, and more beautiful.

### Prototype reference: Fable / Claude Design WorldBox-like V1

The Fable/Claude Design V1 is useful for:

- parchment UI shell
- right-side Saga feed
- bottom fate/action bar
- click-to-replay / How It Happened concept
- general prototype layout

But it is **not** the final world-art direction. It is too WorldBox-adjacent: bright, chunky, icon-like, and toy-island leaning. Keep the UI lessons, move the map and world detail away from that look.

## Core Visual Pillars

### 1. The world is a place, not a board

At every zoom level, the world should feel inhabited. Settlements should not be generic icons stamped onto terrain. A village, shrine, camp, fort, or ruin should imply use, memory, and local identity.

### 2. Detail lives at zoom

The player should discover detail by moving closer. Island view should be readable and calm. Region view should reveal roads, hamlets, shrines, farms, camps, and local factions. Local view should show market lanes, homes, gardens, tiny people, rituals, and aftermath.

### 3. Ancient, not generic fantasy

The world should lean mythic, old, handmade, and regional. Avoid fantasy-MMO excess. Prefer timber, thatch, stone, clay, reeds, banners, hearths, cairns, small shrines, fields, boats, footpaths, grave mounds, sacred trees, and ruined masonry.

### 4. Readability before ornament

Beautiful detail is good only if the player can still understand what they are seeing. Roads, settlements, regions, people, event anchors, and UI state must remain readable.

### 5. History leaves marks

The art direction should support scars and memory:

- repaired bridges
- abandoned huts
- old watchtowers
- plague pits
- shrines raised over time
- ruins reclaimed by moss
- refugee camps becoming towns
- burned fields regrowing
- mixed settlement banners
- grave markers from remembered wars

## Palette Direction

Use a warmer, older, less toy-like palette:

- moss green
- dry grass
- clay brown
- stone gray
- faded slate-blue water
- parchment cream
- ochre
- ember red
- muted teal
- old banner blue
- smoke gray
- ritual gold used sparingly

Avoid:

- neon faction colors
- bright mobile-game greens
- pure saturated blues
- overly clean sand bands
- candy-like faction blobs
- giant dotted circles as the dominant map language

Faction colors should read as cloth, paint, banners, embroidery, settlement accents, or subtle territory cues, not huge UI paint spills.

## Zoom-Level Art Language

### Island View: Living Myth Atlas

Purpose: understand the whole world.

Art language:

- readable island shape
- terrain bands and landforms clear but not chunky
- roads/trade paths visible
- settlement clusters as tiny place-dioramas, not house icons
- region names and faction labels restrained
- banners/settlement flags instead of giant territory circles
- major pulses/events visible but not map-dominating

Influence blend: calmer Fable layout + Metropolis clarity + parchment atlas.

### Region View: Mythic Settlement Constellation

Purpose: understand a region as a lived-in network of places.

Art language:

- multiple sites inside a region: villages, hamlets, shrines, forts, fields, ruins, camps
- roads, rivers, paths, bridges, and local terrain matter
- mixed peoples visible through banners, clothing accents, market areas, and settlement layout
- local chronicle can filter to the region
- camera/time should slow enough for the player to follow events naturally

Influence blend: Metropolis 1998 + Parkitect + ancient myth.

### Local / Site Detail View: Lived-In Diorama

Purpose: understand one place as a social and historical subject.

Art language:

- closer inspectable settlement detail
- visible people, homes, market lanes, shrines, fields, workshops, docks, graves, or ritual spaces
- local UI can show person/place/faction details
- event aftermath visible where possible
- not a full city-builder, but rich enough to make stories specific

Influence blend: Metropolis 1998 detail + RimWorld density + mythic warmth.

### Chronicle Replay View

Purpose: show how an event happened across time and place.

Art language:

- glowing but restrained route traces
- event nodes anchored to locations
- ghosted before/after moments
- parchment How We Got Here panel
- playback controls for history
- visual replay should use real chronicle events and cause-links, not invented drama

Influence blend: Living Myth UI + time-lapse map storytelling + sacred-map memory.

## UI Direction

The UI should feel like a field chronicle, sacred ledger, and strategy-god toolset.

Keep:

- parchment cards
- rounded panels with warm borders
- right-side Saga / Chronicle feed
- bottom fate/action dock
- compact event cards
- How It Happened / How We Got Here overlays
- inspect/follow/curse/bless/omen/prophecy style actions

Improve:

- reduce prototype/mobile feel
- make panel hierarchy cleaner
- use fewer giant icons when text matters
- make culture, rumor, reputation, and local place memory visibly distinct
- keep UI from covering the most interesting world detail

Avoid:

- generic dark dashboard UI
- glossy mobile-game buttons
- fantasy MMO frames
- fake AI/chatbot visual language
- excessive neon outlines

## Typography and Text Feel

Text should feel like a readable myth chronicle, not a spreadsheet and not purple-prose fantasy.

Tone:

- short, concrete, eventful
- old-world but not flowery
- cause-linked and place-specific
- people and place names should carry memory

Example UI labels:

- The Saga
- How We Got Here
- Follow this Place
- Region Lens
- Chronicle Replay
- Turning Points
- Full Thread
- Local Chronicle
- The old road remembers

## Place Types Visual List

Future concept art and assets should establish a clear visual language for:

- hamlet
- village
- town
- city / great seat
- sacred grove
- shrine
- temple
- fort / watchtower
- market
- road / crossing
- bridge
- dock / ferry
- farm / field
- workshop / kiln
- cairn / grave mound
- ruin
- refugee camp
- plague pit
- abandoned place
- mixed settlement

Every place type should be recognizable at region scale and more specific at local scale.

## People and Factions

People should be visible as tiny readable agents, but not visually noisy.

At island scale:

- people are mostly implied by settlements and event pulses

At region scale:

- tiny figures appear near settlements, roads, fields, shrines, and camps

At local scale:

- tiny agents should visibly walk, gather, work, trade, worship, flee, mourn, repair, or fight

Factions should be communicated through:

- banners
- clothing accents
- building motifs
- border stones
- shrine symbols
- market presence
- settlement labels

Avoid relying only on large colored blobs.

## Culture, Rumor, and Reputation Visual Language

Culture should appear as local practices and visible customs, not just stats:

- warlike: fortified paths, watch fires, spear racks, red-brown banners
- devout: shrines, offerings, pilgrims, ritual cloth, sacred paths
- scheming: masked gatherings, messenger routes, shadowed halls, rumor marks
- peaceable: shared markets, gardens, repaired roads, blended banners

Rumor should feel like social movement:

- small whisper-route pulses
- subtle dotted trails between people/settlements
- amber or smoky marks in the feed
- never overwhelming

Reputation should feel like public memory:

- admired names glow slightly warmer in UI
- infamous names are ink-stained or shadow-marked
- Blackened Name should feel remembered, not villain-cartoonish

## What Not To Do

Do not let Living Myth become:

- WorldBox with a parchment UI
- a generic 4X map
- a mobile god-game island
- a RimWorld clone
- a city-builder management sim
- a fantasy MMO UI
- a realistic 3D civilization game
- an AI-generated story toy

The art should always support the game's real thesis: authored procedural history, legible causality, and lived-in places.

## Concept Art Batch Plan

The concept image campaign should be made in batches, not random one-offs.

### Batch 1 — Style Lock / North Star

Goal: prove the core visual identity.

Images:

1. Island View — Living Myth Atlas
2. Region View — mythic settlement network
3. Local View — lived-in settlement detail
4. Chronicle Replay — How We Got Here as visual playback

Success criteria:

- feels distinct from WorldBox
- still readable as a game
- UI/world relationship feels right
- Metropolis/Parkitect/RimWorld influences are translated, not copied

### Batch 2 — Region Biomes and Place Identity

Goal: prove the style works across different terrain and cultures.

Images:

1. Riverfolk marsh region
2. Highland crag region
3. Shore / bay region
4. Deep forest / sacred grove region
5. Dry grass / clay valley region
6. Snowline or cold mountain region

Success criteria:

- each region has a specific mood and settlement language
- terrain affects culture and local life
- settlements remain readable

### Batch 3 — Settlement Types

Goal: define the asset/settlement vocabulary.

Images:

1. hamlet cluster
2. village core
3. market town
4. shrine/sacred grove
5. fort/watchtower border post
6. refugee camp becoming permanent
7. ruin / abandoned place
8. plague pit / quarantined outskirts

Success criteria:

- each place type is immediately recognizable
- each can host events and memory
- scale remains consistent

### Batch 4 — UI and Interaction States

Goal: turn the art into implementable viewer specs.

Images:

1. person inspector with reputation
2. faction inspector with customs and faith
3. place inspector with local memory
4. Saga feed row variants
5. How We Got Here quick-beats view
6. full-thread chronicle view
7. local Region Lens time controls
8. event hover / selected event state

Success criteria:

- Claude Code can implement from the handoff
- panels are readable and not over-decorated
- culture/gossip/reputation are visible

### Batch 5 — History and Replay

Goal: prove history can be watched, not just read.

Images:

1. bridge repaired over time
2. shrine founded from prophecy
3. rumor spreads into conflict
4. famine empties a settlement
5. refugees arrive and form a camp
6. old battlefield becomes a grave mound

Success criteria:

- before/after and cause chains are visually clear
- replay overlay is beautiful but restrained
- event locations are legible

### Batch 6 — Era and Time Progression

Goal: show the world changing over centuries.

Images:

1. early tribal settlement
2. growing village era
3. fortified town era
4. mixed-culture town era
5. ruined / abandoned later era
6. sacred site that persists through all eras

Success criteria:

- history visibly accumulates
- the same place can evolve without losing identity
- no generative-AI storytelling required

## Implementation Translation Notes

Claude Design and image generation can be aspirational. Claude Code implementation should start smaller.

First implementable viewer targets:

1. keep the current parchment UI shell but polish spacing and hierarchy
2. reduce WorldBox-like map colors and giant circles
3. add settlement-diorama markers to regions
4. add person/faction/place inspect surfaces for reputation, customs, and local memory
5. add restrained event-path highlights for How We Got Here
6. prototype Region Lens as visual/navigation first, before deep sim coupling

Viewer-only work must not change the sim verify baseline. Sim-layer work must update `CLAUDE.md` baselines when RNG behavior changes.

## One-Sentence Art Direction

Living Myth is a warm mythic pixel diorama: a living atlas that zooms into ancient settlements, where authored procedural history leaves visible marks on people, places, roads, shrines, ruins, and memory.
