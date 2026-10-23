---
codex: 1
project: Unity.Overworld
layer: research
status: living
updated: 2026-10-23
---

# Overworld — reverse-engineering record

This is the REA of the original, the ground truth for what it did. `docs/BIBLE.md` is the brief for what the
rebuild will do. Everything below was read from the originals (never edited, never to be edited):

- **Source copy** `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\overhead 2` — the Visual Basic .NET / managed DirectX 9
  project (`Engine.vbproj`, 53 compiled `.vb` files, 13,848 lines, plus 6 uncompiled `.vb` files), with its
  `bin\` and `obj\` build output.
- **Built copy** `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\Overworld` — the shipped folder: `Engine.exe`, the DirectX
  assemblies, `data\` (books and `world.mdb`), `gfx\`, `sfx\`, and `world.mdb` / `hero.mdb` at its root (895 files).

Section 1 records how the two copies relate; the rest takes their union. File and routine names are given so a
later session can re-check the original. The in-game name is "Overworld"; the source calls the project "Engine"
(`RootNamespace Engine`, `Engine.sln`), the registry key "Software\MindAttic\Overhead", and the window
"\*\*\* UNDER DEVELOPMENT \*\*\*".

World units: one area tile is one unit wide and one unit tall. A hero (`heroSize`) is 0.144 units. Positions below are
in world units, x to the right, y up, z toward the camera being negative. "Per frame" means once per
`oneFrameRender`, which the original did not throttle (see section 18).

## 1. The two copies: where and why they split

### 1.1 What was compared

Every file of both trees was compared by content (not just name), plus the ZIP archive inside the source copy
(`overhead 2\Overworld.zip`, 32 MB, 980 entries = 895 files and 85 directories, dated 2017-01-23) and the pre-upgrade backup (`overhead 2\backup\Backup`).

| Comparison | Result |
|---|---|
| `Overworld\*` against `overhead 2\bin\*` | **Byte-identical, file for file (895 files)**, except `overhead 2\bin\video.cfg` (73 bytes, 2008-10-26), which `Overworld` lacks. |
| `Overworld.zip` extracted against `Overworld\*` | **Byte-identical, all 895 files.** The archive is a 2017 snapshot of the built folder. |
| `backup\Backup\*.vb` against the live `.vb` files | **Byte-identical** for all 53 `.vb` files the backup has (the compiled set). The backup holds the project in Visual Studio .NET 2003 format (`Engine.sln` format 7.00, `RelPath` project items); the live copy is the Visual Basic 2008 Express upgrade (`Engine.sln` format 10.00, `Engine.vbproj` ToolsVersion 3.5). |
| `Engine.exe` (Jun 26 2006, 237,568 bytes) against `obj\Release\Engine.exe` (Jun 13 2006, 229,376 bytes) | They differ. The Debug build (`bin`, `obj\Debug`) is the later, larger one; **`Overworld\Engine.exe` is the Debug build, the same bytes as `bin\Engine.exe`.** |
| Every procedure, module, class, structure and enum declared in the compiled sources against the identifiers inside `Engine.exe` | **Every one is present in the binary** (the only three misses are the module name `Gui`, `New`, and the enum `tod`, which the compiler renames or elides). The binary contains no routine absent from the source. |
| Debug symbols (`Engine.pdb`) | Lists the compiled files (`Time.vb`, `D3DFont.vb`, `Text.vb` = `Gui\Text.vb`, `Cursor.vb` = `Cursor\Cursor.vb`, and so on). It does **not** list `Hero.vb`, `Gui.vb`, `Lights.vb` or `Terrain.vb`, and none of the unique identifiers of the other two orphans are in the exe. |

### 1.2 Conclusions

1. **There is no behavioural split between the source and the build.** The built copy is the `bin\` output folder of the
   source project, lifted out whole (with the data files the engine reads) and later zipped. The source tree's `.vb`
   files (last written 2006-06-12 and 2006-06-13) were compiled into the 2006-06-26 Debug exe: every declared name is in
   the binary. Where the two copies differ, it is only because one holds the code and the other holds what the code ran
   on. The rebuild is therefore written from the source (what the code does) plus the built copy's data and assets (what it
   ran on), which are the same files in both.
2. **Six `.vb` files in the source folder were never compiled** (not in `Engine.vbproj`, not in the PDB or with none of their
   unique identifiers in the exe, absent from the 2003-format backup): `Hero.vb` (an earlier `Class theHero`: a
   side-view jump/dash/halt hero with gravity), `Gui.vb` (an earlier all-in-one `Module Gui`: loading screen, talk box, menu),
   `Lights.vb` (an earlier 250-second day with Dusk; superseded by `Sun.vb`, which declares the same `Module Lights`),
   `Terrain.vb` (loads a `Terrain` table through a relation `AreaTerrain` that the compiled `Database.vb` never builds),
   the top-level `Cursor.vb` (an earlier `Module Cursor`: `camSPD` 0.015, a `cursorPos` vector), and the top-level `Text.vb` (an earlier `Module Text` with a 256-slot matrix array and a `levelText` "Level N" fly-in).
   All six are dated Feb to Mar 2006 and are the project's earlier shape.
   They are kept in this record (section 18) because the union covers every mechanic, but they describe superseded
   designs, not shipped behaviour.
3. **Two different `world.mdb` files exist, and only one is read.** `Root.vb` opens `Application.StartupPath & "\data\world.mdb"`
   (`dataPath`), so the engine reads **`data\world.mdb` (2006-02-21, 421,888 bytes)**, the same day `Database.vb` and
   `Events.vb` were written. It holds six tables: `Area`, `Event`, `Item_Textures`, `Items`, `Pickup`, `Script`.
   The file at the root, **`world.mdb` (2007-03-08, 794,624 bytes)**, is **later** than the 2006 build and than anything in
   the source, and holds a different, larger, data-driven world design: `Area`, `Area_Enemies`, `Area_Npcs`, `Area_Props`,
   `Enemy`, `Enemy_Class`, `Enemy_Images`, `Enemy_Motion`, `Enemy_State`, `Event`, `Npc`, `Props`, `Scene`, `Script`, `Terrain`.
   No compiled code reads any of those tables, and the engine could not run against this file at all (it queries `Pickup` and `Items`, which the root file lacks).
   The inference (flagged as inference): the owner kept designing the world's data in March 2007, around the first Visual
   Studio upgrade (`UpgradeLog.XML`, 2007-04-06; the second, `UpgradeLog2.XML` of 2008-10-06, produced the Visual Basic 2008 Express project), without a matching engine rewrite. This is the data and asset
   content "only one copy's engine could not use", and the rebuild takes it (section 19).
4. **`hero.mdb` (2004-10-12, 528,384 bytes)** is older than the source (Oct 2004 against Feb 2006 onward). Its code
   reference, `heroDB`, is commented out in `Root.vb`. It is the original *Overhead* project's hero database: a hero
   "Alexander", four weapons and empty armour and relic tables.
5. **`video.cfg`** (windowed 0, 640 x 480, depth 16) is read only by `loadConfig` in `frmMain.vb`, whose only call site
   is commented out. The live options are in the registry (section 2.2). It exists only in the source copy's `bin\`.
6. **Assets exceed code.** Of the 895 files, 231 are named literally (or by numbered loop) by the compiled source; the rest
   are data, books (the engine lists the `data\books` directory at start-up, so all five are live), the area tile grid
   (loaded by a computed name), and art that nothing loads: the Belmont and girl hero sets, the catman and slime enemies,
   the Magitek and woman NPCs, the tent damage frames, the extra weapons, spare area tiles, alternative cursors, and 38 sound
   files of which only two are touched by code. Section 17 classifies every folder.
7. **Date summary.** Source files: 2006-02-07 to 2006-06-13 (`AssemblyInfo.vb` 2004-10-03). Debug exe: 2006-06-26. `data\world.mdb`: 2006-02-21.
   Root `world.mdb`: 2007-03-08. VS upgrade logs: 2007-04-06 and 2008-10-06. `video.cfg`: 2008-10-26. ZIP: 2017-01-23.

## 2. Platform, start-up and main loop

### 2.1 Platform

- VB.NET (.NET 3.5 target, `OptionStrict Off`, `OptionExplicit On`), a WinForms host `frmMain` (borderless, black,
  640 x 480, `MinimumSize = MaximumSize = 640 x 480`, centred, title "\*\*\* UNDER DEVELOPMENT \*\*\*"), Managed DirectX 1.0.2911:
  Direct3D (rendering), DirectInput (keyboard, mouse, joystick), DirectSound and AudioVideoPlayback (`Audio`, `Video`).
- Everything is global state in VB `Module`s (`Root`, `theHero`, `theItems`, ...). One engine instance, one `hero()` array,
  `currHero` as the global "current player".
- Fixed resource roots from `Application.StartupPath`: `data\`, `data\books\`, `gfx\`, `sfx\`, `gfx\gui\`, `gfx\hero\`.

### 2.2 Options and first run (`Root.loadOptions`, `saveOptions`, `setRegKey`)

- Registry key `HKCU\Software\MindAttic\Overhead`, values `Width`, `Height`, `Depth`, `isWindowMode`.
- If the key cannot be read: defaults 640 x 480, depth 32, windowed; they are saved, and a message box
  "Starting for the first time . . ." + newline + "Welcome and beware!" is shown.
- Depth must be 16 (`A1R5G5B5`) or 32 (`A8R8G8B8`); otherwise an exception box. Windowed uses the form's client size;
  full screen uses the stored size, immediate presentation, and requires a verified display mode (`isDisplayModeOkay`,
  which forces 800 x 600 in its full-screen branch).
- **F12** stops the loop and sets `toggleScreen`; `frmMain` flips `isWindowMode` and restarts the engine (`GoTo theStart`).
  **Escape** quits.

### 2.3 Start-up (`frmMain.Startup`, `Engine.initDevice`)

1. `loadOptions`. Show a random loading picture `gfx\gui\load\load640_N.jpg`, N = 1 to 12 (640 x 480), as the form background.
2. Hide the system cursor, create the engine.
3. Device: hardware, `D16` depth buffer required (else exception), hardware vertex processing if supported, multithreaded.
   Render state: Z-buffer on, dither, specular, ambient white, lighting on, source alpha / inverse source alpha blending,
   linear min/mag filtering on sampler 0, clamp addressing.
4. Load order: camera, input (keyboard, joystick if any, with axis range -1000..1000), textures, props, circle, fonts,
   weapons, heroes, sound, status icons, hero-select marker, inventory, item icons, quest screen, options menu and cursor,
   examine box, ring icons, item and swap cursors, path points, team button, cube, background, cursor, books, status box,
   events (area 0), tent, clouds, walls, text, stamina/health/turn/mana bars, quests, subquests, hero screen, inventory
   button, pickups and drops, rain, quick bar, 2D cursor. Then start the game clock, queue the tutorial messages
   (`sayTutorial`), and set `bInitOkay`.
5. `initMouse` (exclusive, foreground DirectInput mouse; re-acquired on form activation). Loop `oneFrameRender` +
   `Application.DoEvents` until `bRunning` is false. Any exception ends the program after a message box.

### 2.4 Frame order (`Engine.oneFrameRender`)

Clear to opaque magenta (255,255,0,255) and depth 1.0; begin scene. If not paused: `getInput`, `updateCloud`, `updateRain`,
`checkItemsInRange`, `checkItemPickup`, `checkDroppedInRange`, `checkDroppedPickup`, `checkFlocking`, `checkQuests`,
`checkSubquests`, `checkCursorOverInventory`, `updateHero`. Then, in draw order:

| Layer | Draws |
|---|---|
| backdrop | `renderCube` (a 67 x 55 x 38 textured box with `menu.png` around the world) |
| 0 | `renderBackground` (area tiles) |
| 1 | `renderTent`, `renderPath`, `renderItem`, `renderDropItem`, `renderwall` (debug wall lines), `renderCircle` (move/attack range dots) |
| 3 | `renderObjects` (heroes, prop bottoms and weapons, depth sorted), `renderWeapon`, `renderHeroSelect` |
| 4 | `renderIcon` (status icons), `renderRingIcon`, `showItemName`, `checkCursorOverItem`, `checkCursorOverHero`, `showRestText`, `renderRain`, `renderCloud` |
| 5 GUI | `renderTime` (lighting and clock; the original `renderDate` writes nothing), `checkScripter`, `renderFPS`, `renderteamBox`, `renderinventoryBox`, `renderStatus`, `renderTurnBar`, `renderInventory`, `renderquickIcon`, `renderquickBar`, `renderExamine`, `renderBook`, `renderHealthBar`, `rendermanaBar`, `renderStaminaBar`, `renderOptionMenu`, `renderQuestScreen`, `renderheroScreen`, `renderMessages`, `renderCursor`, `rendercursor2D` |
| end | `GameOver` (death fade), `initCamera` (recalculate camera), "- PAUSE -" banner if paused, present |

Errors in the frame are swallowed (`Catch` with nothing). The pause flag has a property and a draw, but no key sets it
(the `Keys.Pause` line is commented out).

## 3. World, camera and rendering

### 3.1 Area tile grid (`Background.vb`)

- `area(10,10)`: 11 x 11 slots, row letter A to K by column number 1 to 11, texture `gfx\area\<Letter><Number>.png`, loaded at
  512 x 512 with the magenta colour key; a missing file leaves the slot at (-1000,-1000,-1000).
- Slot position `x = startX + column_index`, `y = startY + rowNum`, with `startX = -3`, `startY = 2`, `rowNum` starting at 1 and
  decreasing per row: row A is y = 3, B = 2, C = 1, D = 0, E = -1, F = -2; column 1 is x = -3. A tile is a quad spanning
  x 0 to 1 and y -0.5 to +0.5 from its position (one unit square). `worldWidth = worldHeight = 10`, which also bounds the
  random spawning of gold, clouds and weather.
- Tiles present: A1-A8, B1-B9 (+ B4z), C1-C7, D1-D7, E1-E7, F1-F6 and F8 (46 files); seven further files `base.png`, `base2x.png`,
  `bigrunescape.png`, `flowers.png`, `grass.png`, `rune.png`, `rune2x.png` are not loaded.
- Only tiles within `renderRange` of the camera are drawn: `renderRange = 1 + |cam.Z|`, tested per axis with `A_near_B`.

### 3.2 Camera (`Camera.vb`)

- Perspective: field of view 45 degrees (`PI/4`), aspect 4/3, near 0, far 100, left-handed look-at with up (0,1,0).
- Position `cam` starts (0,0,-1), looks at `target` (kept equal to cam in x and y, so it always looks straight along +Z).
- Z is clamped to [-3.5, -1] unless free camera (`F`) is on. Zoom per wheel step 0.15; mouse-zoom drag (middle button) adds
  `camSPD / 10 * (centreY - mouseY)`, `camSPD = 0.02`; a zoom cursor shows while held.
- **Centre on hero** (`CenterOnHero`): set by moving, by hero changes and by battle turns. Each frame
  `cam += camSPD * 8 * (hero position - cam)` (target point is hero x + heroSize/2 and hero y - heroSize/2 + 0.03), the
  cursor moves with it, and the flag clears when within 0.05 of (hero x + heroSize/2 + 0.02, hero y - heroSize/2 + 0.03).
- Edge scrolling and bounds: see section 5.3. Debug moves: numpad 4/6/8/2 strafe by 0.05 / 0.02, `X`/`Z` isometric
  +/-0.25 on both axes, `shakeScreen` (unused helper).

### 3.3 Draw order and depth (`Objects.vb`)

`checkDrawOrder` builds the list of every hero (3), every prop bottom (4) and every hero's weapon quad (3), sorts by Y
descending (larger Y, further away, drawn first), and `renderObjects` draws those within range with identity
rotation, translation to (X,Y,Z). Ties resolve by the first unused slot. The Z-buffer is on (everything is at z 0 except
items, rain and clouds, so layering is by draw order). Weather particles and clouds are tested in range on all three axes.

### 3.4 Props (`Prop.vb`)

Four props at (1,1), (2,1), (1.5,1) and (1, 1.064); the first three use `tree\tree_bottom_1.png` (64) and the fourth is widened
to 1.0 with `prop\structure\wall_medium.png` (245 x 64). Each also builds a "top" quad (`tree_top_1.png`, 233 x 139) but
`renderProp` is never called, so tops are never drawn.

### 3.5 Walls and collision (`Walls.vb`, `Heroes.checkWall`)

Six hard-coded segments (the `Terrain.vb` line table was dropped for this):
(1,0)-(1,1), (1,1)-(2,1), (2,1)-(2.5,2), (2.5,2)-(3,2), (3,2)-(3.5,1.5), (3.5,1.5)-(4,2). Each stores slope and intercept.
They are drawn as 1-pixel lines every frame (`renderwall`, texture `terrain.png`, so a debug overlay that is always on).

Each frame, per hero, with test point `(x + heroSize/2, y - heroSize/2 + 0.01)`:
- if the point's x lies between the segment's x ends: slope 0 (`horizontalWall`) or sloped (`angledWall`): when the point's y is within
  `2 * |dY|` of the line's y at that x, `dY` is set to +0.001 or -0.001 (push away to the side the hero is on); for angled
  walls, when within 0.03 of the line, `dX` is also zeroed;
- else if the slope is infinite (vertical): when y lies between the ends and x is within `2 * |dX|` of the wall, `dX` is set to +/-0.001.
The vertical test lives in the `Else` branch of the x test, so the first segment (1,0)-(1,1), the only vertical one, is the only
case that reaches `verticalWall`.

### 3.6 Tent (`Tent.vb`)

One tent quad 0.3 wide at (-0.7,-0.04), `gfx\tent\Tent_128.png` (128 x 128), drawn on layer 1 when in range. The six damage
frames (`Tent_dmg_1..7`) and `Tent_1.png` are never loaded.

### 3.7 Falling / recall (`checkFall`)

If a hero's Z is below 0 it rises by 0.04 per frame and the sprite is the `halt_left` pose; at or above 0 it is set to 0.
Used by `recallHero` (Z = -1) and by the Rune's `CHANGE HERO Z -1`.

## 4. Heroes

### 4.1 The party (`Engine.initHero`, `Heroes.loadHero`)

`maxHero = 2`: three heroes, all drawn with the Chrono art set, differing in name, start and a few numbers.

| | Name | Start (x, y) | Weapon | Walk rate | Walk / run speed |
|---|---|---|---|---|---|
| 0 | Chrono | (0, 0) | Fine Axe | 0.13 | 0.003 / 0.006 |
| 1 | Belmont | (0.3, -0.05) | Steel Sword | 0.1 | 0.005 / 0.01 |
| 2 | Flagg | (-0.3, 0.1) | Steel Sword | 0.13 | 0.003 / 0.006 |

Common to all three (every `loadHero` call): Health 31 / 50, Mana 12 / 20, Stamina 60 / 60, Strength 7, Agility 5, Intelligence 5,
start facing Right, mode Adventure, halt speed 0.0004; animation rates stand 0.6 s, walk 0.13, run 0.1, kneel 0.15, regen 2 s,
battle 0.1 s, draw 0.05 s. Animation frame counts passed in: walk 5, run 5, draw 5, battle 5, face/halt/kneel 0 (zero-based
upper bounds, so six frames for walk, run, draw and battle). Weight limit adds `Strength + Agility/2` per hero (9.5 each, 28.5 total).
`moveDistanceMax = Agility * 0.05` (0.25) and `moveDistance = max * Stamina / maxStamina`.

### 4.2 States

`heroAiState`: Standing, Walking, Running, Kneeling, Resting (unused), Drawing, Battle. `aHeroMode`: Adventure, Battle.
`heroStatus`: Exhausted, Fine, Angry, Poisoned. `direction`: Left, Right, Up, Down. `aBattleAction`: Ready, Moving, Recharging, Attacking.
Flags per hero: moveLeft/Right/Up/Down, PickingUp, Resting, Burdened, Kneeling, Laying, Running.

### 4.3 Per-frame update order (`updateHero`, for each hero n)

`checkStatus`, `checkWeight`, `checkAutorun`, `checkRegen`; then the state's AI (`aiStanding`, `aiKneeling`, `aiWalking`, `aiRunning`,
`aiDrawing`, `aiBattle`); `checkfollow`, `checkWall`, `checkFall`, `checkBattleSelect`; finally
`X += dX * gameSpeed`, `Y += dY * gameSpeed` (`gameSpeed` 1.0, changed by the `-` and `=` keys in steps of 0.01, floor 0).

### 4.4 Movement and animation

- **Standing**: velocity zero, frame 0, facing sprite (`stand\face_*.png`).
- **Walking / Running** (`aiWalking`, `aiRunning`): for each held direction, `dX`/`dY` is moved toward +/-`walkSPD` or `runSPD` in
  0.001 steps (it hovers within 0.001 of the target); with no direction held and the move button up, velocity is zeroed. The animation set follows
  facing: left, right, up (only when not also holding left/right), down likewise. Frame advance is time-based by the rate above.
  Bug: the Down branch of `aiWalking` compares `dY > -walkSPD` without the matching rise, and the run-up/down animations
  use the walk frame counts.
- **Kneeling** (`aiKneeling`): zero velocity, kneel sprite per facing; if `Laying`, the sleep sprite (left/down use `sleep_left`, right/up `sleep_right`)
  and the sleep-hours chooser (section 6.4).
- **Drawing** (`aiDrawing`): zero velocity, the draw animation (left/down use the left set, right/up the right set) at 0.05 s per frame; when
  the last frame is reached, the frame is set to a random battle frame and the state becomes Battle. **Battle** (`aiBattle`): zero velocity, the battle idle
  loop at 0.1 s per frame.
- Hero sprites are 64 x 64 PNGs with a magenta colour key; the quad is `heroSize` (0.144) square. Filenames
  (`Textures.loadTextures`): `chrono\walk\walk_{left,right,up,down}1..6.png`, `run\run_*1..6.png`, `stand\face_*.png`,
  `halt\halt_left|right.png`, `kneel\kneel_*.png`, `kneel\sleep_left|right.png`, `combat\draw\draw_{left,right}_1..6.png`,
  `combat\battle_{left,right}_1..6.png`. (Up/down draw and battle sets are declared but not loaded; they use the left/right sets.)

### 4.5 Follow, recall, flocking (`checkfollow`, `recallHero`, `checkFlocking`)

For every hero that is not the current one and not in battle, when team mode is on (`bTeam`, default True):
- farther than 1 unit on either axis: `recallHero` places it within +/-0.05 of the leader, Z = -1 (it falls in);
- within 1: Running, `moveSpd` 0.03; within 0.2: Walking, 0.02; within 0.05: Standing, 0;
- velocity = (leader - follower) * moveSpd; facing from the larger axis difference (the code first sets facing from dx, then overwrites from dy, then
  from dx again when |dx| is larger, so vertical wins unless horizontal is larger).
With team mode off: every non-current hero is set Kneeling and not Laying.
`checkFlocking` separates two non-current heroes (both not Standing, both alive) within 0.05 on both axes: each is nudged in x by
`|dY|/6` and in y by `|dX|/6`, in opposite directions (a sideways slide).

### 4.6 Weight (`checkWeight`)

If `inventoryWeight > WeightLimit` the (looped) hero n is Burdened, Kneeling, AI Kneeling (so heroes cannot move); the original calls this for the
global loop variable, so it applies to each hero in turn. "You are overburdened" is drawn at screen centre while the current hero is burdened.

### 4.7 Selecting heroes

`[` / `]` (and joystick buttons 9 / 10): previous / next hero (`changeHero`: current hero stops, kneel, lay cleared, `currHero` and `selectedHero` set,
camera centres, cursor snaps to the hero). Left click on a hero under the cursor (`overHero`, `actionHeroSelect`) selects it: in battle mode only if its stamina is full
(it becomes Ready and draws); in adventure mode always (others kneel if solo). The hover name is drawn in 3D text above a hero other than the current
one. `bHeroSelect` is the "choose target" mode used when using items in team mode; `selectedHero` is the target (shown by `select_hero.png`).

## 5. Input

### 5.1 Keyboard via DirectInput (`Input.getKeyboard`)

| Key | Action |
|---|---|
| Arrow keys | Move / face (also navigate inventory, options, ring menu, quest screen, book pages, hero select) |
| Right Ctrl or Space | Run (when stamina > 0); in the inventory, swap items |
| Enter | Pick up / confirm / use item / select ring entry / initiate sleep when laid |
| `\` (BackSlash) | Rest: kneel, then lay down, then stand again |
| Delete | Toggle the inventory (not in battle); with a book open it closes the book and opens the inventory |
| Backspace | Drop the current item (inventory open, adventure mode); in battle, cancels to the ring menu |
| E | Toggle examine mode (eye cursor, examine box for the item under the cursor) |
| `'` | Toggle team / solo |
| `]` / `[` | Next / previous hero |
| A | Toggle autorun (run in the facing direction while stamina remains) |
| Page Down / Page Up | Quest screen / hero screen |
| End | Toggle battle mode |
| `,` / `.` | Previous / next item in reach (pickup selection) |
| 1 to 9, 0 | Quick-bar item 1 to 10 |
| `-` / `=` | Game speed down / up by 0.01 |
| X / Z | Isometric offset +/-0.25 |
| Numpad 4 / 6 / 8 / 2 | Strafe / move the camera |
| Shift | Faster edge-scrolling |

### 5.2 Keyboard via WinForms (`frmMain.OnKeyDown`, when the scripter is closed)

| Key | Action |
|---|---|
| Esc | Quit |
| F1 / F2 / F3 / F4 | Wireframe / textures / back-face culling / FPS counter |
| F5 | Toggles `worldFilter` between Linear and Point, which nothing applies (the `setFilter(worldFilter)` calls are commented out); the later `changeDrawMode` case on the same key is unreachable |
| F7 / F8 | Start / stop looping `regen.wav` |
| F11 | Options menu |
| F12 | Windowed / full screen |
| Tab | `showMenu` (sets a flag nothing draws) |
| Q | Toggles `bQuickBar` (the later `Case Keys.Q` for enemies is unreachable) |
| G / H | Remove / add a ring-menu entry (the second `Case Keys.H` below it is unreachable) |
| F | Free camera |
| J | Joystick on / off |
| L | Message "WHAt's up, this worked!!!!" |
| P / O | Path-point mode cycle (off, show, set, show2) / clear 20 points |
| N | Next book (wrapping) |
| R | `Reset` |
| B | Toggle the book |
| U | Toggle the current hero's mode (adventure / battle) without the team logic |
| W | Weather cycle |
| ~ (OemTilde) | Open the script console; Insert opens it with the last script |

### 5.3 Mouse (`getMouse`)

A virtual 640 x 480 mouse (`mouse`) accumulates the DirectInput relative motion, clamped to 32..width-32 and 32..height-32; `delta` is the last
motion. The world cursor moves by `delta.X * 0.00172`, `-delta.Y * 0.00172` per frame. Buttons: 0 (left) = pick up / confirm / select hero; 1 (right) = move toward the cursor
(drop item when the inventory is open); 2 (middle) = zoom drag in adventure mode, "drop"/cancel in battle. Wheel: ring left/right, book page, inventory left/right,
hero select, else cycles items in reach, else zooms the camera by 0.15 per step.

Edge scrolling (`checkScroll`): when the virtual mouse is at an edge (32 px from it), and not zooming, not centring, and the hero not kneeling, the camera moves by 0.02 (x3 with Shift), allowed
while the hero is within the limits, and is clamped to the hero +0.6 / -0.45 in x and +0.35 / -0.4 in y. `checkHeroEdge` drags the camera and cursor along when the hero leaves the box
(cam x beyond hero + 0.45 or -0.3, y beyond +0.25 or -0.3). The cursor texture changes at edges (scroll arrows), to the zoom cursor while zooming, and to the eye in examine mode; it is the arrow otherwise.
The 3D cursor quad is 0.05 wide; a second, screen-space cursor (`cursor2D`, 28 px) is drawn instead whenever the pointer is over the inventory panel, the team button or the inventory button.

### 5.4 Move-by-mouse (`actionMove`)

While the right button is held (not burdened, kneeling, laying, autorunning, in a ring menu, options, inventory or quest screen, and Enter not held): the vector from the hero's
centre (x + heroSize/2, y - heroSize/3) to the cursor (cursor + (0.03, 0.01)), each axis clamped to +/-0.6; if either axis is at least 0.2, or run is held, and stamina > 0 the hero runs, else walks.
Facing: from the sign of y, overridden by the larger axis. Walking clamps each axis to 0.2. Velocity = delta * 0.02, x1.5 when within 0.1 of the cursor, zero and Standing within 0.01.
In battle mode movement works only while `BattleAction = Moving`, and `checkHeroMove` spends move distance (section 12.3).

### 5.5 Joystick

`bJoystick` starts True, set False when no game controller is found. Axis threshold 250 of +/-1000. Buttons (0 to 10, A B C X Y Z L1 R1 START L2 R2): 0 run, 9 / 10 next / last hero,
8 rest, 4 inventory, 3 pick up, 1 drop (examine is mapped to the same button but unhandled).

### 5.6 Held-key guards (`KeysUp`)

Every action has a "down" latch (`bInventoryDown`, `bUseDown`, `bDropDown`, `bTeamtoggle`, ...) so a held key acts once; released keys clear them. When no movement input remains and the
hero is not kneeling, laying, picking up, drawing or in battle, the hero returns to Standing (or to Kneeling while recharging).
Opposite arrows together (`tryingConflict`): face Down, Standing.

## 6. Time, lighting and sleep (`Sun.vb`, module `Lights`)

### 6.1 Clock

`theTime` starts `64-01-01 09:00`. Every `timeRatio * gameSpeed` seconds (1.0) one in-game minute is added. 12-hour text: `H : MM AM|PM` (with "12" at hour 0; the AM/PM flag is `Hour <= 12`, so noon shows AM).
The HUD (`StatusBox`) writes the day period name and the time at (5, 480-70) and (5, 480-57) in yellow Calisto 8; the hero screen (Page Up) writes "Dayname, Monthname D, YYYY CE" and the time and period.

### 6.2 Periods and light

| Period | Hours | Colour target (R,G,B) | Fade |
|---|---|---|---|
| Day | 9 to 17 | (255,255,255) | channels rise 1 per game minute |
| Evening | 18 to 20 | (255,128,128) | falls 1 per game minute |
| Night | 21 to 5 | (128,128,255) | R, G fall; B rises, 1 per game minute |
| Morning | 6 to 8 | (255,255,255) | rises 1 per game minute |

On the first minute of 9:00, 18:00, 21:00 and 6:00 a large message ("Day", "Evening", "Night", "Morning") is queued. The colour is applied as the material diffuse, ambient and specular, and the
device ambient is `AmbientColor` (the sleep fade, section 6.4). `Reset` sets `dayPeriod` to Morning but not the clock.

### 6.3 Calendar

Months: Frostfall, Dwelling, Thawing, Flora, Rainy Season, Swelter, Pre-Harvest, Reaping, Goldera, Yawning, Gorging, Yeardusk. Weekdays (Sunday first in the .NET enum): Solaraday, Lunaday, Terraday, Humpday,
Titanday, Pyreday, Wynday. Year suffix "CE". `renderDate` computes the names for the hero screen only.

### 6.4 Resting and sleeping

- Rest key: Standing to Kneeling; again, Laying (`Kneeling And Laying`); a third press clears Laying (it never returns to Standing by itself; moving does). "Rest for N hour(s)?" shows at screen centre while laid and kneeling.
- While laid, left/down decrease and right/up increase `sleepHours` between 1 and 24, repeating every 0.08 s.
- Enter while laid starts the sleep (`bSleeping`). `checkSleep`: the ambient channels fade down by `fadeSPD` 5 per frame to 0; once black, for **every** hero: Kneeling, Laying, health and mana up by
  `max * sleepHours * 0.125` (clamped), stamina likewise unless Poisoned; the clock advances `sleepHours` hours, period and colour recompute, `sleepHours` resets to 1, and the leader stands. Then the ambient fades back up by 5 per frame.
  (The fade-up test compares blue with 355, so it never stops testing; harmless.)
- Items `CHANGE HERO AI LAYDOWN | CHANGE TIME HOUR 22 | CHANGE INVENTORY OPEN FALSE` use the same path.

### 6.5 Death (`GameOver`) and `Reset`

Each frame: if the current hero's Health is 0 or below, `fadeOut` decreases by 1 and the ambient colour is (fadeOut,fadeOut,fadeOut); when `fadeOut` reaches 0, `Reset` runs. `Reset` re-queues the tutorial, sets
`gameSpeed` 1, `dayPeriod` Morning, camera (0,0,-1), clears every pickup's picked-up flag, empties the inventory, closes the inventory and options, and resets the render toggles (terrain and events off).
It does **not** restore health, position, gold, experience, quests, the clock or `fadeOut`, so after a death the game keeps calling `Reset` every frame while the screen stays black (defect, section 18).
`R` calls the same routine.

## 7. Vitals and status (`Heroes.checkStatus`, `checkRegen`)

- Status: if not Poisoned, Fine; stamina below 15 percent of maximum: Exhausted; health below 5 percent: Angry. Poisoned persists until cured (`CHANGE HERO STATUS FINE`).
- **Adventure mode**, every 2 s (`regenRate`) per hero: not Poisoned: Kneeling and not picking up: stamina +2 (capped); Standing: stamina +1 (cap). Poisoned: health -1 (floor 0). Running: stamina -1 (floor 0).
- **Battle mode**, every 0.18 s (`refillInterval`) while Recharging: stamina +1 and the hero kneels; at full: Ready, stands, move distance recomputed, and (if not already in the Battle state) Drawing from frame 0.
- Status icons (`StatusIcon.vb`): a 0.05 quad at (hero x + 0.045, hero y + 0.03) animating 4 frames at 0.3 s: Exhausted frames, Angry frames; **Poisoned also uses the Angry frames** (defect: the poisoned
  folder `gfx\status\Poisoned` is empty). Fine shows a blank.
- HUD (section 13) shows HP, MP and AP numbers and bars for the current hero and, when in team mode, for the other two.

## 8. Pickups, inventory, examine and the quick bar

### 8.1 Pickup records (`Items.loadItem`)

372 pickups (index 0 to 371), each a 0.05 quad (`ItemSize`) with Name, Script, Description, Effect, Weight, Type, Value, growth rate (seconds until it grows back after being picked up; 0 = never), position, texture, and an optional
different inventory icon. Types: SingleItem, GroupItem, Reusable, Equipable, UseOnPickup. Index is the key; later assignments overwrite earlier ones.

| Index | Name | Position (x, y) | Script | Effect text | Description | Wt | Type | Value | Grow s |
|---|---|---|---|---|---|---|---|---|---|
| 0 | Health Root | (0,-0.1) | CHANGE HERO HP +10 | +10 Health | Common healing root | 0.1 | Single | 12 | 12 |
| 1 | Moon Berry | (-0.4,0.1) | CHANGE HERO AI LAYDOWN \| CHANGE TIME HOUR 22 \| CHANGE INVENTORY OPEN FALSE | Induces a coma-like sleep until nightfall | Mild sedative fruit | 0.2 | Group | 40 | 12 |
| 2 | Mandrake | (0.5,-0.4) | CHANGE HERO STATUS POISONED | Poison | Very poisonous plant | 0.1 | Single | 5 | 12 |
| 3 | Crabweed | (-0.5,0.4) | CHANGE HERO HP +1 | +1 Health | Weak healing herb | 0.3 | Single | 2 | 12 |
| 4 | Violet Caper | (0.2,0.1) | CHANGE HERO AP +5 | +5 Stamina | Mild stamina restoring fern | 0.1 | Single | 10 | 12 |
| 5 | Angel Vine (Mandrake icon) | (-0.2,0.25) | CHANGE HERO HP +9999 \| CHANGE HERO AP +9999 \| CHANGE HERO MP +9999 | Full health, mana, and stamina restoration | "Truly, a gift from the Gods" -Futhark Ald'jka, Master Alchemist, 64 CE | 0.1 | Single | 200 | 100 |
| 6 to 50 | Potato (45) | field: x = -0.4 + 0.05 * col, y = -0.5 - 0.05 * row, 8 per row | CHANGE HERO HP +1 \| CHANGE HERO AP +3 | +1 Health, +3 Stamina | Common starchy food stuff | 0.1 | Single | 2 | 10 |
| 51 | Rusty Sword | (-0.55,-0.45) | CHANGE HERO WEAPON ITEM | ATK: 5 DEF: 1 DUR: 2/10 | Poorly forged iron short sword with a rusty blade | 5.3 | Equipable | 50 | 0 |
| 52 to 100 | Turnip (49) | field: x = -0.4 + 0.05 * col, y = -0.9 - 0.05 * row | CHANGE HERO HP +1 \| CHANGE HERO AP +1 | +1 Health, +1 Stamina | Common tasteless vegetable | 0.1 | Single | 1 | 10 |
| 101 | Coffee Bean | (0.4,-0.5) | CHANGE HERO AP +15 | +15 Stamina | Highly caffinated bean grown primarily in the subtropical Southlands | 0.1 | Single | 12 | 0 |
| 102 | Cheese (overwrites the "Nitro Bean" set just before: CHANGE HERO AP +200, value 120) | (-0.5,0.15) | CHANGE HERO HP +10 \| CHANGE HERO AP +5 | +10 Health, +5 Stamina | Dairy product of fermented milk | 0.2 | Single | 8 | 0 |
| 103 | Apple | (-0.4,0.3) | CHANGE HERO HP +5 \| CHANGE HERO AP +5 | +5 Health, +5 Stamina | Ripe red fruit | 0.1 | Single | 2 | 10 |
| 104 | Bramble Nut | (0,0.6) | CHANGE HERO HP -5 \| CHANGE HERO AP +15 | -5 Health, +15 Stamina | Nut encased in a sharp spiney shell | 0.1 | Single | 4 | 10 |
| 105 | Grimoire de'Alchemiste | (0.1,-0.1) | CHANGE BOOK COVER BROWN \| CHANGE BOOK FONT CORSIVA8 \| CHANGE BOOK TO ITEM \| CHANGE BOOK NUMSYS ROMAN \| CHANGE BOOK SHOW TRUE | Alchemy | Book about the essentials of alchemy -Huth Ondana, Master Alchemist, 12 CE | 3 | Reusable | 75 | 0 |
| 106 | Egg | (0.3,-0.6) | CHANGE HERO HP +2 \| CHANGE HERO AP +1 | +2 Health, +1 Stamina | Unfertilized chicken embryo | 0.1 | Single | 1 | 2 |
| 107 | Xim's Scrub | (-0.1,0.6) | CHANGE HERO STATUS FINE | Cure Poison | Plant that neutralizes toxins in body - Aldanos Xim, Herbalist, 33 CE | 0.1 | Single | 10 | 15 |
| 108 | Morningstar | (-0.5,0.3) | CHANGE HERO AI LAYDOWN \| CHANGE TIME HOUR 5 \| CHANGE INVENTORY OPEN FALSE | Induces a coma-like sleep until daybreak | Mild sedative fruit | 0.1 | Group | 40 | 10 |
| 109 | Dragon Almanac | (-0.2,-0.3) | book script, BLUE cover, CALISTO font | Dragonology | Book about dragons and their characteristics | 3 | Reusable | 25 | 0 |
| 110 | Poetry Book | (-0.22,-0.45) | book script, BROWN cover, CORSIVA8 font | Poetry | Book of assorted poems | 2.5 | Reusable | 12 | 0 |
| 111 | Rune | (0.52,-0.6) | CHANGE INVENTORY OPEN FALSE \| CHANGE HERO MANA -5 \| CHANGE HERO Z -1 \| CHANGE HERO X 0 \| CHANGE HERO Y 0 \| CHANGE CAMERA POS HERO | Teleportation | Magical rune capable of teleporting user | 2 | Reusable | 1000 | 0 |
| 112 to 200 | Gold (89) | random in the world | CHANGE HERO GOLD +5 | Currency | Legal tender used to barter for goods and services | 0 | UseOnPickup | 5 | 0 |
| 201 to 250 | Satchel (50) | random | CHANGE HERO GOLD +25 | Container | A small bag that gingles delightfully when shook | 0 | Group | 25 | 0 |
| 251 to 370 | Gold (120) | random | CHANGE HERO GOLD +1 | Currency | (as above) | 0 | UseOnPickup | 1 | 0 |
| 371 | Matt's Book | (1,0.5) | book script, BLUE cover, CALISTO | Misc | Matt's book | 3 | Reusable | 25 | 0 |

Random positions: `x = startX + worldWidth * rnd`, `y = (startY - worldHeight * rnd) + 1`, i.e. uniformly over x in [-3,7] and y in [-7,3]; the random generator is seeded from the clock, so they differ per run.
Textures (16 px, 22 x 24 for books): health_root, night_berry (Moon Berry), mandrake (Mandrake and Angel Vine), crabweed, violet_caper, plant1 (potato plant, inventory icon potato), plant2 (turnip plant, icon turnip), rusty_sword, coffee_bean
(the Nitro Bean line), cheese, apple, spineynut, book1 (Grimoire, Poetry), egg, cure_plant, day_berry (Morningstar), book2 (Almanac, Matt's), rune1, gold5, satchel, gold.
The Rusty Sword's `CHANGE HERO WEAPON ITEM` has no handler (the HERO command lacks WEAPON), so it prints "Script failed to execute."

### 8.2 Reach, selection, pick up (`checkItemsInRange`, `checkItemPickup`)

An unpicked item is in reach when within 0.05 of the hero on both axes (hero reference point: x + heroSize/2, y - heroSize/2.5; item reference: x + ItemSize/2, y - ItemSize/2). At most 13 are listed.
The selected one (`selectedItem`, cycled by `,` `.` or the wheel when any is in reach) shows a "." marker and its name in 3D text. Pressing Enter or the left button (not over the HUD) sets the hero Kneeling with `PickingUp`; in the same frame the
pickup completes: the hero's stamina drops by 5 percent of maximum if it is mid-move in battle; the item is marked picked up (and its grow timer started); UseOnPickup items run their script at once; others merge into an existing stack with the same
name (amount +1, weight added) or take a new slot (amount 1). `renderItem` checks growth each frame: a picked item with growth > 0 reappears after that many seconds. Dropped items (`dropped`, same 372 slots) behave the same, copying the inventory item.

### 8.3 Inventory (`Inventory.vb`)

- Panel texture `inventory.png` 332 x 385 (drawn 345 x 400) at (iWidth - 345 - 12, iHeight - 400 - 50); 101 icon slots of 32 px starting at the panel + (8,8), row pitch 40, **ten per row** (the wrap test is `iconCols > 32 * totalCols`, totalCols = 9). Selection moves
  with left/right (wrapping) and up/down (by 10, clamped). Stack counts are written at the icon's lower right.
- Panel text: "Weight: current / limit" (green) at the lower right, "Gold: N", the selected item's name (with " x N"), effect, description, weight and value (`$`).
- `maxInventory = 1000` records, `itemCount` in use. Swap: the run key marks the first item, a second press swaps the two records (`SwapItem`).
- **Use** (Enter or left click on the selected item, or quick-bar numbers): in team mode the hero select marker appears for every item type except GroupItem and Reusable (so the player picks who receives it, and a second Enter applies it); GroupItem and Reusable items apply at once; in solo mode `selectedHero = currHero`.
  The script runs with `HERO` meaning `selectedHero` (the last hero chosen as target, which only equals the current hero after a hero change). Non-Reusable items are consumed (amount - 1, or the slot removed and the list closed up). In battle mode a use also costs `CInt(maxStamina * 0.1)` stamina, closes the inventory and returns to the ring menu.
- **Drop** (Backspace, or right click with the inventory open, only in adventure mode): copies the item to the next `dropped` slot at hero + (0 to 0.05, 0 to 0.05) random, decrements or removes the stack and its weight.
- Clicking the left mouse on a different icon first moves the cursor there (`overInventory`); clicking the inventory button toggles the panel.
- Weight: `inventoryWeight` is the party's total; limit 28.5.

### 8.4 Examine (`ExamineBox.vb`, E key)

With examine mode on and the cursor over an unpicked pickup (within 0.03 of it), a 328 x 175 panel (`examine.png`, at (292,112)) shows the icon, name, effect, "Description:" text, "Weight:" and "Value:" (`$`).
The cursor becomes the eye (`eye.png`).

### 8.5 Quick bar (`QuickBar.vb`)

470 x 47 `quick_bar.png` at the bottom right (400 x 36 drawn), ten 40-pixel slots showing inventory items 1 to 10 and their counts; shown when `bQuickBar` is on (Q, or the Item entry of the battle ring). Number keys use inventory item N-1 if it exists.

## 9. The script language (`Script.vb`, "Ryan's Scripting Language")

### 9.1 Grammar and execution

A script is one or more commands separated by the three-character delimiter `" | "`. Text is upper-cased first. A command is four space-separated words, **Command Type Item Parameter**
(for example `CHANGE HERO HP +10`), or two words (`RUN NIGHT`). Parts are stored in module-level variables; a command that matches neither shape reuses the previous command's parts. Errors set `scriptError` and show "Script failed to execute."; exceptions show "Script failed to run."

Numeric parameters: a leading `+` or `-` is arithmetic (add); a leading `=` is assignment (the `=` is stripped, so negative values can be assigned); anything else is assignment.

### 9.2 Commands

| Command Type | Items |
|---|---|
| CHANGE HERO / HERO1 / HERO2 / HERO3 (selected hero, or heroes 0 to 2) | HP, HEALTH, LIFE (clamped to the max); MP, MANA, MAGIC; AP, STAMINA, ENERGY; X, Y, Z; FACING LEFT/RIGHT/UP/DOWN; STATUS EXHAUSTED/TIRED, ANGRY/ENRAGED, POISONED/POISON, FINE; AI FACELEFT/STANDLEFT.., WALKLEFT.., RUNLEFT.. (each also raises `bScriptRunning`), REST/SIT/KNEEL, LAY/LAYDOWN/SLEEP, STOP; GOLD, MONEY |
| CHANGE ENEMY / BADGUY | HEALTH, AI (empty stubs; any other item is an error) |
| CHANGE LIGHT | AMBIENT (either `r,g,b` or a +/- value added to all three), RED/R, GREEN/G, BLUE/B |
| CHANGE TIME | HOUR/CLOCK (assignment sets the hour, rolling to tomorrow if earlier; the arithmetic path discards its result), DAYPERIOD MORNING (5), NIGHT (22), DAY (12), DUSK/EVENING (19), NOON (12), DAWN/SUNRISE (4), SUNSET (20), LIGHT (12), DARK (22) |
| CHANGE SCRIPT | COLOR RED/GREEN/BLUE/MAGENTA/FUSCHIA (the console text colour), RUNNING ON/OFF |
| CHANGE CAMERA / CAM | X, Y, Z (also move the target), XY/XYZ/POS/POSITION HERO (centres on the current hero) |
| CHANGE INVENTORY | OPEN ON/TRUE/OFF/FALSE, CLOSE (inverse) |
| CHANGE OPTIONS | OPEN, CLOSE (same) |
| CHANGE BOOK | TO/TITLE/CURRENT ITEM (open the book named like the selected inventory item), NUM/NUMBER n, SHOW ON/OFF, NUMSYS/NUMBERING STANDARD/ROMAN/ROMANNUMERAL/ANCHIENT/NONE, COLOR/COVER BROWN/BLUE, FONT/LETTERING ARIAL/CALISTO/ARIALNARROW/AMOUNT/CORSIVA/CORSIVA8 |
| CHANGE CURSOR | POS/POSITION/XY/XYZ CENTER |
| MOVE HERO / HERO1..3 | TO x,y,z: sets the hero walking toward the point on each axis, raises `bScriptRunning`, stores the target for completion tests |
| RUN | NIGHT (executes `CHANGE TIME DAYPERIOD NIGHT`) |
| MAKE | (empty; the `Make` helper that grows a prop array at run time exists but is unused) |

Commented-out sample scripts record the intended vocabulary: `CHANGE HERO Health -10`, `CHANGE HERO NAME Ryan`, `CHANGE TIME DATE 12/25/04`, `CHANGE LIGHT AMBIENT -10`, `CHANGE LIGHT AMBIENT 0,0,0`,
`MAKE Prop ORCHARD1 0,-0.4,-.1`, `MAKE Prop STONEHENGE -1,-0.4,-.05`, `MAKE ENEMY WERELYNX 2,0,0`, `MOVE Prop 0 -1,0,0`, `CHANGE ENEMY STATE retreat`.

### 9.3 The console (`checkScripter`, `frmMain` scripter mode)

`~` opens it; the prompt `/ > text|` / `\ > text ` blinks every 0.5 s at (55,75). While it is open, game input is not read. Keys append their text; C on an empty line types "CHANGE " and again "CHANGE CAMERA "; Space, digits, punctuation
(`: | . / + = - , "` etc.) are mapped; Backspace deletes a character, Delete clears the line, Insert recalls the last script, Enter executes it (and closes the console), `~`, Esc or F11 closes it.
`bScriptRunning` (set by MOVE and the AI items) also blocks input; **`checkScripts`, which would clear it when the target is reached, is never called** (defect), so a MOVE script locks input permanently.

### 9.4 Events (`Events.vb`, `Heroes.checkEvents`)

`loadEvents(0)` loads area 0's events from `data\world.mdb` (rectangles with a script), but `checkEvents` is commented out in `updateHero` and `renderEvents` in the frame. Events never fire in the shipped build. When enabled, a hero whose centre is inside the rectangle
writes "name : scriptName" at (100,100) and runs the script each frame.

## 10. Quests (`Quests.vb`, `QuestScreen.vb`)

`anObjectiveType`: MoveTo, TalkTo, GiveTo, GetItem (only MoveTo is evaluated). A quest has Exp, text, status (Inactive, Active, Failed, Success), start/end script (all empty) and objectives. Completion is by walking: the hero point (x + heroSize/2, y - heroSize/3) within `Range` of the objective on both axes.

| Quest | Text | Exp | Objectives (x, y, range, exp, text) |
|---|---|---|---|
| 0 (active) | Farmer Brinn wants you to check the crops | 1000 | (0, 0.3, 0.1, 100, "Check North Field"); (0, -0.3, 0.1, 150, "Check South Field") |
| 1 | Check the other crops | 2000 | (0, 0, 0.1, 200, "Check Center Field") |
| 2 | Last Quest | 2500 | (-0.3, 0, 0.05, 320, "Go West, my boy!"); (0.3, 0, 0.05, 300, "go East, my boy!", end script `CHANGE TIME HOUR 18`) |
| 3 | Go westernly now | 3500 | (-5, 0, 0.1, 400, "Very Far West") |

| Subquest | Text | Exp | Status | Objective |
|---|---|---|---|---|
| 0 | Check out the weird cirle | 350 | Active | (0, -1, 0.05, 200, "Go Way south") |
| 1 | Walk up to 3 | 1500 | Active | (0, 3, 0.05, 1200, "Go very far north") |

- Each frame `checkQuests` evaluates the **current** main quest (`currQuest`) and `checkSubquests` every active subquest. A completed objective adds its Exp and runs its end script; when all objectives are complete the quest adds its Exp, runs its end script, and the next quest becomes
  Active (stays on the last one; `currQuest < maxQuest`), running its start script. (At the last quest the status is set Active again, so its quest Exp is re-awarded every frame; defect.) Experience is a counter; nothing consumes it.
- `Experience` and `Gold` are globals; gold is spent nowhere (there are no shops).
- Quest screen (Page Down; 579 x 420 `quest_screen_main|subquests|completed.png` at (20,20)): up/down switch the category (Main, Subquests, Finished), right enters the subquest list or details, left backs out. The main page shows the quest text and each objective ("~ text" open, "X text" done); the Finished page lists
  successful quests ("o text"). `showQuestPoints` draws a 3D "\*" at the position of every incomplete main-quest objective and every active subquest's objective.

## 11. Books (`Book.vb`)

- At start-up the engine lists every directory under `data\books\` (the directory name is the title) and reads every file inside as a page (page files sorted by name, `pgNN.txt`), then opens "Grimoire de'Alchemiste" in the Roman-numeral, Corsiva 8 style.
- Reading view: `book.png` / `book_brown.png` / `book_blue.png` (550 x 392, drawn 600 x 450 at (20,20)). Two pages are shown at once, left at (50,50) and right at (340,50) in the book font, black; page numbers at (170,425) and (460,425) in the "amount" font.
  Numbering: Roman (page index + 1 and + 2), standard, none, or "ancient" (blank). Left/right (or the wheel) move by two pages (clamped to the last spread). `B` toggles the book; opening an inventory book runs its script (cover, font, title = item name, Roman numbering, show). Using any other inventory item or pressing Delete closes it; the right mouse button hides it.
  `N` cycles to the next book directory in folder order.
- The page text is hand-wrapped inside the files (`StringToPage`, a 34 to 42 character wrap helper, exists but the loader does not call it).

| Book (directory) | Pages | Words | Pickup that opens it | Content |
|---|---|---|---|---|
| Colesium Vol I | 10 | 941 | none (no pickup, reachable only with N or the CHANGE BOOK script) | A gladiator tale: Kain, champion of the Empire's Coliseum |
| Dragon Almanac | 12 | 899 | 109 | Dragons as the lost link between dinosaurs and birds; the Draconis family, 127 species |
| Grimoire de'Alchemiste | 8 | 302 | 105 | Alchemy as the fusing of science with magic; Ascension; the last page is a dedication "To Anna, My best student and lover -M" |
| Matt's Book | 6 | 1 | 371 | Placeholder text "sdfsdfsdfdsfsdfdsfsdf" in the first page, the rest empty |
| Poetry Book | 16 | 1007 | 110 | A table of contents (Awakening III, One Way Thought V, Why? VII, Shifted Perspectives X, People XI, Along the coast of castaways XII) and poems (page 2 is blank) |

The text files are ASCII with a few Windows-1252 characters (curly apostrophes). They are kept verbatim as content data.

## 12. Battle mode and the ring menu

### 12.1 Entering and leaving (`End` key, `actionBattleToggle`)

Entering: the ring menu opens, the current hero's mode becomes Battle; if its stamina is full it begins Drawing, else it is Recharging. In team mode every other hero copies the mode and battle action, and each with full stamina draws, others recharge.
Leaving: the ring closes and every hero returns to Adventure, Standing, Ready. Quick bar and range circles close. (`U` flips only the current hero.) The inventory cannot be opened by the key in battle (only via the Item ring entry).

### 12.2 Turn order (`checkBattleSelect`, `NextHeroInBattle`)

No initiative: stamina is the turn gauge. When "no one is ready" (`bNoOneReady`), the first hero whose stamina is full becomes the leader: `currHero`, Drawing, camera centres, ring menu opens. `NextHeroInBattle` (End entry, or moves/uses exhausting the gauge): the current hero kneels
and recharges; control passes to the next hero in index order (wrapping) that is in Battle mode and Ready; if none, `bNoOneReady` is set and the ring closes. A left click on a Ready hero in battle hands control to that hero.

### 12.3 The ring menu (`RingMenu.vb`)

Icons around the hero at `ringRadius` 0.1 (angle 0 to 2 pi), entries: **Move (0), End (1), Skill (2), Item (3), Attack (4)** (`ringTotal` 4; G/H add or remove entries up to 7 using the Book, Coffee Bean, Rune textures as filler). The selected entry sits at the top (angle 1.57) under the 0.075 select frame.
Left/right/wheel rotate the ring by 2 pi / (count) with an eased spin (`0.1 + 0.3 * remaining`); Up pulses the ring out to radius 1 then back in. Enter or click chooses:
- **Move**: if stamina > 0, `BattleAction = Moving` and the ring closes; the player then moves with the mouse. `checkHeroMove` subtracts the distance covered per frame (|dX| or |dY| if above 0.001, else both with y halved) from `moveDistance`, mirrors it into stamina, and when it reaches 0 the hero stops, Recharges and the turn passes.
- **Item**: if the inventory has items, open it (ring closes); using an item costs 10 percent of maximum stamina.
- **End**: `NextHeroInBattle`. **Attack, Skill**: no handler.
Highlighting an entry has an effect (`checkRingEffect`): Attack shows red dots at the weapon's `Range` radius; Move shows dots at radius `moveDistance` (green; blue where an ally is within 0.08; red around the hero itself); Item opens the quick bar; Skill and End hide them. The dots (21 of them) orbit at 0.01 rad per frame.
Picking up in battle costs 5 percent of maximum stamina.

### 12.4 Weapons (`Weapons.vb`)

`aWeapon`: name, description, rating, slashing, piercing, blunt, durability, maxDurability, skill requirement, type (Sword, Axe, Spear, Blunt, Arrow), weight, range, and four 6-frame sprite arrays (left, right, draw-left, draw-right).
Two are defined (`maxWeapons` 100 reserved):

| Name | Description | Rating | Slash / Pierce / Blunt | Durability | Skill | Type | Weight | Range |
|---|---|---|---|---|---|---|---|---|
| Steel Sword | A fine sword forged from steel with a leather grip | 8 | 0.5 / 0.15 / 0 | 28 / 30 | 2.0 | Sword | 6 | 0.09 |
| Fine Axe | A fine axe for chopping | 12 | 0.4 / 0 / 0.9 | 41 / 50 | 1.0 | Axe | 10 | 0.08 |

Each hero has a weapon overlay quad at the hero's position showing the weapon frame in step with the hero's draw or battle frame (left set for Left/Down, right for Right/Up); it is blank in any other state. There is no damage code, no enemy and no attack action anywhere in the source.
The weapon sprite folders also hold `defend_left/right.png`, katana, longsword and axeold sets (unused).

## 13. HUD and screens

All HUD quads are screen-space (640 x 480 reference). `Write` uses D3DX fonts: Arial 10 bold (`fontArial`), Calisto MT 8 bold, Arial Narrow 24 and 14 bold, Small Fonts 7, Monotype Corsiva 8 italic and 12 italic, Symbol 10.

| Element | Position and size | Behaviour |
|---|---|---|
| Status box | text at (5,1); `status_box.png` is loaded and sized 96 x 48 but never drawn, so only text shows | Current hero: name (yellow brackets while in hero-select), "HP n / max", "MP", "AP"; in team mode the other two heroes at x offsets +80, +160; in battle, "NN %" recharge or "Ready!" |
| Day period and time | (5,410) and (5,423) | yellow Calisto |
| Bars | bottom left: health (y 480-14*3-2), mana (y 480-14*2-2), stamina (y 480-14-2), 75 x 14, fill clipped by percentage | `health_bar` / `health_fill`, `mana_fill`, `stamina_*` (the mana bar reuses `health_bar.png`) |
| Turn bars | 60 x 13 at (18,59), (96,59), (176,59), (300,59) | Stamina fill per hero; ready bar texture when full; current hero first, then the others if in team mode |
| Team button | (18,75) 28 x 28 | `icon_solo` / `icon_team`; click toggles |
| Inventory button | (18,104) 28 x 28 | `inventory_Open` / `inventory_Closed` |
| FPS | (iWidth-50, 4), yellow | sampled every 100 ms, shown when `bDrawFPS` |
| Messages | spawn at screen centre | Queue of up to 100; each fades 2 alpha and rises 1 px per frame; the next spawns when the previous passed 14 px below centre; used for the tutorial and day-period announcements |
| Hero screen (Page Up) | `menu.png` (256 x 256 stretched) | Date line and time line only |
| Options (F11) | (50,50) | Main: Controls, Video, Audio, Exit (the cursor ">"; Exit quits); Controls lists Move Left/Right/Up/Down key names; Video and Audio have no content |
| Quest screen, Book, Inventory, Quick bar, Examine | sections 8 to 11 | |
| Overburdened | centre | "You are overburdened" |
| Pause | centre | "- PAUSE -" (unreachable) |

Tutorial text (queued at start and after `Reset`, as fading messages, blank lines between): "hi!", "This is a tutorial.", "Move by holding Right Mouse", "Pickup items by pressing Left Mouse", "Zoom with scroll wheel", "INSERT - Toggle INVENTORY"
(the key is actually Delete), "W - Toggle WEATHER Mode", "END - Toggle BATTLE Mode", "B - Toggle BOOK Mode", "Q - Toggle QUICK Mode", "R - Reset", "PAGEDOWN - Toggle QUEST Menu", "PAGEUP - Toggle HERO Menu", "~ - Toggle Script Language Mode",
"There's more so experiment!!!", "Have fun!!!", "...thanks again!".

3D text (`GraphicsFont`, D3DFont.vb sample class, Arial 24) draws item labels, hero-name labels, quest markers and the "." anchor dots.

## 14. Weather, clouds, scenery

### 14.1 Weather (`Weather.vb`, module `Rain`)

2,501 particles (`maxRains` 2500), 0.04 wide (`RainWidth`), 16 px textures, spawned at x in [-3,7], y in [-7,3] from z = -1.5 - 0.01 * index. Mode (cycled by `W`: Snowing, Raining, Clear, FallingLeaves, DustStorm; default Snowing):

| Mode | Per-frame motion | Texture |
|---|---|---|
| Snowing | x +0.001 * rnd, y +0.001 * rnd, z +0.01 | `snow1` |
| Raining | z +0.05 | `rain1` |
| Clear | none, not drawn | |
| FallingLeaves | one random diagonal of 0.003 * rnd, z +0.01 | `snow1` (no leaf art exists) |
| DustStorm | x +0.05, z +0.05 | `dust1` |

A particle at z >= 0 respawns at z = -1.1 with a new random x and y (+1). Only particles within render range on x, y and z are drawn. `splash1..4.png` and `snow2.png` are loaded but unused.

### 14.2 Clouds (`Clouds.vb`)

401 clouds at random x in [-3,7], y in [-7,3] + 1, z = -1.1 - 0.005 * index, drifting right by 0.001 to 0.004 per frame, wrapping at the world edge with a new random y; width 0.2 to 0.8, height half of that; texture `cloud1` or `cloud2` at random (70 x 70);
the first four are the balloon (`balloon.png`, 128 x 128). `cloud3` and `cloud4` are loaded, unused.

### 14.3 Path points (`Path.vb`, P / O keys)

A debug aid: in "set" mode the hero drops a 0.02 `path.png` dot every 0.05 units of travel along its facing axis (up to 1001 dots); P cycles off / show / set / show2; O clears the last 20. Not part of play; kept as a developer tool.

## 15. Audio (`Sound.vb`)

- DirectSound device at normal cooperative level; one secondary buffer `sfx\hero\regen.wav` (F7 plays it looping at volume -1000 (-10 dB), F8 stops it).
- `Music = New Audio(sfx\music\music.mp3)` and `vid = New Video(sfx\ruby.avi)` are constructed at module initialisation; `playMusic` and `rndMusicPos` exist but **nothing ever calls them**, so no music plays, and the video is never shown.
- Volumes: music 0, effects -1000, ambient -1500 (hundredths of a decibel).
- 38 sound files ship: `sfx\music\{Battle.mp3, Music.mp3, music.wma}`, `sfx\ambient\zombies.mp3`, `sfx\ruby.avi`, `sfx\death_1..4.wav`, `defend.wav`, `footstep_1..2.wav`, `sword_hit_1..3.wav`, `sword_swing_1..7.wav`
  (duplicates of those under `hero\attack`), `sfx\hero\{charge,footstep,heavy_footstep,jump,regen}.wav`, `sfx\hero\attack\{sword_draw,sword_hit_1..3,sword_swing_1..7}.wav`. Only `regen.wav` and `music.mp3` (never played) are referenced.

## 16. Data files in full

### 16.1 `data\world.mdb` (the engine's database; 2006-02-21)

Read by `theDatabase.getArea` into a DataSet with relations AreaEvents, EventScripts, AreaPickups, PickupItems.

`Area`: 0 Foothills of Phar; 1 The Badlands.

`Event` (areaID, eventID, eventNAME, left, right, top, bottom, scriptID):

| areaID | eventID | Name | Left | Right | Top | Bottom | scriptID |
|---|---|---|---|---|---|---|---|
| 1 | 2 | Test | -1 | -0.5 | 0.5 | 0 | 2 |
| 0 | 0 | Dark Woods | 0.5 | 1 | 0.5 | 0 | 0 |
| 0 | 1 | Cinematic | 1.05 | 2 | 0.5 | 0 | 1 |

`Script` (id, name, text, rate): 2 "Name Ryan" `CHANGE HERO NAME RYAN` 0; 0 "Make Night" `CHANGE TIME DAYPERIOD NIGHT` 0; 1 "Pan Right" `CHANGE CAM X +0.001` 0.

`Item_Textures` (id, name, file): 0 Health Root `C:\tex.bmp`; 1 Moon Berry `C:\tex2.bmp`; 2 Mandrake `C:\tex3.bmp`.

`Items` (itemID, texureID, name, script, effect, description, weight, type, amount): 0 Health Root `CHANGE HERO HP +10`, "+10 HP", "Common healing root", 0.1, OneTime, 1;
1 Moon Berry `CHANGE HERO AI LAYDOWN`, effect text holds `CHANGE TIME HOUR 22 | CHANGE INVENTORY OPEN FALSE | Induces a coma-like sleep until nightfall` (the pipe fields shifted by the column split), "Mild sedative fruit", 0.2, OneTime, 1;
2 Mandrake `CHANGE HERO STATUS POISONED`, "Poison", "Very poisonous plant", 0.1, OneTime, 1.

`Pickup` (areaID, itemID, x, y, z): (0, 0, -0.2, 0.01, 0); (0, 1, 0.3, 0.4, 0); (0, 2, 0, 0, 0).

The compiled engine loads these tables and uses none of them (its pickups, scripts and events are hard-coded or disabled): they are the first version of the data-driven design, with the three starter items.

### 16.2 Root `world.mdb` (the later design; 2007-03-08) — not read by any compiled code

`Area`: 1 Grasslands; 2 Lowlands.

`Scene` (areaID, id, name, image, x, y, z, width, height): 

| Area | ID | Name | Image (under `\gfx\area\West of Town\`) | x | y | z | w | h |
|---|---|---|---|---|---|---|---|---|
| 1 | 1 | Grasslands 1 | bgLandscape2_1x1.jpg | -1 | 0 | 0 | 2 | 0.6666667 |
| 1 | 2 | Grasslands 2 | bgLandscape2_1x2.jpg | 1 | 0 | 0 | 2 | 0.6666667 |
| 1 | 3 | Grasslands 3 | bgLandscape2_1x3.jpg | 3 | 0 | 0 | 2 | 0.6666667 |
| 1 | 4 | Grasslands 4 | bgLandscape2_1x4.jpg | 5 | 0 | 0 | 2 | 0.6666667 |
| 1 | 5 | Grasslands 5 | new.png | 7 | 0 | 0 | 2 | 0.6666667 |
| 1 | 6 | Grasslands 6 | bgLandscape2_1x6.jpg | 9 | 0 | 0 | 2 | 0.6666667 |
| 1 | 7 | Grasslands 7 | bgLandscape2_1x7.jpg | 11 | 0 | 0 | 2 | 0.6666667 |
| 1 | 8 | Grasslands 8 | bgLandscape2_1x8.jpg | 13 | 0 | 0 | 2 | 0.6666667 |
| 1 | 9 | Grasslands Sky 4 | bgLandscape2_0x4.jpg | 7 | 1.333333 | 0 | 2 | 0.6666667 |
| 1 | 10 | Mountain | mountain.png | 6 | 0 | 0.03 | 3 | 1.222222 |
| 0 | 11 | Grasslands Sky 8 | bgLandscape2_0x4.jpg | 8 | 1.333333 | 0 | 2 | 0.6666667 |

(Scene 11's area 0 matches no area row; the 640 x 480 landscape images are drawn as 2 wide by 0.667 tall, i.e. 3:1. The 1x5 slot was replaced by `new.png`; `bgLandscape2_1x5.jpg` and `.PNG` also exist.)

`Terrain` (areaID, id, x1, y1, x2, y2), all area 1: 1 (-1,-0.3)-(5.5,-0.3); 2 (5.5,-0.3)-(6,-0.5); 3 (6,-0.5)-(8.5,-0.5); 4 (8.5,-0.5)-(10,-0.2); 5 (10,-0.2)-(11,-0.2); 6 (11,-0.2)-(12,-0.43); 7 (12,-0.43)-(16,-0.43); 8 (0.25,-0.25)-(1,0.05); 9 (1,0.05)-(1.25,0.05).
(The orphan `Terrain.vb` and the unused `getIntersectY` describe how they were meant to be used: the ground line a hero stands on.)

`Props` (id, name, image, width px, height px): 1 to 7 Orchard Tree 1 to 7 (`\gfx\prop\tree\orchard_N.png`: 360x193, 306x112, 481x117, 237x214, 268x185, 213x248, 218x177); 8 Stonehenge (`\gfx\prop\structure\stonehenge.png`, 521x124); 9 Gateway (`\gfx\prop\structure\gate.png`, 147x102).

`Area_Props` (area 1; prop, x, y, z, width, height): (1, 1, -0.35, -0.05, 1, 0.25); (2, 2.5, -0.45, -0.05, 1, 0.15); (3, 4, -0.45, -0.07, 1.5, 0.15); (4, 5.6, -0.3, -0.06, 0.7, 0.32); (5, 7, -0.25, -0.06, 1, 0.3); (6, 9.2, -0.25, -0.05, 0.8, 0.4);
(7, 11, -0.4, -0.04, 0.9, 0.3); (8, -0.7, -0.5, -0.03, 1.4, 0.18); (1, 7.5, -0.3, -0.05, 1.4, 0.35); (5, 10, -0.45, -0.07, 1.2, 0.3); (6, 14, -0.36, -0.04, 1, 0.3); (9, 3, -0.3, -0.5, 0.5, 0.12); (3, 6.2, -0.45, -0.07, 1.5, 0.15);
(4, 10.5, -0.45, -0.04, 0.7, 0.32); (9, 5, -0.4, -0.05, 0.5, 0.12).

`Npc` (id, name, width, height, art): 1 Peasent Woman 0.144 x 0.072 `\gfx\npc\woman\`; 2 Magitek Armor 0.144 x 0.072 `\gfx\npc\magitek\`. `Area_Npcs` (area 1): npc 1 at (1,-0.2) state "stand"; npc 2 at (4,-0.2) state "pursue".

`Enemy` (id, name, max HP, move speed, range, state, motion, class): 1 Werelynx 6, 0.004, 0.8, Defensive, Walking, Animal; 2 Botherfly 3, 0.006, 1.4, Aggressive, Flying, Insect; 3 Blue Slime 2, 0.003, 0.4, Passive, Walking, Paranormal;
4 Red Slime 5, 0.004, 0.7, Defensive, Walking, Paranormal; 5 Toadie 4, 0.05, 0.4, Aggressive, Hopping, Reptile. `Enemy_Class`: Paranormal, Animal, Reptile, Insect, Mechanical. `Enemy_Motion`: Walking, Flying, Hopping, Teleporting, Stationary, Swimming.
`Enemy_State`: Passive, Aggressive, Afraid, Defensive. `Area_Enemies` and `Enemy_Images`: empty tables (their columns: enemy id, x, y, z; and stand/walk/attack/hurt left/right image names).

`Event` (area, id, name, x, y, left, right, top, bottom, scriptID): (1, 1, Poison Box, 3, 0, -0.25, 0.25, -0.1, -0.45, 1); (1, 2, Healing Box, 2, 0, -0.25, 0.25, -0.1, -0.45, 2); (1, 3, Entering Dark Woods, 4, 0, -0.25, 0, -0.1, -0.45, 3).
`Script`: 1 Poison `CHANGE HERO HP -1` rate 0.5; 2 Heal `CHANGE HERO HP +1` 0.5; 3 Night `CHANGE TIME DAYPERIOD Night` 0.

### 16.3 `hero.mdb` (2004-10-12)

`Hero`: id 1, Alexander, HP 50, max HP 50, STR 1, AGI 2, INT 0, SPD 2, weapon 1, relic 1.
`Weapon` (id, name, power, random, class, durability, max durability, directory): 4 Axe 4, 3, 2, 24, 0 `\gfx\weapon\axe\`; 1 Steel Sword 1, 1, 1, 10, 10 `\gfx\weapon\steelsword\`; 2 Katana 2, 1, 1, 13, 0 `\gfx\weapon\katana\`; 3 Broad Sword 3, 2, 1, 24, 0 `\gfx\weapon\broadsword\`.
Empty tables: `Armor` (hero, head, body, arms, feet ids), `Armor_Head|Body|Arms|Feet` (id, name, defence, random, durability, max durability, description), `Relic` (id, name, description, STR/INT/SPD/AGI modifiers), `Weapon_class` (id, name).

### 16.4 Settings that were data in the registry

`Software\MindAttic\Overhead`: Width, Height, Depth, isWindowMode. `video.cfg`: Windowed? 0, Screen Width? 640, Screen Height? 480, Color Depth? 16 (the unused legacy form).

## 17. Asset inventory (895 files in `Overworld`)

Counts are files. "Loaded" means a compiled routine opens it. Where a file is in the compiled loader's numbered range but absent on disk, it is noted.

| Folder | Files | Disposition |
|---|---|---|
| `Engine.exe`, `Engine.pdb`, `Engine.vshost.exe`(+manifest), 10 DLLs | 14 | Reference only (the original binary and its DirectX/VB assemblies); the rebuild replaces them |
| `world.mdb`, `hero.mdb`, `data\world.mdb` | 3 | Section 16 |
| `data\books\*` | 52 | Five books, pages as in section 11; all loaded |
| `gfx\area\*.png` (A1..F8, B4z, base, base2x, bigrunescape, flowers, grass, rune, rune2x) | 53 | 45 tiles loaded by computed name (A1-A8, B1-B9, C1-C7, D1-D7, E1-E7, F1-F6, F8); `B4z` and the seven `base` / `flowers` / `grass` / `rune` files are not loaded |
| `gfx\area\West of Town\*` | 14 | Used by the root `world.mdb` Scene table (not by code); `mountain.JPG`, `mountain_long.PNG` (1280 x 480) and two duplicate 1x5 files are spare. `gfx\area\New Folder` is empty |
| `gfx\clouds\cloud1..4` | 4 | 1 and 2 used; 3 and 4 loaded, unused |
| `gfx\balloon.png` | 1 | Used (first four clouds) |
| `gfx\cursor\*` | 19 | arrow, box, cross, retreat, group, blank, 4 scroll arrows, zoom, hand, item_cursor, swap_cursor, eye loaded; arrow2, arrow3, hand2, scroll_downright unused |
| `gfx\enemy\catman\*` | 20 | Unused in code; Werelynx art (48 x 48: stand, walk x4, attack x4, hurt, each left and right) |
| `gfx\enemy\slime\*`, `slime_black|blue|red|yellow.png` | 10 | Unused in code; slime frames (slime_1, slime_2, attack x2, dead, hurt) and four colour strips (96 x 48) |
| `gfx\gui\*` | 39 + 12 | status_box, menu, inventory, examine, quest_screen_main/subquests/completed, path, select_hero, book/book_brown/book_blue, icon_solo/team, inventory_Open/Closed, ring_select, event, terrain, quick_bar, health/mana/stamina/ready bars and fills, red/green/blue dot loaded; unused: `Copy of inventory`, `ap_bar`, `inventory2`, `inventory_2`, `load.png`, `menu.jpg`, `quest_screen.png`, `select_hero2`, `talkbox.png`; `gui\load\load640_1..12` (12 loading pictures) loaded |
| `gfx\hero\Chrono\*` | 131 | 84 loaded: stand 4, walk 24, run 24, halt 2, kneel 4 + sleep 2, draw 12, battle 12; 47 spare: `idle` 12, `jump` 2, `fall&jump` 4, combat `attack` 14, `walk_battle_*` 12, `hurt_*` 2, `chrono_run.PNG` |
| `gfx\hero\belmont\*` | 133 | Unused (side-view Belmont: stand, walk, run, halt, jump, doublejump, dash, kneel, kneelattack, attack sword and shoot, numbered frames) |
| `gfx\hero\girl\*` | 47 | Unused (halt 7, jump 2, run 12, stand 2, walk 12 + `a` 6 and `backup` 6 variants) |
| `gfx\hero\reticle*.png`, `talk_bubble*.png` | 5 | Unused (the orphan `Gui.vb` talk box) |
| `gfx\items\*` | 23 | All 23 loaded (apple, book1, book2, cheese, coffee_bean, crabweed, cure_plant, day_berry, egg, gold, gold5, health_root, mandrake, night_berry, plant1, plant2, potato, rune1, rusty_sword, satchel, spineynut, turnip, violet_caper) |
| `gfx\npc\magitek\*`, `gfx\npc\woman\*` | 22 | Unused in code; front, left, right, walk left x4, walk right x4 (48 x 48) |
| `gfx\prop\*` | 27 | tree_bottom_1, tree_top_1, wall_medium loaded; orchard_1..7, stonehenge, gate (structure) used only by the root `world.mdb`; bellfry, court, fgDryad, fgDryad2, fgRain, gate, magitek, magitek2, torch, tree, vase, willow, tree1, tree_bottom_01, tree_top_01 spare |
| `gfx\ring\*` | 6 | End, Attack, Skill, Item, Move loaded; `ring_Magic` unused |
| `gfx\status\{Angry,Exhausted}\*` | 8 | Loaded; `Poisoned` is empty |
| `gfx\tent\*` | 9 | Tent_128 loaded; `Tent_1` and the seven damage frames unused |
| `gfx\weapon\*` | 190 | steelsword and axe sets loaded (24 files each: 6 left, 6 right, 6 draw-left, 6 draw-right frames); katana, longsword, broadsword, axeold, dev sets, `stat_*.png` and `defend_*` unused |
| `gfx\weather\*` | 8 | snow1, rain1, dust1 used; snow2, splash1..4 loaded unused |
| `gfx` root: `blank.png`, `grey_box.png`, `map.png` (1024 x 1024), `shadow.png`, `small_shadow.png`, `sword.png`, `template.png` | 7 | blank loaded; the rest unused (a world map, hero shadows, a 48 x 48 sprite template) |
| `sfx\*` | 38 | Section 15 |

Sprite sizes: hero and weapon frames 64 x 64; NPC, enemy and template 48 x 48; items 16 x 16 (books 22 x 24); status icons 32 x 32 (loaded at 16 x 16); cursors 16 to 20; area tiles 512 x 512; scene strips 640 x 480.
Textures are loaded through `TextureLoader.FromFile` with the loader's requested width and height (so they are resampled to that size) and the colour key `0xFFFF00FF` (magenta transparent).

## 18. Orphans, dead code and defects (what the rebuild must decide)

Not shipped (section 1): the top-level `Cursor.vb` (superseded `Module Cursor`), `Hero.vb` (side-view jump/dash hero: jump max 0.008, gravity 0.0003, dash speed 0.01, run acceleration, double jump; its `checkEvents` ran every frame), `Gui.vb` (loading screen, talk box, menu, FPS, status toggle), `Lights.vb` (250-second day with Day/Dusk/Night/Morning
by timers), `Terrain.vb` (terrain line loader), top-level `Text.vb` (`levelText` "Level N" fly-in), plus the Belmont assets, which belong to the Hero.vb design.

Compiled but unreachable or unfinished: events and event rendering; `renderProp` and prop tops; `playMusic`, `rndMusicPos`, `Video`; `Pause`; `showMenu`; `loadConfig`; `Cutscene` (a record with name, title, start/end script and objective; `loadCutscenes` sets one name "Hello"); `Magic` (spell types Regeneration, Destruction, Alteration, Conjuration, Alchemy; one spell "Flame", "Causes a small fire to appear at a given point", cost 5, range 0.2, duration 10 s; comments sketch Cure, Heal Light Wounds,
Blizzard, Hurricane, Lightning, Meteor, Fast Walk, Water Walk, Summoning, necromancy, potions and enchantment); ring entries Skill and Attack; `enemyToggle`/`bEnemy`; `CHANGE ENEMY`; the Subquest script command (commented); TalkTo, GiveTo and GetItem objectives; `Equipable` items (the Rusty Sword's script fails); the `Resting` AI state; the weapon types Spear, Blunt and Arrow; wireframe, back-face culling and filter debug keys; `Make`.

Defects (the BIBLE says what the rebuild does about each): frame-rate-dependent movement, camera, weather and clouds (everything but animation and clock is per frame; speed doubles on a faster machine); the death soft-lock (6.5); `checkScripts` never called (9.3); the Poisoned icon using Angry frames (7); the Nitro Bean overwritten by Cheese (8.1); the Rusty Sword unusable; the Down-walk and run-animation frame counts (4.4); the clock's noon shown as AM; the mana assignment clamp (assignments clamp mana to its current value); `Experience` re-awarded every frame at the last quest (10); `getRndSingle` returns a plain 0-1 random ignoring its bounds;
`CHANGE TIME HOUR +N` discarded; `Tab`, `Q` handled twice; Roman numerals repeat wrong values at 65 to 69 and 74 to 80 ("LV" for 65, "LXIV" for 74); `isDisplayModeOkay` uses an unassigned manager variable and forces 800 x 600; unthrottled loop (no vertical sync wait in windowed mode) with a 1-second-resolution message fade.

## 19. The union and the decisions it needs

The rebuild takes the union of the two copies and of the code's era and the data's era:

1. **Behaviour** comes from the compiled source (sections 2 to 15), the shipped game.
2. **Content the shipped game used**: the 372 pickups, 4 quests and 2 subquests, 2 weapons, 3 heroes, 6 walls, 4 props, tent, 5 books, 45 area tiles, all the loaded art and the loaded audio.
3. **Content the data and assets carry beyond the code** (root `world.mdb`, `hero.mdb`, unreferenced art and sound): the Grasslands and Lowlands areas with their scenes, terrain, props, NPCs, enemies and events; Alexander and the four weapons; the Belmont and girl sprite sets; the enemy and NPC sprite sets;
   the unused sounds; Colesium Vol I; the older database's three starter items, events and scripts. These are built as the later layers of the build order, flagged in the BIBLE as decisions beyond the original wherever the original gave data but no behaviour (combat, enemy AI, NPC behaviour, equipment).
4. **Dead and superseded code** (orphans, section 18) is not rebuilt as behaviour, but every identifier and number in it is recorded here so the owner can promote any of it later.
5. **Defects** are listed and the BIBLE chooses, one by one, to keep, fix or replace.

## 20. Build order (the stories)

Every story is in the ticket database under epic A (`tickets story list --repo Unity.Overworld`), generated into `docs/USER_STORIES.md`. Build them in this order; each depends on the ones before it.

| # | Story | Builds | Section |
|---|---|---|---|
| 1 | U.OVW-US-A1 | Core / Runtime / Editor / Tests foundations, 60 Hz step, command and event spine | 2, 19 |
| 2 | U.OVW-US-A2 | All data files and tables as validated data | 8, 10, 16 |
| 3 | U.OVW-US-A3 | Asset catalogue of all 895 files and its diff test | 17 |
| 4 | U.OVW-US-A4 | World grid, perspective camera, depth-sorted drawing, culling | 3 |
| 5 | U.OVW-US-A5 | Hero locomotion and animation | 4, 5 |
| 6 | U.OVW-US-A6 | World cursor, edge scroll, camera centring, zoom, free camera | 3, 5 |
| 7 | U.OVW-US-A7 | Walls and falling | 3.5, 3.7 |
| 8 | U.OVW-US-A8 | Party, follow, flocking, hero selection, team/solo | 4 |
| 9 | U.OVW-US-A9 | Clock, periods, lighting, calendar | 6 |
| 10 | U.OVW-US-A10 | Vitals, regeneration, status effects and icons | 7 |
| 11 | U.OVW-US-A11 | Rest and sleep | 6.4 |
| 12 | U.OVW-US-A12 | Pickups, labels, reach and cycling, regrowth, examine | 8 |
| 13 | U.OVW-US-A13 | Inventory, weight, swap, drop, use, quick bar | 8 |
| 14 | U.OVW-US-A14 | Script language and console | 9 |
| 15 | U.OVW-US-A15 | Quests, subquests, quest screen, markers | 10 |
| 16 | U.OVW-US-A16 | Books and reader | 11 |
| 17 | U.OVW-US-A17 | Weather, clouds, tent, props | 14, 3 |
| 18 | U.OVW-US-A18 | Battle mode: stances, weapons, turn gauge | 12.1, 12.2, 12.4 |
| 19 | U.OVW-US-A19 | Ring menu, move budget, circles, turn passing | 12.3 |
| 20 | U.OVW-US-A20 | HUD, messages, tutorial, options, hero screen, loading screens | 13 |
| 21 | U.OVW-US-A21 | Gamepad | 5.5 |
| 22 | U.OVW-US-A22 | Audio | 15 |
| 23 | U.OVW-US-A23 | Persistent options, windowed / full screen | 2.2 |
| 24 | U.OVW-US-A24 | Defeat and reset | 6.5 |
| 25 | U.OVW-US-A25 | Later layer: areas, scenes, terrain, props from the root world.mdb | 16.2 |
| 26 | U.OVW-US-A26 | Later layer: area events and scripts | 9.4, 16 |
| 27 | U.OVW-US-A27 | Later layer: NPCs | 16.2 |
| 28 | U.OVW-US-A28 | Later layer: enemies | 16.2 |
| 29 | U.OVW-US-A29 | Later layer: combat and weapons | 12.4, 16.3 |
| 30 | U.OVW-US-A30 | Later layer: hero database, equipment, hero screen | 16.3 |
| 31 | U.OVW-US-A31 | Later layer: Belmont and girl sprite packs | 17 |
| 32 | U.OVW-US-A32 | Defined seams for the unfinished hooks (magic, cutscenes, extra motions and states, textures table) | 18 |
| 33 | U.OVW-US-A33 | End-to-end verified run and screenshots | all |
