---
codex: 1
project: Unity.Overworld
layer: bible
status: living
updated: 2026-10-24
---

# Overworld â€” rebuild bible

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
               (lives at Assets/StreamingAssets/Content: content.json names every file; both world.mdb databases and hero.mdb are
               exported there by tools/export-mdb.ps1, the books by tools/export-books.ps1; Core's ContentRegistry loads and validates them,
               naming every problem by file, key and line. world-2007.json is the playable world, world-2006.json a reference record.
               The 2007 export stays exactly as the database held it; world-2007-fixes.json holds the corrections, the start and the
               exits, applied when the playable world is read, and WorldPlayability checks the result every load: see Fixed defects.)
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
  on a 60 Hz display regardless of machine; animation frame rates and the clock stay in seconds. Presentation interpolates. Core's tick is driven by its own accumulator, independent of `FixedUpdate`.
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
| Near plane (3.2) | `PerspectiveFovLH(PI/4, 4/3, 0, 100)`: a near plane of 0 | 0.01, the nearest a Unity camera allows (view.json); nothing the game draws comes nearer the camera than that except a recalled hero at Z -1 with the camera at -1, which neither version shows |
| Aspect (3.2) | Fixed 4:3 in a 640 x 480 window | The 45-degree vertical field of view is kept and the aspect follows the window, so wider windows see more to the sides at the same world scale; 640 x 480 reproduces the original frame exactly (the capture tools render at that size) |
| Colour key and layering (2.3, 3.3) | `TextureLoader.FromFile` with key 0xFFFF00FF; alpha blending with every quad at z 0, so later draws cover earlier ones | The art is imported verbatim under `Assets/Unity.Overworld/Art/Resources` at its original path, the importer turning opaque magenta into transparent black as Direct3D did; every quad is a sprite-shader quad layered by sorting order in the original's draw order (backdrop, tiles, then objects back to front by Y) |
| Frame-rate dependence (RECONSTRUCTION 18) | Per-frame speeds on an unthrottled loop | Fixed 60 Hz tick; per-frame constants kept numerically as per-tick values |
| Walking down after running down (4.4) | `aiWalking`'s Down branch only ever lowers `dY` toward `-walkSPD` (it lacks the matching rise), so a hero who runs down and lets go of the run key keeps running speed while showing the walk cycle | Fixed: every direction steps toward its speed from above or below in 0.001 steps, so the walk down settles at -0.003 like the other three directions (U.OVW-T9; Core `LocomotionTests`) |
| Free camera hold (5.3) | `checkScroll` lets the free camera (F) scroll past the limits around the hero, but `renderCursor` then clamps the camera to hero +0.6 / -0.45, +0.35 / -0.4 every frame regardless, so the free camera never scrolled further (only its zoom was free) | Fixed: with the free camera on the hold is lifted too, so the edges, numpad and X / Z move the camera anywhere; with it off everything is held exactly as before (U.OVW-T10; Core `CameraControlTests`) |
| Diagonal running animation (4.4) | Running up or down while a side arrow is held shows the side's run cycle but advances it at the walk rate and frame count (the run-up/down branches reuse the walk numbers) | Fixed: every run cycle advances at the hero's run rate, six frames, so a diagonal run animates like a straight one (U.OVW-T9; Core `LocomotionTests`) |
| Walls (3.5) | Six segments hard-coded in `Walls.loadwall`; `checkWall` rewrites each hero's velocity before it moves (flat and angled walls set `dY` to +/-0.001 within twice `|dY|`, angled ones also zero `dX` within 0.03, the vertical one sets `dX` to +/-0.001 within twice `|dX|` along its length); the walls drawn every frame as 1-pixel debug lines | The same rule in Core's `WallCollision`, run per hero per 60 Hz tick after its AI and before its move, in single precision; the segments and every figure (test point, lift, reach, push, angled stop) in walls.json. Kept exactly, including what it implies: a hero hovers within twice its speed of a wall rather than touching it, a still hero is never pushed, and a hero heading diagonally at the vertical wall's top end slides round it. The lines are not drawn (developer mode, see Debug keys) (U.OVW-T11; Core `WallsAndFallingTests`) |
| Recall and falling (3.7, 4.5) | `recallHero` places a hero within +/-0.05 of the leader (two `getRndPercent` draws, x then y) at Z -1; `checkFall` raises a hero below 0 by 0.04 a frame showing `halt_left`, then sets it to 0 | The same in Core's `Party` (figures in heroes.json "fall", the halt pictures in "sprites"): a `RecallHero` command (player id, hero) does what the follow rule (story A8) will do on its own; the random source is the party's own `System.Random` (seedable for tests and replays); `HeroRecalled` and `HeroLanded` events let presentation play it. A recalled hero is placed, not eased in, and is drawn at its depth, so it drops toward the ground from the camera plane (U.OVW-T11) |
| Hero controls (5.1, 5.4) | DirectInput: arrows, Right Ctrl / Space to run, right mouse button to head for the cursor, the cursor a quad on the ground | The Input System (actions built in code in `HeroInput`) with the same bindings; the move button heads for Core's world cursor (below); the held state is sent each frame as a `SetMoveInput` command carrying the player id, and the party applies it per 60 Hz tick in the original's getKeyboard, getMouse, KeysUp, updateHero order |
| World cursor and camera (3.2, 5.3) | The system pointer hidden, DirectInput's relative mouse motion moving a virtual 640 x 480 mouse (held 32 px in) and the world cursor (0.00172 a pixel); edge scroll, the hold and carry around the hero, centring, wheel and drag zoom and the debug keys per frame | The same rules, numbers in view.json, in Core's `CameraControl` (one `Viewpoint` of camera and cursor per player, installed after the party so it runs after updateHero, as renderCursor and initCamera did), per 60 Hz tick. The pointer is hidden and locked while the window has focus; its motion is scaled from window pixels to the virtual 640 x 480 and sent as `SetViewInput` (motion and wheel steps add up until a tick uses them); E and F are `ToggleExamine` / `ToggleFreeCamera` on the key press (F was a WinForms KeyDown, so holding it toggled on every key repeat). The wheel zooms one step per frame it turns, as the original's per-poll test did; the wheel's other uses (rings, books, inventory, items in reach) come with their stories. The camera and cursor are drawn eased between their last two ticks |
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
| Items in later areas | Only area 0 data existed for pickups | Pickups stay in the original world (the 45 loaded tiles); the Grasslands area (story A25) carries the 2007 props, NPCs and enemies |
| The world played (owner 2026-10-23) | The engine loaded the 2006 `data\world.mdb` (and used none of it); the 2007 root `world.mdb` was never read by any code | The 2007 world is the world the game starts in and plays, with the fixes below; the 2006 database is a reference record only. A new game begins in the Grasslands (U.OVW-T38) |

## Fixed defects

The 2007 world was never loaded by any code of the original, so nothing had ever found what in it does not work. U.OVW-T38 checked every
table, row, reference and picture of it against the original's files and fixed what is broken. Each data fix is an entry of
`Assets/StreamingAssets/Content/world-2007-fixes.json` with the same id (a Core test fails if a fix has no row here); `world-2007.json` itself
stays the faithful export, and `WorldPlayability` re-checks every rule below on every load, so undoing a fix fails loudly.

| ID | Defect in the 2007 world | Fix |
|---|---|---|
| W1 | The original engine could not open it at all: `getArea` queries `Pickup` and `Items`, which it lacks, and nothing reads its `Scene`, `Terrain`, `Props`, `Npc` or `Enemy` tables. Three columns hold numbers as text (`Area_Npcs.areaID`, `Enemy.enemyID`, `Enemy.enemyMAXHP`) | Core reads its own 15-table schema (U.OVW-T6) and the text columns as whole numbers, rejecting text that is not one. No data change |
| W2 | Scene 11 "Grasslands Sky 8" is in area 0, which has no Area row (0 is the 2006 database's Foothills of Phar; the copied row kept its area) | Moved to area 1, the Grasslands its name gives. The record keeps it as a warning; the played world allows no dangling area |
| W3 | Scene 11 at x 8 covers half of Scene 9 (Grasslands Sky 4, x 7 to 9): the same sky picture at the same depth, so they flicker against each other | Moved to x 9, continuing the sky row. No two pictures of an area may overlap at one depth |
| W4 | Scene 5's picture `new.png` (the 1x5 strip whose sky is see-through so the Mountain behind shows) is 441,816 zero bytes in the shipped folder (and in the Overhead RPG copy): it is not a picture | The asset import takes the intact file of the same size from the sibling project `Fish 2\bin\gfx\area\West of Town\new.PNG` (`assetSources`) |
| W5 | Every picture path has backslashes and a leading backslash, and 10 name an extension in the wrong case (`orchard_N.png` for `.PNG`, `gate.png`, `mountain.png`, `new.png`); only Windows' forgiving file system made them resolve | Each is the file's exact relative path (`gfx/prop/tree/orchard_1.PNG`); NPC folders end with `/`. A Core test checks every one against the original folder, byte signature included |
| W6 | NPC 1 is spelled "Peasent Woman" | "Peasant Woman" |
| W7 | No start: no area to begin in, nowhere for the party to stand | Area 1 (Grasslands), the party on ground line 1 at x -0.4 (Chrono), -0.1 (Belmont), -0.7 (Flagg): the 2006 heroes' order and 0.3 spacing reused, the point given is under each hero's feet |
| W8 | Area 2 "Lowlands" is a name only: no picture, no ground | A stand-in until the owner supplies content: one strip using the spare original 1x5 landscape (`bgLandscape2_1x5.jpg`, the picture `new.png` displaced) at x -1 to 1, and a ground line across it at the Grasslands' height (-0.3) |
| W9 | Nothing links the areas: no exit table and no event that moves the party, so the Lowlands could never be reached | Two exits: the Grasslands' east end (x 14.8 to 15, where its pictures stop; ground line 7 runs on to 16) to the Lowlands at x -0.6, and the Lowlands' west end (x -1 to -0.8) back to the Grasslands at x 14.5. Every area must be reachable from the start and lead back to it |

Checked and not changed, because they work as the 2007 design meant: the events and their scripts (Poison Box, Healing Box, and Entering Dark
Woods, whose "Night" script is the 2006 "Make Night" with the same effect, the interpreter upper-casing it); the props, including the Gateway at
z -0.5, a deliberate foreground piece (the 2005 sibling engine `Battle 2` places `gate.png` at the same depth); the half-height sizes (see
RECONSTRUCTION 16.2); the ramp and ledge of ground lines 8 and 9; the five enemies with no placement and no `Enemy_Images` rows (nothing
references missing art; their art and placement are story A28's). Nothing else of the 2006 database was needed: its three events, scripts and
starter items have no 2007 counterpart they would repair.

## Open questions (flag when a ticket hits one)

- Where the shipped 2006 tile world (A1 to F8, with its pickups, quests, walls, props and tent) sits now that a new game begins in the 2007
  Grasslands (U.OVW-T38): the 2007 database has no area for it and no exit to it. Linking it in (as an area, perhaps under its 2006 name
  Foothills of Phar) is the owner's call; until then it is built as before and reached by the stories that build it.
- What the Lowlands hold: the data gives none; W8's single strip is a stand-in until the owner supplies content.
- What the Skill ring entry offers beyond the Flame spell seam (story A32).

## Laws

See AGENTS.md for the non-negotiables (REA the original in its totality first, an engine-free tested Core, hard assembly boundaries, content as data, built in code, modern
Unity only, done means verified by play, finite).
