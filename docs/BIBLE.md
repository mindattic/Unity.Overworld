---
codex: 1
project: Unity.Overworld
layer: bible
status: living
updated: 2026-10-23
---

# Overworld — rebuild bible

The brief for the Unity rebuild. `docs/RECONSTRUCTION.md` is the evidence of what the original did (read it first; section numbers
below refer to it). This file says what we are building: the original's gameplay kept exactly, its code replaced by modern,
tested, data-driven Unity. Where the rebuild deviates from the original, it says so here and why.

## Pitch

A top-down overworld adventure seen through a 45-degree perspective camera over a grid of hand-painted 512-pixel tiles: lead a
party of three through a world of herbs, crops, books and quests, mouse-driven (hold the right button to walk toward the cursor,
left to pick up), with a day and night that turns on a one-minute-per-second clock, weather that cycles at a key press, a script
language that runs every item and event, and a stamina-gauge turn-based battle mode driven by a ring menu around the active hero.
Originally a 2006 VB.NET / DirectX 9 prototype ("overhead", then "Overworld"); the rebuild is its finished, polished proof of concept.

## Pillars

1. **The mouse is the player.** Hold the right button and the hero walks, or runs when far, toward the cursor; the world cursor is a
   real object with edge-scroll arrows, a zoom hand and an examine eye. (RECONSTRUCTION 3, 5)
2. **Everything is a script.** Item effects, quest completions, area events and the debug console all run through one tiny
   command language (`CHANGE HERO HP +10 | CHANGE TIME HOUR 22`), so content is data and behaviour is one interpreter. (9)
3. **A living clock.** One real second is one in-game minute; the day moves through Morning, Day, Evening and Night with tinted
   light, sleep skips time, and the calendar has its own month and weekday names. (6)
4. **Stamina is the turn.** Battle has no initiative roll: the gauge that powers running and moving is the turn timer, the ring
   menu is how you spend it, and everyone recharges kneeling. (12)
5. **The world is a data table.** Pickups, quests, books, walls, props and (in the later layer) areas, scenes, terrain, NPCs and enemies
   are rows, not code. (8, 10, 16)

## Scope and the union

The rebuild is finite. It covers the union described in RECONSTRUCTION section 19: the shipped 2006 behaviour first, then the
later data layer (the 2007 `world.mdb`, `hero.mdb`, the unused art and sound). The 33 stories of epic A in `docs/USER_STORIES.md` are
the whole of it, in build order (RECONSTRUCTION section 20). When all are built and the verified run (story A33) passes, the
repo is done.

## Architecture

Follows the owner's rebuild standard in AGENTS.md (Unity.SpaceGod and Unity.GridGame are the reference architectures).

```
Assets/Overworld/
  Core/        Overworld.Core      pure C# (noEngineReferences, no UnityEngine): rules, state, commands, events, data models, the script interpreter
  Runtime/     Overworld.Runtime   MonoBehaviours and presentation: camera, sprites, HUD, input, audio, UI Toolkit screens; references Core only
  Editor/      Overworld.Editor    editor-only builders and importers (scene, atlas and catalogue generators); references Core and Runtime
  Content/     validated data      JSON the Core loads: pickups, quests, books, walls, props, areas, scenes, terrain, NPCs, enemies, heroes, weapons
  Art/ Audio/  the original assets, imported verbatim and catalogued
Tests/
  Overworld.Core.Tests   NUnit, plain `dotnet test`, no Unity: rules, scripts, data validation, the asset-catalogue diff
  PlayMode / EditMode    Unity tests for Runtime wiring and scripted play-through runs
```

- **Core never sees Unity.** Vectors, colours, time and randomness are Core types behind small interfaces (`IClock`, `IRandom`), so every
  rule is deterministic under test. Randomness is seeded, so the random gold and weather are reproducible in tests.
- **Commands in, events out.** Input becomes a command carrying a player id (`MoveHero`, `PickUp`, `UseItem`, `ToggleBattle`, `Rest`, `RunScript`, ...);
  `Game.Apply(command)` mutates state and returns events (`HeroMoved`, `ItemPickedUp`, `PeriodChanged`, `QuestObjectiveCompleted`, ...).
  Presentation reads state and plays events, and never mutates the game. There is one authority and no "current player" singleton:
  `currHero` becomes a per-player selection in state, so a second player is a data change, not a rewrite.
- **A fixed 60 Hz step.** The original moved, scrolled, rained and drifted per rendered frame and only animated and kept time by the clock. The rebuild
  runs the simulation at a fixed 60 Hz and converts every per-frame constant to per-tick, so the game plays at the speed the owner remembers
  on a 60 Hz display regardless of machine; animation frame rates and the clock stay in seconds. Presentation interpolates.
- **Content is data, validated on load.** Every table in RECONSTRUCTION 8, 10, 11, 12.4 and 16 becomes a file under `Content/`; a loader validates references (item textures
  exist, scripts parse, quests reference real objectives) and fails loudly. All tuning numbers (speeds, regen rates, ranges, costs, colours, volumes) live in one
  settings registry and surface in the options screen where the original had none.
- **Built in code.** One near-empty scene; a bootstrap builds the world, camera, HUD and screens in code (UI Toolkit with a shared theme), generated from the data.
  No prefab or Inspector wiring the owner has to maintain.
- **Modern Unity.** Unity 6000.4.3f1, URP behind a pipeline-agnostic layer, the Input System (the original's key, mouse and gamepad map as rebindable actions),
  `com.mindattic.unity.shared` profiles for settings (replacing the registry key), `com.mindattic.capture` for the scripted-run screenshots.
- **Rendering.** A perspective camera at 45 degrees looking straight along the world's depth axis, world units as in the original (one unit per area tile, the
  hero 0.144), sprites as quads with alpha cutout (the magenta colour key becomes real transparency at import), draw order by Y as the original sorted, weather
  and clouds as depth-layered particle quads at their original depths. The 4:3 640 x 480 reference is kept as the base aspect; other aspects letterbox or extend the
  horizontal view without changing the world's scale.

## Decisions beyond the original

Each is a place where the original is silent, broken or ambiguous. They are decided here so no ticket has to guess; a ticket that wants to change one amends this table.

| Topic | Original | Rebuild |
|---|---|---|
| Frame-rate dependence (RECONSTRUCTION 18) | Per-frame speeds on an unthrottled loop | Fixed 60 Hz tick; per-frame constants kept numerically as per-tick values |
| Death and `Reset` (6.5) | Fades to black, then calls `Reset` every frame forever because health is never restored | Same fade and the same `Reset` effects (pickups returned, inventory emptied, camera home, morning), then the party is restored to full health at its start positions and play resumes |
| `checkScripts` never called (9.3) | A `MOVE HERO TO` script locks input forever | The completion check runs every tick; the script ends when every target axis is within 0.05, as the code was written to do |
| Music never plays (15) | The track is constructed and never started | Music plays and loops (the track the original loaded, volume 0), switching to Battle.mp3 in battle mode; the unused effects are wired to the events they were evidently made for (footsteps, swings, hits, defend, charge, regen, deaths, jump); ambient zombies loops in the dark woods event area |
| Events (9.4) | Loaded, never evaluated | Evaluated: standing inside an event rectangle runs its script at its rate (the data-driven design the 2007 database is built for) |
| Poisoned icon (7) | Reuses the Angry frames (the Poisoned folder is empty) | A distinct green-tinted poisoned animation derived from the Angry frames, flagged for replacement art |
| Nitro Bean (8.1) | Overwritten by Cheese before use | Both exist: Cheese at pickup 102, the Nitro Bean (+200 stamina, value 120) as an added 373rd pickup at its original coordinates |
| Rusty Sword (8.1) | `CHANGE HERO WEAPON ITEM` has no handler, so it fails | `CHANGE HERO WEAPON` equips the carried item (the Equipable type's evident purpose), ATK 5, DEF 1, DUR 2/10 as its effect text says |
| Quest Exp at the last quest (10) | Re-awarded every frame | Awarded once; the final quest stays Success |
| Quest Exp use (10) | A counter nothing reads | Shown on the hero screen; no levels (none existed) |
| Clock noon (6.1) | 12 PM shown as AM | 12:00 shows PM |
| Roman numerals past 64 (18) | A few entries repeat or are wrong (65 to 69, 74 to 80) | Correct numerals; page numbers beyond 16 never occur in the shipped books |
| Mana assignment clamp (18) | Clamped to current mana | Clamped to maximum mana, like health and stamina |
| Hero sprites (4.1) | All three heroes use the Chrono art | Kept; Belmont and the girl sprite pack are optional per-hero art (story A31) |
| Settings (2.2) | Registry key | A MindAttic profile `settings.json`; first run still shows the welcome text |
| Debug keys (5.2) | F1 to F5 wireframe, texture, culling, FPS, filter; G/H ring entries; U; L; P/O path dots | Available behind a developer-tools switch (off in the shipped build), except FPS (F4) which stays; the wall lines draw only in developer mode |
| Pause (2.4) | A flag with a banner and no key | P is taken by the path tool in developer mode; Pause is bound to the Pause key and Escape opens the options menu, whose Exit quits (the original's only quit key was Escape directly) |
| Combat (12.4) | No damage, enemy or attack code | Defined in story A29: a hit's damage is the weapon rating scaled by its slashing/piercing/blunt split against the target's class; durability drops one per hit; enemies have the health, speed, range, state and motion the data gives. Every number is a tuning value in the registry |
| Enemy AI states (16.2) | Passive, Aggressive, Afraid, Defensive as names | Passive ignores the party; Aggressive pursues within range; Defensive holds until hurt or approached within half its range; Afraid retreats; speeds from the data |
| NPC states (16.2) | "stand" and "pursue" as names | Stand idles facing the party; pursue follows the leader at the NPC's data speed without attacking |
| Items in later areas | Only area 0 data existed for pickups | Pickups stay in the original world (the 53 tiles); the Grasslands area (story A25) carries the 2007 props, NPCs and enemies |

## Open questions (flag when a ticket hits one)

- Which of the two areas systems wins when both apply: the 2006 tile world (A1 to F8) and the 2007 Grasslands/Lowlands scenes. The plan is both: the tile world is the
  default "overworld", Grasslands and Lowlands are reachable areas. A ticket that needs to choose raises it.
- Whether "Lowlands" (area 2) has any scenes: the data has none; it is built as an empty reachable area until the owner supplies content.
- What the Skill ring entry offers beyond the Flame spell seam (story A32).

## Laws

See AGENTS.md for the non-negotiables (REA the original in its totality first, an engine-free tested Core, hard assembly boundaries, content as data, built in code, modern
Unity only, done means verified by play, finite).
