# Roadmap

Phases advance only through their human review gate (see AGENTS.md). Each phase brief below
is the verbatim scope contract for the LLM agent.

---

## Milestone 0 - Repo and automation ✅ (this phase)

Goal: make the agent loop functional.
- Unity project opens; Blender script runs headlessly; one asset exported as GLB;
  Unity imports generated GLB; Unity command-line tests run; screenshot capture works;
  CI or local script runs full validation.
- **Exit criterion**: `./tools/check_all.sh` runs without manual editor work.

## Prototype 0.1 - Playable Night (current build target)

Question answered: *Is it fun and tense to manage distance from light while directly
controlling the Bellkeeper?*

- Locked orthographic isometric camera.
- Small test map: beach, abbey hill, forest edge, camp area.
- Campfire creates a circular safe light zone; DarknessEvaluator classifies Safe/Edge/Dark.
- Bellkeeper: move, ring bell, carry flame, rescue one villager.
- Villagers work during day, attempt to return to light at dusk; one starts too far out.
- One monster spawns outside light at night and avoids strong light.
- Black Hound placeholder in bell tower: chained, wary, fed, following; player can feed it;
  if fed before night, hound responds to bell and helps against the monster.
- Debug overlay: light zones, villager states, hound state, time of day.
- PlayMode tests: light classification, dusk recall, hound response to feeding.
- Placeholder geometry only.

Blender proof assets: campfire, shipwreck crate (then hull, storage pile, shelter, lantern
post, ruined abbey wall, bell tower ruin, hound chain, hound placeholder).

Visual proof: two screenshots - day camp beside ruined abbey with wreck behind; same scene
at night, firelight only, hound silhouette near the tower.

## Phase 2 - The First White Night (20–30 min vertical slice)

Question: *Does this feel like a living settlement game with emotional stakes?*

1. Shipwreck salvage economy (wreck depletes visibly).
2. Villager jobs: Salvager, Builder, Woodcutter, Tender, Guard (+ Injured).
3. Resources: wood, food, oil, candles, stone, scrap iron, relic fragments.
4. Buildings: campfire, shelter, storage pile, woodcutter hut, lantern post, guard post,
   abbey gate repair, bell tower repair, candle shrine, asylum corner.
5. Hound bond: trust/hunger/pain/fear/attachment; behaviours chained→angry.
6. Nightmare director: pale hound, drowned sailor, lantern moth.
7. Morning consequence report (storybook tone).
8. Win: survive First White Night with ≥6 villagers, Bellkeeper alive, abbey fire lit.
   Loss: Bellkeeper dies / abbey fire out / all villagers dead or fled. Soft-failure
   variance makes replays interesting.
9. Debug overlay for resources, villager states, hound, light safety, director state.
10. Tests: resource accounting, dusk recall, light safety, hound transitions, night
    completion, win/loss. Visual gate: day_camp / dusk_recall / night_attack /
    morning_after screenshots: *does this already feel like the game?*

Direction addendum (2026-07-04): future Phase 2 cleanup should bend the existing build
system toward **seed slots**. Buildings hug existing buildings and light, completed buildings
open nearby child slots, and roads emerge from villager traffic instead of player road
placement. The current `infirmary_corner_t1` implementation is legacy naming; the intended
direction is **Asylum Corner**, with sanity recovery replacing medicine/infirmary healing.

Excluded: farming, seasons, laws, multiple beasts, multiple maps, final UI.

## Phase 3 - Seasons, laws, and moral consequence (60–90 min)

Question: *Does the way you survive change the settlement, beast, abbey, and nightmares?*

1. Seasons: Spring (hope) → Summer (growth) → Autumn (warning) → Winter (judgment);
   night length scales.
2. Laws: Food (Equal/Workers First/Beast Share/Fasting), Night labour (No Work After
   Bell/Paid Risk/Forced), Burial (Full Rites/Mass Graves/Use the Dead), Hound
   (Family/Weapon/Chained/Sacred).
3. Moral pressures: Mercy, Fear, Faith, Reason, Hunger.
4. Hound evolution: Guardian, War, Starved, Sacred, Broken.
5. Consequence nightmares: Hunger Wights, Dead Workers, Grave Crawlers, Chain Hounds,
   Faceless Saints.
6. Abbey transformation states (Sanctuary/Fortress/Famine/Cult/Broken).
7. Renewable economy: grain, meat, wool, herbs, tools, coal, candles.
8. Dilemmas: Missing Salvager, Food Thief, Hound Bites a Child.
9. Four chapters on one map: The Wreck, The Meadow, The Long Rain, The First White Night.
10. End summary reflecting actual choices.

Combat & survival co-pillar (direction 2026-07-04, decompose with the rest at phase start):
11. Clustered settlement growth: authored **seed slots** grow into child slots beside
    existing buildings, paths, and lit ground. A compact village compounds safety through
    overlapping windows, lanterns, and abbey light. Overextension creates light debt.
12. Emergent roads: villagers wear **desire paths** through work and recall. The player does
    not build roads directly. Important paths grant movement speed, but they also demand
    lantern coverage and burn more fuel at night.
13. Two-tier night defense: settlers shelter in homes and sleep if danger stays away. If
    monsters reach the door, interior lights flare and settlers shoot from **lit** windows
    at a sanity cost. Warriors + beast operate in the dark; buildable **warrior structures
    with an upgrade tree**; direct-control Bellkeeper fights alongside autonomous defenders.
    Combat resolves on a **light-band gradient** (Safe: monsters debuffed / Edge: even /
    Dark: friendlies debuffed + sanity drain; the beast is exempt everywhere). Nights
    **escalate** to set-piece stands. Anti-turtle: houses are **destructible**. If the outer
    line breaks, monsters raze a home and kill the settlers inside, losing that light node
    and colonists. Every night also has one problem only a dark-capable unit can solve.
14. **Sanity/asylum**: dread → insanity when the light fails and units are caught in the
    dark; **health resets each morning, sanity does not**. An insane unit spends asylum
    cooldown, **misses the next night**, and is released only by day; the beast is immune.
    If no asylum exists, insane settlers recover slowly at home, but they disturb the
    household at night, spreading dread through screaming, crying, sleeplessness, and
    nightmares. Lower sanity reduces daytime work efficiency; above the insanity threshold,
    villagers stop working until recovered.
15. **Ground scars + desire paths**: night scars stamped at dawn from the event log, fading
    across the day (meadow regrows before dusk; winter snow covers them instead). Day
    desire paths persist as infrastructure and fuel pressure.
16. **Spring-ship season win**: survive winter with a three-part manifest (settlers ·
    provisions · hull/rigging incl. sailcloth) → launch and sail; who-sails/who-stays
    dilemma. Carries the campaign into Phase 4 (arriving at a new coast).
Tests: law effects, pressure updates, hound evolution, nightmare triggers, seasonal
transitions, win/loss, summary accuracy, plus: build-slot expansion, desire-path traffic,
lantern fuel debt, lit-window defense wakeups, woken-house sanity loss, sanity-to-work
efficiency, home recovery dread spill, asylum miss-a-night accounting, warrior upgrades,
ground-scar decay, spring-ship manifest win, night-escalation curve.

Excluded: multiple maps, multiple beasts, procedural generation, full campaign,
multiplayer, art polish.

## Phase 4 - Second map, second beast (replayability proof)

Question: *Can the game generate a new moral survival story with a new beast?*

- **Map 2: The Abbey of Antlers** - dense, beautiful, confusing forest (sacred grove,
  orchard, deep forest, stream, charcoal camp, deer paths, stone circle, hidden graves,
  corrupted logging camp).
- **The Stag Beneath the Abbey**: trust/patience/wound/wildness/covenant; indirect bond;
  broken covenant → the Horned Accuser.
- **Forest Debt** map pressure; extractive vs restorative buildings.
- Resources: old wood, green wood, apples, venison, herbs, resin, sacred seeds, charcoal.
- Nightmares of misdirection: Root Walker, Bell Mimic, Antler Wraith, Hollow Deer,
  Charcoal Dead. **True Bell vs False Bell** mechanic.
- Dilemmas: Old Tree, Starving Deer, Lost Woodcutters, Charcoal Camp.
- Campaign carryover: Map 1 outcome grants one Bellkeeper trait (Calming Presence /
  Commanding Voice / Ritual Authority / Hard Lessons). Beasts stay with their abbeys.
- Map 2 must be winnable both exploitatively and by covenant, and losable by village
  death, Bellkeeper death, or broken covenant.

## Beyond (parked - do not build)

Maps 3–5 (Drowned Mare, White Bear, Moth Queen), full campaign narrative, procedural maps,
multiplayer, final art/animation polish.
