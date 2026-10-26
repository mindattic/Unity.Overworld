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
camera moves. Belmont and Flagg follow the leader as the original's party did (running when more than 0.2 away, walking
when near, standing within 0.05, recalled beside the leader, falling in, when a unit behind, and sliding apart when they
crowd); `[` and `]` or a left click on a hero choose the leader, and `'` or the team button at the top left turns following
off (solo), so the others kneel. The clock runs a minute a second from 9:00 AM on Terraday, Frostfall 1, 64 CE: the HUD
at the bottom left shows the period and time, the world's light fades toward evening red, night blue and morning white, and each
period's name drifts down from the centre as it begins. Each hero has 50 health, 20 mana and 60 stamina at most (starting at 31,
12 and 60): every 2 seconds standing gives back a point of stamina, kneeling two, running takes one, and poison takes a point of
health; the status box at the top left shows the leader's (and the team's) HP, MP and AP, the three bars at the bottom left fill
by the leader's, and an Exhausted, Angry or (green) Poisoned animation plays above a hero below 15 percent stamina, below 5 percent
health or poisoned. `\` rests: once kneels, again lies down ("Rest for 1 hour?" at the centre; the arrows set 1 to 24 hours),
again kneels; Enter or a left click while lying down sleeps, fading the world to black, giving every hero an eighth of its maximum
health, mana and stamina an hour (no stamina while poisoned), moving the clock on and fading back in. The original's 372 pickups
(and the restored Nitro Bean) lie where it put them: the herbs, the potato and turnip fields, the sword, books, rune, food and
beans, and the gold, five-gold and satchels scattered at random. Walk within reach (0.05) and the selected one's name shows over it; `,` and
`.` (or the wheel) choose among those in reach; Enter or the left button kneels and picks it up: coins pay out at once, the rest
go into the party's pack, and picked herbs and crops grow back after their growth time. With E on, the cursor over a pickup
opens the examine box (name, effect, description, weight and value). The stories that make the rest play follow in build order.
`.\play.ps1` builds the Windows player (headless) when needed and launches it muted; `-Sound` opts back in to audio and
`-Capture <file.png>` saves a screenshot and quits (`-overworld-capture-after <seconds>` plays that long first). Scripted runs
hold keys with `-overworld-hold right,run` or the move button with `-overworld-move-to <x>,<y>[,run]` (a world point), and drive
the view with `-overworld-pointer <x>,<y>` (the virtual 640 x 480 mouse; 608 or 32 and 448 or 32 are the scrolling edges),
`-overworld-wheel <steps>`, `-overworld-view-hold zoom,shift,x,z,num4,num6,num8,num2`, `-overworld-examine` and
`-overworld-free-camera`; `-overworld-recall <hero>[,<seconds>]` recalls Belmont (1) or Flagg (2) beside the leader, at once
or after that many seconds, so it falls in from z -1; `-overworld-solo` starts with the team off, and
`-overworld-select <hero|next|previous>[,<seconds>]` makes Chrono (0), Belmont (1) or Flagg (2) the leader, or steps as `]` / `[`
do; `-overworld-clock <h:mm>[,<seconds>]` sets the time of day (period and light settled at once) and, given seconds, how many
real seconds a minute takes (0: a minute every 60 Hz tick, a day in 24 seconds); `-overworld-vitals <hero>:<key=value,...>[;...]`
sets vitals as CHANGE HERO did (`hp`, `mp`, `ap`, and `poisoned` or `fine`; e.g. `0:ap=5;1:hp=1;2:poisoned`); `-overworld-rest <kneel|lay|hours>[,<seconds>]` kneels, lies down or sleeps that many hours, and
`-overworld-sleep-until <hour>[,<seconds>]` sleeps until that hour as the Moon Berry (22) and Morningstar (5) do;
`-overworld-seed <n>` fixes the random scatter of the gold and satchels; `-overworld-place <x>,<y>` puts the leader there;
`-overworld-reach <pickup>[,<seconds>]` puts the leader beside that pickup (0 to 372) and selects it, `-overworld-pickup <pickup>[,<seconds>]`
then holds the pick-up button, and `-overworld-examine-pickup <pickup>[,<seconds>]` opens the examine box over it; a scripted run never takes the real mouse.

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
