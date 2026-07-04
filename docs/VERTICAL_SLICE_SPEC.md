# Vertical Slice Spec: The First White Night

Map 1 — **The Bell Tower Hound**. Target length 20–30 minutes (Prototype 0.1 proves the
first night in ~5 simulated minutes; Phase 2 fills it to full length).

## 1. Player fantasy

*"I spent the day salvaging the wreck and building around the abbey. At dusk I rang the
bell, but one villager was too far out. I carried fire into the dark to rescue them. The
hound followed me because I had fed it earlier. During the night, monsters tested the edge
of the light. By morning, I had survived, but I knew winter would be worse."*

Sunny shipwreck settlement by day, panic at dusk, horror at night, and a real emotional
bond with the abbey beast. The player does not *unlock* the hound — they earn its first
moment of trust.

## 2. First map layout

Small coastal meadow:
- shipwreck beach (south-west)
- ruined abbey hill (north-east), broken bell tower on top — something huge chained inside
- meadow camp area between them (3 build zones)
- forest edge (north-west), small stream
- dark cave / tree line (2 salvage zones, 1 dangerous night path)

## 3. Opening sequence (the first 20 minutes)

| Minutes | Beat |
|---------|------|
| 0–3 | **Shipwreck.** Dawn. Salvage crates, gather timber, move supplies uphill, light first campfire, place first storage pile. World feels safe and beautiful. |
| 3–7 | **First camp.** Build campfire, storage, two shelters, woodcutter hut, lantern post; repair abbey gate. Villagers visibly haul, hammer, sit by fire. *"This is nice. I can make this place work."* |
| 7–10 | **Abbey discovery.** Bellkeeper enters: broken pews, old bones, claw marks, dead candles, stairs, a deep growl. At the top: the Black Hound — chained, wounded, surrounded by dead pale things it has killed. **First major choice**: feed it / free it / leave it chained / approach slowly (injury risk) / calm it with the bell. Sets the first bond state — never cosmetic. |
| 10–13 | **Dusk panic.** Birds stop, shadows lengthen, colour drains, the bell rings, UI warns *Nightfall*. Villagers outside light hurry home. One villager is too far: the first rescue moment. Hero can ring bell, call villagers, carry fire, escort, relight a lantern, check the hound. |
| 13–18 | **First night.** Intimate and scary, not a wave: 2–3 pale hounds, 1 shadow at the forest edge, whispers on the unlit road, maybe one panic event. Player learns: monsters avoid strong light; weak light flickers; darkness is territory; the hero can enter edge darkness. Hound behaviour depends on the earlier choice — fed: may break the chain to save the hero; starved: may kill a monster, drag the corpse away, and refuse the bell. |
| 18–20 | **Morning consequence.** Dawn report: injuries, food used, fires lost, hound trust/fear/resentment, what villagers now believe about the hero. Player immediately wants to continue. |

## 4. Starting resources

12 survivors · the Bellkeeper · a dying campfire · wreck crates (wood, food, oil, medicine,
rope, cloth, tools, oil casks, ship bell fragment, medicine chest) · limited food · wet
timber · one broken bell · no buildings · no walls · no beast trust · one wounded Black
Hound in the bell tower.

## 5. Buildable structures

Campfire, Storage Pile, Shelter, Woodcutter Hut, Lantern Post, Guard Post,
Abbey Gate Repair, Bell Tower Repair, Candle Shrine, Infirmary Corner.
(Prototype 0.1 pre-places campfire + lantern; construction UI is Phase 2.)

## 6. Villager behaviours

States: Idle, AssignedToWork, WalkingToTask, Working, CarryingResource, ReturningToStorage,
ReturningToLight, Panicking, Injured, Resting, Missing, Dead.
Roles: Salvager, Builder, Woodcutter, Tender, Guard.
Dusk: distance check → brave finish tasks, fearful abandon tools early, children first,
injured move slower; the bell speeds recall and lowers panic.

## 7. Bellkeeper controls

Move (direct control) · Ring Bell (recall + hound call + weak-nightmare stun) ·
Carry Flame (mobile temporary light) · Rally (calm nearby) · Strike (basic attack) ·
Rescue (guide/carry one villager) · Feed Beast · Touch Beast (high-risk calming).
Health + stamina kept simple.

## 8. Hound states

Values: trust, hunger, pain, fear, attachment_to_hero.
States: chained, wary, fed, following, guarding, hunting, protective, angry, missing,
wounded, trusting. Behaviour table in GAME_DESIGN.md §5. All transitions data-driven from
the shared event log — the fed-hound-saves-hero and starved-hound-ignores-bell branches are
the same system with different thresholds.

## 9. Light rules

Safe / Edge / Dark classification per position. Campfire = basic zone; abbey flame =
sacred; lantern posts = workable edges; carried flame = rescue tool; window light =
comfort; bell tower light = sacred signal. Fires consume fuel; unfueled lights flicker
then die; darkness is not visual only — it is hostile territory.

## 10. First night events

2–3 pale hounds test edges · 1 shadow at forest edge · whispers near the unlit road ·
possible villager panic · lantern moth may create a darkness gap (Phase 2) · drowned
sailor near the wreck if someone died by water (Phase 2) · hound intervention branch by
bond state · the First White Night as the climax special event.

## 11. Win/loss conditions

**Win**: survive until dawn after the First White Night with ≥6 villagers alive, the
Bellkeeper alive, and the abbey fire lit.
**Loss**: Bellkeeper dies, abbey fire goes out, or all villagers die/flee.
**Soft failure spectrum**: hound trusts/fears/vanishes; villagers hopeful/terrified; abbey
damaged; supplies low; injuries; one missing villager.

## 12. Required assets

Blender: shipwreck_hull, shipwreck_crate (closed/open), shipwreck_barrel, sailcloth,
campfire_t1, lantern_post_t1, shelter_t1, storage_pile_t1, woodcutter_t1, guard_post_t1,
abbey_gate (ruined/repaired), bell_tower (ruined/repaired), candle_shrine_t1,
infirmary_corner_t1, abbey_wall_broken, hound_chain, grave_marker, forest_tree_01/02,
rock_cluster, dirt_road_segment.
Characters (low-poly placeholders, never block gameplay on art): bellkeeper, villager,
black_hound (silhouette matters most: large, wounded, memorable), pale_hound,
drowned_sailor, lantern_moth.

## 13. Required tests

EditMode: light classification (overlap, extinguished, dark), resource accounting,
footprint validation, hound state transitions, game clock transitions.
PlayMode: dusk recall, hero rescue, monster avoids strong light, fed hound answers bell,
starved hound unreliable, first-night completion, win and loss both reachable.
Asset (pytest): GLB + metadata + previews exist, budgets respected, pivot/footprint,
anchors present, grayscale readability stub.

## 14. Things intentionally excluded

Seasons · full law system · farming · multiple beasts · multiple maps · final UI ·
advanced animation · complex combat · procedural maps · full campaign narrative.
This section exists to prevent scope creep. Additions require editing this file first.

> Direction note (2026-07-04): the *game* has since promoted combat to a co-pillar
> (Thronefall-meets-horror; see GAME_DESIGN.md §§17–20). This **slice** deliberately stays
> the intimate-first proof — the escalating warrior economy, sanity/asylum, ground scars,
> and the spring-ship win are **post-slice (Phase 3+)** and remain excluded here. One cheap
> Phase 2.5 taste worth prototyping after the gate: a dawn ground-scar decal pass driven by
> the hound's existing kill/drag log records, to sell "the day remembers the night" early.
