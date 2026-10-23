# Unity.Overworld

A Unity 6 rebuild of *Overworld*, a 2006 VB.NET / DirectX 9 top-down overworld prototype: a party of three led by the mouse across a
grid of painted tiles, a clock that turns a real second into a game minute, a one-line script language behind every item and event,
and a stamina-gauge battle mode run from a ring menu around the active hero.

[![Unity](https://img.shields.io/badge/Unity-6000.4.3f1-black)](ProjectSettings/ProjectVersion.txt) [![Status](https://img.shields.io/badge/status-reconstruction-orange)](docs/BIBLE.md)

There is no build yet. This repo currently holds the Unity project scaffold, the reverse-engineering record the rebuild is written
from, the design brief and the full list of stories in build order.

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

Reconstruction-complete, implementation not yet started. Every story is planned, in dependency order (`tickets story list --repo
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
