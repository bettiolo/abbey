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
5. **The Bellkeeper is a directly-controlled hero** — rescuer, signaler, guardian, beast-
   bond character, *and* front-line fighter. You drive the Bellkeeper directly (Thronefall-
   style); every other defender acts autonomously.
6. **Defense is a co-pillar; the night is a rising cost.** The settlement fields a real,
   buildable, upgradable defense — warriors, settler archers, the beast. Nights escalate
   from intimate dread in summer to set-piece defensive stands in autumn and winter — but
   the dark always taxes sanity, so combat is never a clean power fantasy.
7. **The day remembers the night.** Night movement and battle scar the ground; day labour
   wears paths into it. The day is bucolic to *be* in, yet the earth carries what happened.

> Pillar note (direction set 2026-07-04): combat was promoted from a thin support role to a
> co-pillar — *Thronefall-meets-horror-settlement*. The horror identity is preserved by the
> sanity cost of the dark (pillar 6), the beast's singular centrality (pillar 3), and
> intimate early nights that escalate only as the seasons turn. Detail in §§17–20.

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
trust/hunger, unburied dead, accumulated fear, bell tower state. Nights **escalate**:
intimate and scary first (summer), rising to set-piece defensive stands at the seasonal
climaxes (autumn/winter, the White Nights). Even the big nights stay tense rather than
triumphant, because the dark taxes sanity (§18) and every night includes at least one
problem only a dark-capable unit — warrior or beast — can solve (§17, anti-turtle rule).

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

---

## 17. Combat and night defense (co-pillar)

Direct control + autonomous defenders (the Thronefall model): the player drives the
**Bellkeeper** directly; **warriors**, **settler archers**, and the **beast** fight on their
own. Two tiers hold the night:

- **Settlers** shelter in their homes and provide **ranged support (arrows, slings) from lit
  windows**. A house only defends while it is **lit** (window light — this ties defense to
  the oil/wood economy), so house placement becomes a fields-of-fire decision over the
  approaches. Settlers who fight too much at night wake **fatigued and less productive** the
  next day.
- **Warriors and the beast** are the only units that operate **in the dark**. Warriors are
  produced and **upgraded** from buildable structures (barracks / watch line) — a real
  progression axis across the season. They pay the sanity cost of the dark (§18); the beast
  does not.

**Escalation, not a flat wave:** summer nights are intimate; autumn and winter nights grow
into set-piece defensive stands; the **White Nights** are the climaxes.

**Anti-turtle — safety is conditional (design invariant).** Ranged-from-home is only safe
while the outer line holds. If the warriors and the beast are **overwhelmed**, monsters reach
the houses and **destroy them, killing the settlers inside** — which collapses that light node
(the territory shrinks) and costs colonists (the spring-ship manifest, §20). Hiding everyone
indoors and shooting is therefore not a winning line; you must hold ground outside. On top of
that, every night carries at least one problem ranged fire cannot answer — a lantern moth
opening a dark gap that must be physically relit, a monster hugging cover out of arc, a
stranded settler to rescue, a wounded beast to reach — keeping *brave-into-the-dark* a live
choice, not a mistake.

## 18. Sanity, the dark, and the asylum

The dark is hostile territory to minds as well as bodies. Human units accumulate **dread** in
Edge/Dark zones (extending the existing villager Fear value); past a threshold they slip
toward **insanity** — erratic, unreliable, eventually incapacitated.

**Insanity is usually the price of an under-fuelled night, not bravado.** If you don't
stockpile enough oil/wood, a light dies; the territory it held goes dark; whoever is caught
out there — a campfire lost far from the abbey with a long walk home, or fighters sent past
the light — takes the sanity hit. It couples directly to the economy: fail to provision the
lights and the dark takes minds.

**The key asymmetry — the day forgives the body but not the mind.** Every morning all units
recover **health** for free (the bucolic day). **Sanity does not.** An insane unit must spend
**cooldown time in the Asylum** (a sanity-infirmary evolving the Phase 2 Infirmary Corner);
recovery spans a full cycle, so the unit **misses the next night's defense**, and can only be
**released during the day**. This is what gives darkness lasting weight while the day stays
gentle — health is a nightly reset, sanity is a debt that carries.

Two distinct night costs keep the two tiers honest:

- **Warriors / beast pushed into the dark → sanity** (multi-day: asylum, miss the next
  night). The **beast is immune** — it is a beast, and one more reason it stays singular
  (pillar 3), never a replaceable soldier.
- **Settlers defending from lit homes → fatigue** (next-day productivity only, clears with
  rest), since they fight from the safety of window-light.

## 19. The ground remembers — scars and paths

Both halves of the cycle **write to the ground**, but they write differently — night-writing
is destructive and **ephemeral**, day-writing is constructive and **durable**:

- **Night scars** — scorch where flame was carried, trampled ground on the beast's patrol
  line, drag-trails and dark stains at kill sites, spent-arrow scatter beneath the defended
  windows. Scars **stay through the morning** (you read the whole shape of last night by
  daylight), then **fade across the day** — grass and flower-meadow **regrow before the next
  night**, so dusk always finds a fresh, bucolic world for the new terror to violate. (In
  winter there is no regrowth: overnight **snow** covers the marks instead — a clean white
  silence, and fresh blood on new snow reads far louder than on summer grass.)
- **Day paths** — repeated work routes wear **desire paths** into the ground. Unlike scars,
  paths **persist**, and they are **infrastructure**: units move **faster along them**, so
  warriors redeploy quickly between threatened lit zones at night (a Settlers-2 road/flag
  network turned into a defensive-mobility layer). Laying good paths by day is preparing your
  battle lines for the night.

Architecturally the scar pass is *one more consumer of the shared event log* (§16): a dawn
decal pass reads the night's records, stamps the terrain, then fades it over the day — no new
source of truth. Emotional target: *relaxing to be in by day, yet you feel last night in the
dirt.*

## 20. Winter and the spring ship (the season win)

The seasonal arc has a destination. The macro-goal is not merely to *survive* winter but to
reach spring with the means to **leave**: build a new vessel on the abbey shore and sail for
better coasts when the trade winds resume. This mirrors the opening — you *begin* stripping a
wrecked ship and *win* by launching a new one.

The win is a **three-part manifest**, which makes the economy a *spend-to-survive vs.
save-to-escape* tension all season (every log burned for warmth is a log not in the hull):

1. **Settlers** — enough surviving colonists to be worth sailing, and to crew the ship.
2. **Provisions** — food/water banked for the voyage, beyond what winter itself consumes.
3. **Hull & rigging** — shipbuilding materials: wood, **canvas (sailcloth)**, rope, iron.

Because every settler is also a future colonist, losses hurt twice (a worker now, a berth in
spring) — which is what makes the defensive co-pillar (§17) matter and feeds the moral
pressures (§11). If the ship can only be provisioned for *N* and more survived, the
**who-sails / who-stays** dilemma writes itself, and carries the campaign into Phase 4
(arriving at a new coast).
