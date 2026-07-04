# GAME.md — The Abbey at World's End: Master Plan

This is the consolidated build plan. Detail lives in the linked docs; this file is the
single place that holds the whole shape of the project.

- Design reference: [GAME_DESIGN.md](GAME_DESIGN.md)
- Art direction: [ART_BIBLE.md](ART_BIBLE.md)
- Agent rules: [AGENTS.md](AGENTS.md)
- Phase contracts: [docs/ROADMAP.md](docs/ROADMAP.md)
- First playable spec: [docs/VERTICAL_SLICE_SPEC.md](docs/VERTICAL_SLICE_SPEC.md)
- Work tracker (source of truth for who builds what): [REQUIREMENTS.yml](REQUIREMENTS.yml)

---

## 1. The game

A 3D isometric survival settlement game. Your ship wrecks on a beautiful green coast.
Survivors climb inland to a ruined abbey on a hill. By day the world is bucolic: salvage
the wreck, build a camp, restore the abbey, expand zones of firelight. At dusk, panic. At
night, horror: monsters test the edge of the light, and the darkness is territory, not
decoration. The player directly controls the **Bellkeeper** — rescuer, signaler, guardian, and
front-line fighter — and earns a bond with each map's ancient abbey beast. On Map 1 that beast is the **Black
Hound of the Bell Tower**: chained, wounded, starving, furious — and the only reason worse
things haven't come.

**The fantasy**: sunny shipwreck settlement by day, panic at dusk, horror at night, a real
emotional bond with the abbey beast — and a settlement you defend, upgrade, and ultimately
**escape by sea** when spring returns.

Pillars (rev. 2026-07-04): light is territory · dusk is drama · the beast is the moral memory
of the settlement · the monsters are what we have made possible · the Bellkeeper is a
directly-controlled hero (rescuer, signaler, guardian, *and* fighter) · **defense is a
co-pillar** — buildable/upgradable warriors + settler window-archers + the beast hold a night
that escalates with the seasons, while the dark taxes sanity so it never becomes a power
fantasy · the day remembers the night in scars on the ground. Detail: GAME_DESIGN.md §§1,
17–20.

## 2. Dual-pipeline architecture

1. **Unity** (Unity 6.5, 6000.5.2f1) is the game/runtime/simulation/editor. Locked orthographic
   isometric camera (pitch 30°, yaw 45°), real 3D assets art-directed as a 2D diorama,
   shader-driven desaturation for night/fear/winter/nightmare.
2. **Blender** (headless Python; `blender` binary or `bpy` module) is the automated asset
   factory: spec (JSON) → generator script → `.glb` + `.blend` + 4 isometric previews
   (day/night/winter/grayscale) + `.meta.json` + validation report.
3. **The LLM works through code, scripts, tests, metadata, and generated previews** — never
   by clicking around. The canonical loop: spec → script → headless run → export → previews
   → validate → Unity import → Unity tests → screenshot → fix → commit.

Hard gates: closed material library (17 shared materials, max 4 per asset), triangle
budgets (props 800 / buildings 2500 / characters 1500), pivot center-bottom, named anchors,
grayscale readability. `./tools/check_all.sh` is the definition of done.

## 3. Build phases

### Milestone 0 — Repo and automation
Agent loop functional: Blender headless export works, Unity project compiles, tests run
from CLI, screenshots capture, `check_all.sh` gates everything.

### Prototype 0.1 — Playable Night
Greybox proof of the core question: *is it fun and tense to manage distance from light
while directly controlling the Bellkeeper?* Iso camera, small map (beach / abbey hill /
forest edge / camp), campfire light zone, Safe/Edge/Dark darkness evaluator, Bellkeeper
(move / ring bell / carry flame / rescue), villager day-work + dusk recall with one
villager too far out, one light-avoiding monster, Black Hound with chained → wary → fed →
following states, feed interaction, fed-hound-answers-bell payoff, debug overlays,
PlayMode tests. Placeholder geometry only.

### Phase 2 — The First White Night (20–30 min vertical slice)
The tech demo becomes a pitchable game: shipwreck salvage economy (the wreck is the first
"mine" and visibly depletes), villager jobs with visible logistics (Salvager, Builder,
Woodcutter, Tender, Guard), 8 resources, 10 buildings, 4 abbey restoration nodes (Gate,
Bell Tower, Candle Shrine, Infirmary Corner), the real hound bond (trust/hunger/pain/fear/
attachment read through behaviour, not a meter), a nightmare director (pale hound, drowned
sailor, lantern moth = protect people / use the bell / maintain the light network),
storybook morning consequence reports, win/loss with a soft-failure spectrum.
Gate question: **does this already feel like the game?**

### Phase 3 — Seasons, laws, and moral consequence (60–90 min)
Survival becomes morally consequential across Spring → Summer → Autumn → Winter: law trees
(Food / Night Labour / Burial / Hound), five moral pressures (Mercy, Fear, Faith, Reason,
Hunger), hound evolution (Guardian / War / Starved / Sacred / Broken), behaviour-generated
nightmares (Hunger Wights, Dead Workers, Grave Crawlers, Chain Hounds, Faceless Saints),
abbey moral transformation (Sanctuary / Fortress / Famine / Cult / Broken), renewable
economy, three scripted dilemmas, endings that reflect actual choices.

Combat & survival axis (direction 2026-07-04): buildable/upgradable **warriors** + settler
**window-archers** (fatigue cost) + the sanity-immune beast; a **sanity → asylum** system
where health resets each morning but insanity carries — an insane unit misses the next
night and is released only by day (insanity is the price of an under-fuelled night);
**ground scars** that write the night into the day via an event-log decal pass; and the
season win — **survive winter, then build the spring ship** and sail for better coasts
(three-part manifest: settlers · provisions · hull/rigging; who-sails dilemma). Nights
**escalate** toward set-piece stands; the White Nights are the climaxes.

### Phase 4 — Second map, second beast (replayability proof)
**Map 2: The Abbey of Antlers** with **the Stag Beneath the Abbey** — restraint, debt, and
the cost of expansion. Forest Debt pressure, extractive vs restorative buildings, indirect
bond (the Stag *permits*, it does not obey), misdirection nights (Bell Mimic → True Bell
vs False Bell mechanic), four map dilemmas, campaign carryover as one Bellkeeper trait
(Calming Presence / Commanding Voice / Ritual Authority / Hard Lessons). Beasts stay with
their abbeys. Gate question: **does the second beast change everything?**

### Parked (do not build)
Maps 3–5 (Drowned Mare, White Bear, Moth Queen), full campaign, procedural maps,
multiplayer, final art polish.

## 4. Architecture rules

- One event log, many consumers: moral pressures, hound evolution, nightmare selection,
  abbey appearance, and morning reports all derive from the same recorded player actions.
- Design constants in data files / ScriptableObjects, never in MonoBehaviours.
- Scenes are generated by editor bootstrap code, never the source of truth for logic.
- Deterministic systems (seeded RNG, fixed tick) so PlayMode tests can simulate a night.
- Debug overlay for every hidden system.
- Placeholders before polish; commit only working milestones.

## 5. What "first two weeks" means

The exact sequence: locked iso camera → Blender campfire → Blender crate → campfire light
radius scene → day/night cycle → villager that panics in darkness → Bellkeeper who carries
flame → dusk recall bell → one light-avoiding monster → hound that responds to food and
bell → **one playable night**. That proves the game. Everything else is expansion.

## 6. Orchestration model

Work is decomposed in [REQUIREMENTS.yml](REQUIREMENTS.yml). An orchestrator session fires
subagents (each in its own git worktree/branch), fires reviewers on their output, merges
approved work into the integration branch, and updates task status in the YAML after every
transition so any new orchestrator session can resume from the file alone. Protocol:
[docs/ORCHESTRATION.md](docs/ORCHESTRATION.md).

## 7. Human review gates

1. Art bible (camera, scale, palette, contrast, hound silhouette)
2. First playable night (day charming? dusk tense? night scary? light = territory? hero necessary?)
3. Beast bond (does the hound feel alive and emotionally meaningful?)
4. Vertical slice (fun for 20 minutes? clear identity? real game, not an AI asset collage?)
