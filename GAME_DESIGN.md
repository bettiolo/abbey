# Game Design - The Abbey at World's End

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
5. **The Bellkeeper is a directly-controlled hero** - rescuer, signaler, guardian, beast-
   bond character, *and* front-line fighter. You drive the Bellkeeper directly (Thronefall-
   style); every other defender acts autonomously.
6. **Defense is a co-pillar; the night is a rising cost.** The settlement fields a real,
   buildable, upgradable defense - warriors, settler archers, the beast. Nights escalate
   from intimate dread in summer to set-piece defensive stands in autumn and winter - but
   the dark always taxes sanity, so combat is never a clean power fantasy.
7. **The day remembers the night.** Night movement and battle scar the ground; day labour
   wears paths into it. The day is bucolic to *be* in, yet the earth carries what happened.
8. **The village grows by hugging the light.** New build slots sprout near existing
   buildings, windows, paths, and lit ground. Expansion compounds safety when clustered well,
   but every outward step creates more road to light and more fuel debt to pay.
9. **The island is older than the abbey.** Bells, candles, shrines, and walls sit on older
   covenants: wells, stones, moon rites, storm taboos, burial customs, animal spirits, and
   beast bargains. Use these as local pressures and choices, not as generic spellcasting.

> Pillar note (direction set 2026-07-04): combat was promoted from a thin support role to a
> co-pillar - *Thronefall-meets-horror-settlement*. The horror identity is preserved by the
> sanity cost of the dark (pillar 6), the beast's singular centrality (pillar 3), and
> intimate early nights that escalate only as the seasons turn. Detail in §§17-21.

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

Season, weather, and moon phase modify light itself. This is the Frostpunk-style forecast
pressure for Abbey: the player should see enough of tomorrow's night to dread and prepare for
it.

| Condition | Light impact |
|-----------|--------------|
| Spring | forgiving nights, faster sanity recovery, paths regrow quickly |
| Summer | longer workdays and safer expansion temptation |
| Autumn | fog and rain soften lantern edges, fuel pressure rises |
| Winter | longer nights, slower sanity recovery, snow hides old paths |
| Full moon | more visibility, weaker darkness, some old things grow bolder |
| New moon | lantern radius reduced, monsters approach closer |
| Tempest | flames flicker, lanterns burn extra fuel, bell range suffers |
| White Night | ordinary safe zones shrink, sacred light matters most |

**Settlement growth rule.** Buildings are not scattered freely across the map. Each map
starts with a few seed slots near the campfire, abbey, wreck, or other landmarks. Completing
a building can reveal child slots beside it, so the settlement grows as a tight cluster of
houses and workplaces hugging one another. A slot is valid because it is close to life: near
existing buildings, connected by travelled paths, and inside manageable light. This keeps the
Thronefall clarity of authored choices while adding the Settlers feeling of a village filling
in around work.

**Light debt.** Every new slot is both opportunity and liability. A compact cluster lets
window light, lanterns, shrines, and abbey fire reinforce each other. A long outward branch
may unlock salvage or defense, but it lengthens the path network and creates more lanterns to
fuel. The player should feel expansion as a promise made to the dark: if you grow there, you
must keep it lit.

**The three bands are a combat gradient** (Phase 3 combat, §17) laid over that same
classification - light is not just where you *can* stand, it is **home-field advantage**:

| Band | Friendly units | Monsters | Net |
|------|----------------|----------|-----|
| **Safe** (strong light) | normal | **debuffed** | you favoured |
| **Edge** (twilight) | normal | normal | even fight |
| **Dark** | **debuffed** (+ sanity drain, §18) | normal | they favoured |

Your ground weakens monsters; their ground weakens you; the twilight is a fair fight. This
is the mechanical reason monsters avoid strong light and test the edges. The **beast is never
affected** - it fights at full strength in every band, which makes it your natural striker
for the dark and one more reason it stays singular (§3).

## 3. Dusk recall

At dusk: villagers check distance to light; far workers abandon tasks; children run home
first; guards move to posts; fearful villagers panic earlier; brave villagers risk finishing
tasks. The bell reduces panic and improves recall speed. At least one villager should be too
far out on the first dusk - the first rescue moment.

The dusk read should be visible and ritualized. The Bellkeeper rings the bell; workers pick
up tools, leave half-finished jobs, walk the worn paths home, and enter houses. Windows light
one by one as families settle inside. This is the daily handoff from Settlers-style bustle to
horror: the player sees exactly who made it home, which road stayed dark, and which building
is now a fragile pool of life.

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

### Map 1 - The Black Hound of the Bell Tower (loyalty, protection)

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
Hound** - each with distinct behaviour and visual identity.

### Map 2 - The Stag Beneath the Abbey (restraint, debt, cost of expansion)

Values: `trust, patience, wound, wildness, covenant`. The Stag is indirect: follow it,
respect territory, interpret appearances, leave offerings, avoid taboos. It does not obey
the Bellkeeper - it *permits* the Bellkeeper. Central pressure: **Forest Debt** (rises with
old-growth cutting, overhunting, grove intrusion, night burning, forced forest labour;
falls with replanting, shrines, deer protection, tree burials, restraint). Broken covenant
turns it into the **Horned Accuser**.

Future beasts (not designed yet): Drowned Mare (grief/water), White Bear (hunger/winter),
Moth Queen (light/worship).

### Old rites and local belief

The abbey is a Christian/monastic layer over older island covenants. Pagan references should
feel specific to place: moon phases, sacred wells, boundary stones, storm offerings, animal
omens, harvest taboos, burial customs, salt, hair, carved wood, and food left at thresholds.

Mechanically, start with tags and choices, not a full new meter. Old-rite tags can alter
beast reactions, shrine outcomes, nightmare triggers, villager petitions, and exploration
finds. A few useful examples:

- Moon rites: full moon, new moon, blood moon, and White Night change light and beast rules.
- Offerings: candles, food, salt, blood, relic fragments, carved wood, hair.
- Taboos: do not cut the marked tree, do not leave dead unburied, do not ring during a
  tempest, do not chain the hound under a full moon.
- Covenant sites: wells, stones, groves, cairns, crypt thresholds, shore shrines.

## 6. Villagers

State machine, not complex AI: `Idle, AssignedToWork, WalkingToTask, Working,
CarryingResource, ReturningToStorage, ReturningToLight, Panicking, Injured, Resting,
Missing, Dead`.

Roles (Phase 2): Salvager, Builder, Woodcutter, Tender, Guard, Injured. Villagers
physically carry things - the Settlers feeling.

## 7. Economy

Phase 2 resources: `wood, food, oil, candles, stone, scrap_iron, relic_fragment`.

First loop: shipwreck crates → wood/food/oil/candles · forest edge → wood · campfire
consumes wood → safety · lantern post consumes oil → edge light · shelter → night
protection and window light · abbey gate repair → sacred safe zone. *The village exists
because the wreck gave a temporary head start.*

Do not grow this into a heavy production-chain city builder. The economy exists to make
settlement shape, light coverage, sanity debt, and the morning report matter. Goods should be
visible in the world: salvage is hauled, construction piles grow, lantern routes consume oil,
and households turn stockpiles into safety at night.

Phase 3 renewables: grain, meat, wool, tools, herbs, coal (+ sanctity, old faith, and fear
as pressures).
Map 2: old_wood, green_wood, apples, venison, herbs, resin, sacred_seeds, charcoal -
renewable but over-usable; extractive vs restorative buildings make the settlement layout
a moral map. Never Factorio complexity: the economy always feeds the question *"can we keep
the light alive through winter?"*

## 8. Buildings

Phase 2 direction set: Campfire, Storage Pile, Shelter, Woodcutter Hut, Lantern Post,
Guard Post, Abbey Gate Repair, Bell Tower Repair, Candle Shrine, Asylum Corner.

Abbey restoration nodes (Phase 2): Gate (wood+stone → larger safe area, monsters hesitate),
Bell Tower (wood+iron → longer bell reach, clearer hound response, faster recall), Candle
Shrine (candles+relic → fear↓, nightmares weakened, recovery↑), Asylum Corner
(wood+candles+relic → recover insanity over time, releases only by day).

The old Phase 2 implementation may still contain `infirmary_corner_t1` as a legacy name.
Future design direction treats that slot as the **Asylum**, not a medicine/infirmary system.
Health can recover generously at morning. Sanity is the persistent wound.

**Build slots and upgrades.** Build sites are authored slots, not free placement. Each slot
has allowed building ids, adjacency rules, light requirements, path anchors, and upgrade
branches. Completing or upgrading a building can open nearby child slots. The strategic depth
comes from choosing how a compact settlement compounds: wider window defense, lower fuel
burn, stronger recall, safer path segments, or more production.

Phase 3 abbey: Kitchen Hall, Asylum, Crypt, Watch Wall, Scriptorium, Kennel Yard,
Winter Hearth (+ moral visual variants, see ART_BIBLE).

Map 2: Forester Hut, Herbalist Hut, Orchard Plot, Hunter Blind, Grove Shrine, Root Bridge,
Charcoal Kiln, Stag Garden, Forest Watchpost, Abbey Cloister Repair.

## 9. Nightmares

The director decides nights from: darkness near camp, villagers outside light, hound
trust/hunger, unburied dead, accumulated fear, bell tower state, exposed roads, and
under-lit growth branches. Nights **escalate**: intimate and scary first (summer), rising to
set-piece defensive stands at the seasonal climaxes (autumn/winter, the White Nights). Even
the big nights stay tense rather than triumphant, because the dark taxes sanity (§18) and
every night includes at least one problem that cannot be solved by staying inside lit homes.

Phase 2 enemies: **Pale Hound** (avoids strong light, attacks isolated villagers, retreats
from the hound, tests lantern edges), **Drowned Sailor** (slow, frightening, follows sound,
weakened by bell), **Lantern Moth** (extinguishes weak lights, harmless alone, dangerous
because it creates darkness gaps). Three night problems: protect people, use the bell,
maintain the light network.

Forecasted conditions are part of the director: moon phase, weather, moth signs, hound
restlessness, storm warnings, and White Night omens. They should make preparation legible
without making the night solved.

Phase 3 consequence enemies: Hunger Wights (food cruelty), Dead Workers (forced night
labour), Grave Crawlers (neglected bodies), Chain Hounds (hound abuse), Faceless Saints
(extreme faith).

### Threat sources and exploitation pressure

Monsters do not come from a generic spawn pool. They come from places the settlement has
entered, damaged, drained, mined, felled, desecrated, or overused. At night, the forest, old
wells, caves, mountains, shore wrecks, crypts, boundary stones, and abandoned roads can each
become a source.

Each source tracks **exploitation pressure**. Exploitation is often necessary, but it wakes
what sleeps there:

| Source | Common exploitation | Awakened pressure |
|--------|---------------------|-------------------|
| Forest | woodcutting, hunting, forced night logging, charcoal | root walkers, hollow deer, pale hounds on animal paths |
| Well | heavy water draw, corpse washing, broken offerings | drowned voices, well-crawlers, sickness omens |
| Cave | mining, hiding stores, sheltering fugitives | blind things, echoes, lost-worker nightmares |
| Mountain | quarrying, iron digging, shrine breaking | stone saints, avalanche omens, bell-muting winds |
| Shore / wreck | salvage, stripping bodies, failed rescues | drowned sailors, kelp things, tide ghosts |
| Crypt / grave | rushed burial, mass graves, relic theft | grave crawlers, faceless saints, dead workers |

This makes the map itself remember exploitation. The player can still cut wood, draw water,
mine, salvage, and quarry, but overuse changes where monsters come from and what they want.
Restraint, offerings, repairs, light, proper burial, and old-rite bargains can slow pressure
or redirect it. Map 2's Forest Debt is the first focused version of this larger source
pressure rule.

Map 2 (misdirection, not siege): Root Walker, Bell Mimic (imitates the abbey bell - the
**True Bell vs False Bell** mechanic), Antler Wraith, Hollow Deer, Charcoal Dead. Paths
shift, fog hides lanterns, villagers walk toward false lights.

### Emergency overdrive actions

Overdrive is a family of desperate actions, not one button. Each action solves tonight and
creates tomorrow's cost.

| Action | Immediate use | Cost / risk |
|--------|---------------|-------------|
| Forced Night Work | finish a building, cut trees, repair a road light after bell | sanity loss, Bellkeeper trust loss, consequence nightmares |
| Candle Line | villagers carry candles and mark a temporary road or worksite | burns candles fast, attracts moths, collapses if wind rises |
| Lantern Overburn | stronger lantern radius or monster repulsion for one night | extra oil burn, hard blowout if fuel runs dry |
| Bell Toll | stronger recall, panic reset, weak nightmare stun | overuse reduces trust, draws sound-following nightmares, unsettles the hound |
| Abbey Rite | spend candles/relics to strengthen sacred light | increases dependence on the abbey covenant, may conflict with old rites |
| Hound Hunt | send the hound beyond light to solve one dark problem | pain, hunger, fear, attachment risk |
| Volunteer Watch | a household, guard post, or warrior group guards a known route | sanity loss, lower work output next day |

Candle Line is the key night-work visual: workers leave home with candle bundles, plant small
light islands along a road or around a tree stand, work inside those shrinking pools, then
try to return before the candles fail.

## 10. Laws and doctrine (Phase 3)

Small trees, real consequences. Park the full law design for a dedicated pass, but keep the
rule: laws are crisis tools, not abstract menu bonuses. Every law should solve a real problem
and plant a later problem.

- **Food**: Equal Rations · Workers First · Beast Share · Emergency Fasting.
- **Night labour**: No Work After Bell · Paid Night Risk · Forced Night Work.
- **Burial**: Full Rites · Mass Graves · Use the Dead.
- **Hound**: Family · Weapon · Chained · Sacred.
- **Old rites**: Tolerate Offerings · Bless the Boundary Stones · Forbid Pagan Rites · Bind
  Abbey and Old Well.

Each law shifts moral pressures and can unlock consequence nightmares.

## 11. Morality model (Phase 3)

No good/evil meter. Track concrete social and spiritual states:

- **Trust in Bellkeeper**: do people follow the person who was supposed to guide the ship and
  instead brought them to wreck? Affects recall obedience, law acceptance, volunteers,
  household petitions, expedition crews, and whether people trust the Bellkeeper to sail in
  spring.
- **Sanctity of Abbey**: does the place still protect them? Affects sacred light, fear
  recovery near abbey ground, shrine/asylum effectiveness, and nightmare resistance.
- **Village mood**: the settlement's general temperature: hopeful, strained, fearful,
  mutinous.
- **Household sanity** and **individual sanity**: who works well, who panics, who must stop
  working, and who spills dread into a home.
- **Beast status**: trust, fear, hunger, pain, attachment.
- **Moral pressures**: Mercy, Fear, Reason, Hunger, and Old Faith pressure. These are not
  score meters; they are event tags that bias laws, petitions, nightmares, and endings.

Trust in Bellkeeper and Sanctity of Abbey are deliberately separate. The first is leadership:
"Will we follow this person?" The second is spatial and covenantal: "Does this place still
hold back the dark?"

## 12. Campaign carryover (Phase 4)

Each beast stays with its abbey. Map 1's outcome grants one Bellkeeper trait:
Guardian → **Calming Presence** · War Hound → **Commanding Voice** · Sacred → **Ritual
Authority** · Starved/Broken → **Hard Lessons**.

## 13. Scripted dilemmas

Phase 3: The Missing Salvager · The Food Thief · The Hound Bites a Child.
Map 2: The Old Tree · The Starving Deer · The Lost Woodcutters · The Charcoal Camp.
Dilemmas are data-driven state transitions on the shared event log - not cutscene logic.

## 14. Morning consequence report

Dawn report is storybook-toned, not spreadsheet-only: survivors, injured, missing, insane,
sleep-starved households, food, fuel, extinguished lights, exposed roads, hound state,
village mood, Bellkeeper trust, abbey sanctity, and one or two generated memory lines (*"The
hound slept outside the abbey gate. No one dared approach it, but the children left scraps
nearby."*). This is how the game creates emotional memory.

The morning report is also the replay hook. It should leave the player with one clear
question for the next day: which road needs light, which slot is too exposed, who is in the
asylum, what promise did the Bellkeeper keep or break, what did the hound remember, and what
did last night prove about the settlement's shape?

## 15. Data model (canonical spec shapes)

Design data lives in text specs mirrored into ScriptableObjects.

**Building**: id, display_name, footprint (w×d), build_cost, construction_time,
workers_required, light_radius, night_safety_modifier, produces, consumes, prefab, icon,
upgrade_paths.

**Build slot**: id, map_id, position, parent_slot_id, allowed_building_ids,
light_requirement, path_anchor, child_slot_ids, unlock_condition, exposed_night_weight.

**Household defense**: building_id, resident_capacity, window_light_radius,
night_wake_threshold, ranged_support_profile, sanity_loss_per_wake,
sanity_loss_per_attack, nightmare_spill_radius, destruction_consequence.

**Sanity**: villager_id, current, work_efficiency_curve, stop_work_threshold,
insanity_threshold, home_recovery_per_day, asylum_recovery_per_day,
nightmare_spill_per_night.

**Bellkeeper trust**: current, recall_obedience_curve, law_acceptance_modifier,
volunteer_modifier, spring_sail_threshold, recent_promises.

**Abbey sanctity**: current, sacred_light_modifier, fear_recovery_modifier,
asylum_modifier, old_rite_conflicts, desecration_events.

**Desire path**: id, points, traffic_score, width, movement_bonus, lantern_need,
fuel_debt_weight, visible_state.

**Island exploration**: node_id, discovered, route_light_requirement, expedition_risk,
possible_survivors, possible_warriors, resources, old_rite_tags, unlocks_seed_slots.

**Shipwreck event**: event_id, coast_node_id, storm_condition, survivors, possible_warriors,
specialists, salvage_resources, rescue_deadline, nightmare_tags, trust_outcome.

**Threat source**: source_id, source_type, map_node_id, exploitation_pressure,
pressure_thresholds, spawn_tags, warning_events, mitigation_actions, old_rite_tags.

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

---

## 17. Combat and night defense (co-pillar)

Direct control + autonomous defenders (the Thronefall model): the player drives the
**Bellkeeper** directly; **warriors**, **settler archers**, and the **beast** fight on their
own. Two tiers hold the night:

- **Settlers** shelter in their homes. If the night stays away from their door, they sleep
  and recover. If danger reaches the house, interior lights flare, silhouettes appear in the
  windows, and they provide **ranged support (arrows, slings) from lit windows**. A house only
  defends while it is **lit** (window light ties defense to the oil/wood economy), so house
  placement becomes a fields-of-fire decision over the approaches. Waking to fight costs
  sanity: the household survives the attack, but the people inside lose sleep, hear the
  scratching at the door, and work worse the next day.
- **Warriors and the beast** are the only units that operate **in the dark** - where they
  fight **debuffed** (the combat-band gradient, §2) and, for humans, drain sanity (§18).
  Warriors are produced and **upgraded** from buildable structures (barracks / watch line) -
  a real progression axis across the season. The **beast is exempt from both band and
  sanity**: full strength in every zone, your one true striker for the dark.

**Escalation, not a flat wave:** summer nights are intimate; autumn and winter nights grow
into set-piece defensive stands; the **White Nights** are the climaxes.

**Anti-turtle - safety is conditional (design invariant).** Ranged-from-home is only safe
while the outer line holds. If the warriors and the beast are **overwhelmed**, monsters reach
the houses and **destroy them, killing the settlers inside** - which collapses that light node
(the territory shrinks) and costs colonists (the spring-ship manifest, §21). Hiding everyone
indoors and shooting is therefore not a winning line; you must hold ground outside. On top of
that, every night carries at least one problem ranged fire cannot answer - a lantern moth
opening a dark gap that must be physically relit, a monster hugging cover out of arc, a
stranded settler to rescue, a wounded beast to reach - keeping *brave-into-the-dark* a live
choice, not a mistake.

## 18. Sanity, the dark, and the asylum

The dark is hostile territory to minds as well as bodies. Human units accumulate **dread** in
Edge/Dark zones (extending the existing villager Fear value); past a threshold they slip
toward **insanity** - erratic, unreliable, eventually incapacitated.

Homes are not free safety. A quiet home is recovery: settlers sleep, fear cools, and sanity
stabilizes. A woken home is defense: lights flare, silhouettes appear, and settlers fight
from windows, but everyone inside takes a sanity hit for being awake with monsters at the
door. Repeated night wakeups make the same family worse at daytime work even if nobody is
physically hurt.

**Insanity is usually the price of an under-fuelled night, not bravado.** If you don't
stockpile enough oil/wood, a light dies; the territory it held goes dark; whoever is caught
out there - a campfire lost far from the abbey with a long walk home, or fighters sent past
the light - takes the sanity hit. It couples directly to the economy: fail to provision the
lights and the dark takes minds.

**The key asymmetry - the day forgives the body but not the mind.** Every morning all units
recover **health** for free (the bucolic day). **Sanity does not.** An insane unit must spend
**cooldown time in the Asylum**. The Asylum is the recovery building for insanity, not a
medical infirmary. Recovery spans a full cycle, so the unit **misses the next night's
defense**, and can only be **released during the day**. This is what gives darkness lasting
weight while the day stays gentle - health is a nightly reset, sanity is a debt that carries.

Sanity affects labour directly. As sanity drops, villagers walk slower, work less steadily,
panic earlier at dusk, and are more likely to abandon tasks. Above the insanity threshold,
they stop working and can no longer be assigned normal jobs until recovered.

If the Asylum exists, insane villagers are removed from the work roster and recover there,
missing work and the next night's defense. If the Asylum does not exist, they recover slowly
at home, but the household pays for it: screaming, crying, sleeplessness, and nightmares
spill dread into other residents. This can turn one broken settler into a household-level
problem, which gives the Asylum a social purpose beyond faster recovery.

Two distinct mental costs keep the two human defense tiers honest:

- **Warriors pushed into the dark → sanity** (multi-day: asylum, miss the next night). The
  **beast is immune** - it is a beast, and one more reason it stays singular (pillar 3),
  never a replaceable soldier.
- **Settlers defending from lit homes → sanity loss** (next-day productivity loss, with
  longer-term risk if wakeups repeat). They fight from window-light, but they still spend the
  night afraid and awake.

## 19. The ground remembers - scars and paths

Both halves of the cycle **write to the ground**, but they write differently - night-writing
is destructive and **ephemeral**, day-writing is constructive and **durable**:

- **Night scars** - scorch where flame was carried, trampled ground on the beast's patrol
  line, drag-trails and dark stains at kill sites, spent-arrow scatter beneath the defended
  windows. Scars **stay through the morning** (you read the whole shape of last night by
  daylight), then **fade across the day** - grass and flower-meadow **regrow before the next
  night**, so dusk always finds a fresh, bucolic world for the new terror to violate. (In
  winter there is no regrowth: overnight **snow** covers the marks instead - a clean white
  silence, and fresh blood on new snow reads far louder than on summer grass.)
- **Day paths** - repeated work routes wear **desire paths** into the ground. The player
  does not place roads directly. Villagers create them by hauling, building, guarding, and
  returning home. Unlike scars, paths **persist**, and they are **infrastructure**: units move
  **faster along them**, so warriors redeploy quickly between threatened lit zones at night.
  A useful path also becomes a liability: if the settlement depends on it after dusk, it needs
  lantern coverage, and that burns fuel. Good daytime work prepares the battle lines for the
  night, but wide daytime growth also creates more darkness to maintain.

Architecturally the scar pass is *one more consumer of the shared event log* (§16): a dawn
decal pass reads the night's records, stamps the terrain, then fades it over the day - no new
source of truth. Emotional target: *relaxing to be in by day, yet you feel last night in the
dirt.*

## 20. Island exploration and population growth

The island map unlocks through daytime expeditions and risky edge-of-light scouting. The
player should not see the whole island at once. Smoke, bells, coastlines, old roads, stone
markers, abandoned shrines, wreckage, animal trails, and survivor signs reveal the next
places to investigate.

Exploration nodes should do at least one concrete thing:

- reveal resources, survivors, old-rite sites, nightmare causes, or weather forecast clues;
- open a new seed slot, shrine, road marker, salvage site, or old route;
- create a moral choice that affects Bellkeeper trust, Abbey sanctity, Old Faith pressure, or
  beast status.

Population grows in two ways:

- **They find the village.** Smoke, lit roads, the abbey flame, and the bell draw refugees at
  dawn. A visible, surviving settlement becomes a beacon.
- **The village finds them.** Expeditions discover stranded sailors, hermits, woodcutters,
  deserters, pilgrims, prisoners, and children hidden in ruins.
- **The sea throws them ashore.** Later storms can wreck other boats on the island coast.
  These events bring survivors, specialists, warriors, supplies, and news of the wider world,
  but they are never free population. The player must find the wreck, light the route, rescue
  people before night or tide, and decide who gets shelter.

Light makes the village visible, trust makes people stay, and exploration finds people before
the dark does. Low Trust in Bellkeeper can make new arrivals refuse night work, military
service, expeditions, laws, or the spring voyage. Low Abbey sanctity can make pilgrims avoid
the abbey or arrive frightened.

New wrecks should be emotionally pointed because the Bellkeeper already failed one ship.
Saving another crew can restore trust. Abandoning one, arriving too late, or stripping its
cargo before saving people can damage trust and seed drowned nightmares.

Warriors stay rare. Some are found, some arrive because the bell is known, and some villagers
can train into militia. They may refuse service if trust is low, laws are cruel, or the
settlement keeps breaking promises.

## 21. Winter and the spring ship (the season win)

The seasonal arc has a destination. The macro-goal is not merely to *survive* winter but to
reach spring with the means to **leave**: build a new vessel on the abbey shore and sail for
better coasts when the trade winds resume. This mirrors the opening - you *begin* stripping a
wrecked ship and *win* by launching a new one.

The win is a **three-part manifest**, which makes the economy a *spend-to-survive vs.
save-to-escape* tension all season (every log burned for warmth is a log not in the hull):

1. **Settlers** - enough surviving colonists to be worth sailing, and to crew the ship.
2. **Provisions** - food/water banked for the voyage, beyond what winter itself consumes.
3. **Hull & rigging** - shipbuilding materials: wood, **canvas (sailcloth)**, rope, iron.

Because every settler is also a future colonist, losses hurt twice (a worker now, a berth in
spring) - which is what makes the defensive co-pillar (§17) matter and feeds the moral
pressures (§11). If the ship can only be provisioned for *N* and more survived, the
**who-sails / who-stays** dilemma writes itself, and carries the campaign into Phase 4
(arriving at a new coast).
