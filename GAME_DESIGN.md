# Game Design — The Abbey at World's End

A 3D isometric survival settlement game. Bucolic day / terrifying night. Shipwrecked
settlers restore a ruined abbey and expand zones of firelight. The player directly controls
the **Bellkeeper** and earns a bond with each map's ancient abbey beast.

This document is the durable design reference. Phase sequencing lives in
[docs/ROADMAP.md](docs/ROADMAP.md); the first playable is specified in
[docs/VERTICAL_SLICE_SPEC.md](docs/VERTICAL_SLICE_SPEC.md).

---

## 1. Pillars

1. **Light is territory.** Each map is a puzzle of extending light without overextending.
2. **Dusk is drama.** The recall moment creates stories without scripted narrative.
3. **The beast is the moral memory of the settlement.** Never "level 2 dog".
4. **The monsters are what we have made possible.** Nightmares reflect player behaviour.
5. **The Bellkeeper is a rescuer, signaler, guardian, and beast-bond character** — not a
   generic warrior.

## 2. Light territory

| Light | Role |
|-------|------|
| Campfire | Basic safety zone |
| Abbey flame | Sacred high-safety zone |
| Lantern post | Expands workable area |
| Carried flame | Hero rescue tool |
| Window light | Minor comfort around homes |
| Bell tower light | Map-specific sacred signal |

Positions classify as **Safe / Edge / Dark**. Fires consume fuel. Weak light flickers.
Monsters avoid strong light and test edges.

## 3. Dusk recall

At dusk: villagers check distance to light; far workers abandon tasks; children run home
first; guards move to posts; fearful villagers panic earlier; brave villagers risk finishing
tasks. The bell reduces panic and improves recall speed. At least one villager should be too
far out on the first dusk — the first rescue moment.

## 4. The Bellkeeper

| Ability | Purpose |
|---------|---------|
| Ring Bell | Recall villagers, call beast, stun weak nightmares |
| Carry Flame | Temporary mobile light radius |
| Rally | Calm nearby villagers |
| Strike | Basic combat |
| Rescue | Guide or carry a villager |
| Feed Beast | Build trust or reduce hunger |
| Touch Beast | High-risk calming interaction |

## 5. Beasts

Each beast belongs to its abbey. The Bellkeeper does not collect beasts; they understand
each covenant. Map 1 results grant one carried trait (see §12).

### Map 1 — The Black Hound of the Bell Tower (loyalty, protection)

Values: `trust, hunger, pain, fear, attachment_to_hero`.
States: `chained, wary, fed, following, guarding, hunting, protective, angry, missing,
wounded, trusting`.

Interactions: feed (hunger↓ trust↑), approach slowly (trust↑ if not afraid), bell (calms or
agitates by history), free chain (trust↑ control-risk↑), leave chained (short-term safety,
resentment risk), combat use (attachment↑ on success, pain risk), heal (trust↑ pain↓),
starve (aggression↑ trust↓).

Bond reads through behaviour, never a visible obedience meter:
- **Low trust**: stays in tower, growls at villagers, sometimes ignores bell, eats alone.
- **Medium**: follows Bellkeeper at dusk, guards abbey door, may rescue a nearby villager.
- **High**: comes to the bell, sleeps near campfire, blocks attacks on the Bellkeeper,
  drags injured villagers to light, stands between children and darkness.
- **Damaged**: disappears at night, returns bloodied, over-aggressive, scares villagers,
  may protect the hero but ignore everyone else.

Evolution paths (Phase 3): **Guardian, War Hound, Starved Hound, Sacred Hound, Broken
Hound** — each with distinct behaviour and visual identity.

### Map 2 — The Stag Beneath the Abbey (restraint, debt, cost of expansion)

Values: `trust, patience, wound, wildness, covenant`. The Stag is indirect: follow it,
respect territory, interpret appearances, leave offerings, avoid taboos. It does not obey
the Bellkeeper — it *permits* the Bellkeeper. Central pressure: **Forest Debt** (rises with
old-growth cutting, overhunting, grove intrusion, night burning, forced forest labour;
falls with replanting, shrines, deer protection, tree burials, restraint). Broken covenant
turns it into the **Horned Accuser**.

Future beasts (not designed yet): Drowned Mare (grief/water), White Bear (hunger/winter),
Moth Queen (light/worship).

## 6. Villagers

State machine, not complex AI: `Idle, AssignedToWork, WalkingToTask, Working,
CarryingResource, ReturningToStorage, ReturningToLight, Panicking, Injured, Resting,
Missing, Dead`.

Roles (Phase 2): Salvager, Builder, Woodcutter, Tender, Guard, Injured. Villagers
physically carry things — the Settlers feeling.

## 7. Economy

Phase 2 resources: `wood, food, oil, candles, stone, scrap_iron, medicine, relic_fragment`.

First loop: shipwreck crates → wood/food/oil/medicine · forest edge → wood · campfire
consumes wood → safety · lantern post consumes oil → edge light · shelter → night
protection · abbey gate repair → sacred safe zone. *The village exists because the wreck
gave a temporary head start.*

Phase 3 renewables: grain, meat, wool, tools, herbs, coal (+ faith, fear as pressures).
Map 2: old_wood, green_wood, apples, venison, herbs, resin, sacred_seeds, charcoal —
renewable but over-usable; extractive vs restorative buildings make the settlement layout
a moral map. Never Factorio complexity: the economy always feeds the question *"can we keep
the light alive through winter?"*

## 8. Buildings

Phase 2 (complete list): Campfire, Storage Pile, Shelter, Woodcutter Hut, Lantern Post,
Guard Post, Abbey Gate Repair, Bell Tower Repair, Candle Shrine, Infirmary Corner.

Abbey restoration nodes (Phase 2): Gate (wood+stone → larger safe area, monsters hesitate),
Bell Tower (wood+iron → longer bell reach, clearer hound response, faster recall), Candle
Shrine (candles+relic → fear↓, nightmares weakened, recovery↑), Infirmary Corner
(medicine+wood → heal villagers and hound).

Phase 3 abbey: Kitchen Hall, Infirmary, Crypt, Watch Wall, Scriptorium, Kennel Yard,
Winter Hearth (+ moral visual variants, see ART_BIBLE).

Map 2: Forester Hut, Herbalist Hut, Orchard Plot, Hunter Blind, Grove Shrine, Root Bridge,
Charcoal Kiln, Stag Garden, Forest Watchpost, Abbey Cloister Repair.

## 9. Nightmares

The director decides nights from: darkness near camp, villagers outside light, hound
trust/hunger, unburied dead, accumulated fear, bell tower state. Never just a tower-defence
wave — intimate and scary first.

Phase 2 enemies: **Pale Hound** (avoids strong light, attacks isolated villagers, retreats
from the hound, tests lantern edges), **Drowned Sailor** (slow, frightening, follows sound,
weakened by bell), **Lantern Moth** (extinguishes weak lights, harmless alone, dangerous
because it creates darkness gaps). Three night problems: protect people, use the bell,
maintain the light network.

Phase 3 consequence enemies: Hunger Wights (food cruelty), Dead Workers (forced night
labour), Grave Crawlers (neglected bodies), Chain Hounds (hound abuse), Faceless Saints
(extreme faith).

Map 2 (misdirection, not siege): Root Walker, Bell Mimic (imitates the abbey bell — the
**True Bell vs False Bell** mechanic), Antler Wraith, Hollow Deer, Charcoal Dead. Paths
shift, fog hides lanterns, villagers walk toward false lights.

## 10. Laws and doctrine (Phase 3)

Small trees, real consequences:

- **Food**: Equal Rations · Workers First · Beast Share · Emergency Fasting.
- **Night labour**: No Work After Bell · Paid Night Risk · Forced Night Work.
- **Burial**: Full Rites · Mass Graves · Use the Dead.
- **Hound**: Family · Weapon · Chained · Sacred.

Each law shifts moral pressures and can unlock consequence nightmares.

## 11. Morality model (Phase 3)

No good/evil meter. Five pressures: **Mercy, Fear, Faith, Reason, Hunger** — shifted by
actions (feed injured first → Mercy↑; forced night work → Fear↑; corpse bait → Hunger↑…).
Pressures drive villager behaviour, hound personality, abbey appearance, available laws,
nightmare types, and ending text.

## 12. Campaign carryover (Phase 4)

Each beast stays with its abbey. Map 1's outcome grants one Bellkeeper trait:
Guardian → **Calming Presence** · War Hound → **Commanding Voice** · Sacred → **Ritual
Authority** · Starved/Broken → **Hard Lessons**.

## 13. Scripted dilemmas

Phase 3: The Missing Salvager · The Food Thief · The Hound Bites a Child.
Map 2: The Old Tree · The Starving Deer · The Lost Woodcutters · The Charcoal Camp.
Dilemmas are data-driven state transitions on the shared event log — not cutscene logic.

## 14. Morning consequence report

Dawn report is storybook-toned, not spreadsheet-only: survivors, injured, missing, food,
fuel, extinguished lights, hound state, village mood, abbey status — plus one or two
generated memory lines (*"The hound slept outside the abbey gate. No one dared approach it,
but the children left scraps nearby."*). This is how the game creates emotional memory.

## 15. Data model (canonical spec shapes)

Design data lives in text specs mirrored into ScriptableObjects.

**Building**: id, display_name, footprint (w×d), build_cost, construction_time,
workers_required, light_radius, night_safety_modifier, produces, consumes, prefab, icon,
upgrade_paths.

**Beast**: id, display_name, map_id, temperament, hunger_rate, trust_thresholds,
fear_thresholds, preferred_rewards, hated_actions, night_abilities, day_abilities,
bond_events, nightmare_failure_mode.

**Light source**: id, radius, fuel_type, fuel_per_hour, sacred_strength, storm_resistance,
monster_repulsion, villager_sanity_recovery.

**Nightmare**: id, trigger_condition, spawn_season, spawn_time, target_priority,
light_weakness, fear_effect, counterplay, visual_theme.

## 16. Architectural rule: one event log, many consumers

Moral pressures, hound evolution, nightmare selection, abbey appearance, and the morning
report are all downstream of the same recorded player actions. This is what makes
"the monsters are what we have made possible" testable rather than scripted.
