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
opens the examine box (name, effect, description, weight and value). Delete (or the inventory button under the team button)
opens the pack: its stacks ten to a row with their counts, the weight against the party's 28.5 and the gold; the arrows (or the
wheel) move the cursor, the run key marks a stack and swaps it with the next one chosen, Backspace (or the right button) drops one
by the leader, where it can be picked up again, and Enter (or a click) uses it: solo on the leader, in team mode a satchel, draught
or book at once and anything else on the hero chosen with left and right and Enter. Q shows the quick bar of the first ten stacks,
and 1 to 0 use them. A pack heavier than 28.5 burdens the party: the heroes kneel and "You are overburdened" shows until something
is dropped. `~` opens the script console (the green `/ > |` prompt at the top left): type the original's language, e.g.
`CHANGE HERO HP +10 | CHANGE TIME DAYPERIOD NIGHT` (C on an empty line types `CHANGE `, again `CHANGE CAMERA `; the `\|` key is the
pipe, `=` is Shift and the `+` key), Enter runs it and shuts the console, Esc, F11 or `~` shut it, and Insert brings the last one
back. Every item's effect, the Rune's teleport and the quests' end scripts run through the same interpreter; a command it does not
understand shows "Script failed to execute." and a number that is not one "Script failed to run.". While the console is open, or
a script walks a hero (`MOVE HERO TO x,y,z`, `CHANGE HERO AI WALKLEFT` until `CHANGE HERO AI STOP`), the keys and mouse do not
move the party. The original's quests run as it ran them: Farmer Brinn's north and south fields, the centre field, the west and east pair (going east sets the clock to 6 PM) and the far west, one at a time, and the two subquests (way south, far north) alongside; walking the leader into an objective's range completes it and pays its experience (100 to 1200; each quest 350 to 3500 more once all its objectives are done) into the party's, and a white `*` marks every objective not yet reached. Page Down opens the quest screen: up and down choose Quests, Subquests or Completed, right enters the subquest list and then shows the chosen one, left backs out. The five books (Grimoire de'Alchemiste, Dragon Almanac, Poetry Book, Matt's Book and Colesium Vol I) read two pages at a time on the original's book picture, the pages in the book's own lettering (Monotype Corsiva, Calisto ...) and numbered in Roman: using a book from the pack opens it in its cover and lettering, B shows and hides the book open, N moves on to the next book, left and right (or the wheel) turn a spread, and the right button hides it (with the pack open, Delete or Backspace hide it first). The weather falls as the original's did: 2,501 snowflakes drift down toward the ground (most start far above, so the snow thickens over the first 45 seconds), and W cycles it on to rain, clear skies, falling leaves (drawn with the snow picture, as the original had no leaf art), a dust storm blowing right and back to snow; 401 clouds and four hot-air balloons drift right over the world and wrap round at its east edge (they float between the ground and the camera, so they show once the wheel zooms out), the tent stands west of the start, and the four props (three tree trunks and a wall) stand north-east of it, all in the clock's light. End enters battle mode with the team: a hero whose stamina (its turn gauge) is full draws its weapon, Chrono his Fine Axe and Belmont and Flagg their Steel Swords, and holds its battle stance, the weapon drawn over it frame for frame; one below full kneels and recharges a point every 0.18 seconds, its gauge shown under its column of the status box ("NN %" fading in as it fills), and once full it is "Ready!" and draws. In battle the ring menu circles the active hero: Move, End, Skill, Item and Attack, turned with left and right (or the wheel), pulsed with Up and chosen with Enter or a click. Highlighting Move shows a dotted circle as far as the hero may walk (agility x 0.05 at a full gauge), Attack its weapon's range in red, Item the quick bar; choosing Move lets the hero walk there with the right button, its gauge falling as it goes, Item opens the pack (a use costs 10 percent of the gauge and brings the ring back; a pickup while moving costs 5), and End, or a spent move, kneels it to recharge and hands the turn to the next ready hero, the camera following. End again sheathes every weapon and the party is back in adventure mode. The stories that make the rest play follow in build order.
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
then holds the pick-up button, and `-overworld-examine-pickup <pickup>[,<seconds>]` opens the examine box over it;
`-overworld-give <pickup>[*<count>],...` puts those pickups' items into the pack (e.g. `0*3,201*2,105`), `-overworld-quickbar` shows the
quick bar, `-overworld-inventory <slot>[,<seconds>]` opens the panel on that stack, `-overworld-swap <from>,<to>[,<seconds>]` swaps two
stacks with the run key, `-overworld-drop <slot>[*<count>][,<seconds>]` drops from a stack (Backspace), `-overworld-use <slot>[:<hero>][,<seconds>]`
and `-overworld-quick <key>[:<hero>][,<seconds>]` use a stack from the panel or a number key (choosing that hero when the item asks for
its target, or leaving the target marked without one), and `-overworld-pickup-drop <drop>[,<seconds>]` picks a dropped item up again;
`-overworld-script "<script>"` types a script into the console key by key and presses Enter, `-overworld-console "<text>"` types
text and leaves the console open (for a capture of the prompt), both after `-overworld-script-after <seconds>`;
`-overworld-objective <q:o|s:n:o>[+...][,<seconds>]` completes those objectives in turn (main quest q's objective o, or subquest n's), the leader placed on each but walking into the last, and `-overworld-quest-screen <main|subquests|subquest:n|finished>[,<seconds>]` opens the quest screen on that page;
`-overworld-book <title|number>[,<page>[,<seconds>]]` shows that book (0 Colesium to 4 Poetry, in folder order) at the spread holding that page (1 first), styled as its pickup's script opens it (or as N does for the Colesium);
`-overworld-weather <snowing|raining|clear|leaves|dust>[,<seconds>]` sets the weather (`-overworld-seed` also fixes the scatter of the weather and clouds);
`-overworld-battle <seconds>[,<seconds>]` presses End after that many seconds (entering battle mode) and, given a second number, again that much later (leaving it), e.g. with `-overworld-vitals "0:ap=30;2:ap=45"` to show heroes recharging beside one Ready; `-overworld-ring <steps>[,choose][,<seconds>]` turns the open ring that many entries (negative left) after that many seconds (default 1) and, with `choose`, chooses the top one (with `-overworld-move-to` the leader then walks its move distance);
a scripted run never takes the real mouse.

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
- Clock and light, vitals, rest, pickups, inventory, scripts, quests, books, weather, battle mode
- The ring menu, HUD and screens, gamepad, audio, options, defeat
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
