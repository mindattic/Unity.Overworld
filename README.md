# Unity.Overworld

A Unity 6 rebuild of *Overworld*, a 2006 VB.NET / DirectX 9 top-down overworld prototype: a party of three led by the mouse across a
grid of painted tiles, a clock that turns a real second into a game minute, a one-line script language behind every item and event,
and a stamina-gauge battle mode run from a ring menu around the active hero.

[![Unity](https://img.shields.io/badge/Unity-6000.4.3f1-black)](ProjectSettings/ProjectVersion.txt) [![Status](https://img.shields.io/badge/status-reconstruction-orange)](docs/BIBLE.md)

The rebuild is in progress: the world draws as the original drew it (the 11 x 11 grid of area tiles under the 45-degree camera,
the backdrop box, and the heroes and props back to front), and Chrono moves as the original moved him: hold the right mouse
button to head for the cursor (walking near it, running when it is far or the run key, Right Ctrl or Space, is held), or use the
arrow keys. The mouse is the world cursor (the system pointer is hidden and held while the window has focus): push it to a
screen edge to scroll (Shift for three times as fast), the camera eases back onto Chrono while he moves, the wheel or the middle
button zooms between -1 and -3.5, E shows the examine eye, and F (free camera), X / Z and the numpad are the original's debug
camera moves. The stories that make the rest play follow in build order.
`.\play.ps1` builds the Windows player (headless) when needed and launches it muted; `-Sound` opts back in to audio and
`-Capture <file.png>` saves a screenshot and quits (`-overworld-capture-after <seconds>` plays that long first). Scripted runs
hold keys with `-overworld-hold right,run` or the move button with `-overworld-move-to <x>,<y>[,run]` (a world point), and drive
the view with `-overworld-pointer <x>,<y>` (the virtual 640 x 480 mouse; 608 or 32 and 448 or 32 are the scrolling edges),
`-overworld-wheel <steps>`, `-overworld-view-hold zoom,shift,x,z,num4,num6,num8,num2`, `-overworld-examine` and
`-overworld-free-camera`; `-overworld-recall <hero>[,<seconds>]` recalls Belmont (1) or Flagg (2) beside the leader, at once
or after that many seconds, so it falls in from z -1; a scripted run never takes the real mouse.

## Why

The original prototype tried a few things worth rebuilding properly:

- **The mouse is the player.** Hold the right button and the hero walks toward the cursor (running when it is far); the world cursor
  scrolls the camera at the screen edge and turns into a zoom hand or an examine eye.
- **A tiny script language runs everything.** `CHANGE HERO HP +10 | CHANGE TIME HOUR 22` is how every herb, book, rune, quest end and
  event is written, and a tilde console types it live.
- **A living clock.** One real second is one in-game minute; Morning, Day, Evening and Night tint the world; sleeping skips hours;
  the calendar has its own months and weekdays.
- **Stamina is the turn.** Battle has no initiative roll: the gauge that powers running is the turn timer, spent through a ring menu
  (Move, End, Skill, Item, Attack) around the active hero.
- **A world made of data.** 372 pickups, quests, five books, walls, props and, in the later 2007 design, areas, scenes, terrain, NPCs and
  enemies, all as rows.

See [`docs/RECONSTRUCTION.md`](docs/RECONSTRUCTION.md) for the mechanic-by-mechanic record (and how the source and the built copy
of the original relate), [`docs/BIBLE.md`](docs/BIBLE.md) for the rebuild's brief and its decisions beyond the original, and
[`docs/USER_STORIES.md`](docs/USER_STORIES.md) for the 33 stories of epic `U.OVW-A`, in build order.

## Status

Reconstruction-complete; the engine-free foundations (U.OVW-US-A1) are in: `Assets/Unity.Overworld/Core` (pure C#, `noEngineReferences`: players, commands, events, the fixed 60 Hz `Game` step and general world types), `Runtime`, `Editor` and `Tests/EditMode`, one-way references into Core. Run Core's tests outside Unity with `.
un-core-tests.ps1` (`Tests/Core.Tests`). Every story is planned, in dependency order (`tickets story list --repo
Unity.Overworld`):

- Engine-free Core, data files, asset catalogue
- World grid, camera, hero locomotion, cursor, walls, party
- Clock and light, vitals, rest, pickups, inventory, scripts, quests, books, weather
- Battle mode and the ring menu, HUD and screens, gamepad, audio, options, defeat
- The later data layer: areas, events, NPCs, enemies, combat, equipment, extra sprite packs, unfinished-hook seams
- An end-to-end verified run

## Original source

Read-only references, never edited by this project:

- `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\overhead 2` — the VB.NET / DirectX source project and its build output.
- `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\Overworld` — the built copy with its data (books, `world.mdb`, `hero.mdb`), `gfx` and `sfx`.

## Session mute

This repo references `com.mindattic.unity.shared` (`Packages/manifest.json`) for its CLI/automation session
mute: launched with `-muted` or `MINDATTIC_CLI_MUTED=1`, Unity.Shared's bootstrap turns on
`Unity.Shared.Core.Profiles.SessionMuted` before the first scene loads, and
`Assets/Unity.Overworld/Runtime/SessionAudio.cs` zeroes `AudioListener.volume` for that run. The flag is
session-only and read-only against the player's own settings: nothing is ever written to settings.json, and a
normal launch afterwards has sound again.

`play.ps1` (modelled on Unity.QuickDuel's) launches the player with `-muted` + `MINDATTIC_CLI_MUTED=1` by
default; `-Sound` opts back in.
