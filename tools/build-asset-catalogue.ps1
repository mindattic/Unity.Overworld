# Regenerates docs/asset-catalogue.tsv from the original built folder (read-only), RECONSTRUCTION section 17.
# Usage: .\tools\build-asset-catalogue.ps1 [-Source <Overworld folder>]
# Dispositions: used (imported and used by the shipped game), union (imported and used by the union layer: content the
# data or assets carry beyond the code), not-shipped (recorded, deliberately not shipped).
param([string]$Source = 'R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\Overworld')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $Source).Path.TrimEnd('\')

# First match wins. Patterns run against the lower-case relative path with forward slashes.
$rules = @(
  @('^(engine\.|microsoft\.)', 'not-shipped', 'Original binary or DirectX/VB assembly; the rebuild replaces it (reference only)'),
  @('^data/world\.mdb$', 'used', 'Database the shipped game read (section 16)'),
  @('^(world|hero)\.mdb$', 'union', 'Older database beyond the code (section 16, union layer)'),
  @('^data/books/', 'used', 'Book page, loaded (section 11)'),
  @('^gfx/area/west of town/new\.png$', 'not-shipped', 'All zero bytes; an intact copy is in Fish 2'),
  @('^gfx/area/west of town/(mountain\.jpg|mountain_long\.png|bglandscape2_1x5\.png)$', 'not-shipped', 'Spare or duplicate scene strip'),
  @('^gfx/area/west of town/', 'union', 'Scene strip used by the root world.mdb Scene table'),
  @('^gfx/area/(b4z|base|base2x|bigrunescape|flowers|grass|rune|rune2x)\.png$', 'not-shipped', 'Area file never loaded'),
  @('^gfx/area/[a-f]\d\.png$', 'used', 'Area tile loaded by computed name'),
  @('^gfx/clouds/', 'used', 'Cloud, loaded'),
  @('^gfx/balloon\.png$', 'used', 'Balloon, used'),
  @('^gfx/cursor/(arrow2|arrow3|hand2|scroll_downright)\.png$', 'not-shipped', 'Cursor never loaded'),
  @('^gfx/cursor/', 'used', 'Cursor, loaded'),
  @('^gfx/enemy/', 'union', 'Enemy art unused in code, built in the enemy layer'),
  @('^gfx/gui/load/', 'used', 'Loading picture, loaded'),
  @('^gfx/gui/(copy of inventory|ap_bar|inventory2|inventory_2|load|menu|quest_screen|select_hero2|talkbox)\.(png|jpg)$', 'not-shipped', 'GUI file never loaded'),
  @('^gfx/gui/', 'used', 'GUI art, loaded'),
  @('^gfx/hero/chrono/(idle|jump|fall&jump)/', 'not-shipped', 'Spare side-view frame (Hero.vb design)'),
  @('^gfx/hero/chrono/chrono_run\.png$', 'not-shipped', 'Spare side-view frame (Hero.vb design)'),
  @('^gfx/hero/chrono/combat/(attack/|walk_battle_|hurt_)', 'union', 'Combat frame beyond the code, built in the combat layer'),
  @('^gfx/hero/chrono/', 'used', 'Hero frame, loaded'),
  @('^gfx/hero/(belmont|girl)/', 'union', 'Alternate hero sprite set, union layer'),
  @('^gfx/hero/(reticle|talk_bubble)', 'not-shipped', 'Orphan Gui.vb art'),
  @('^gfx/items/', 'used', 'Item icon, loaded'),
  @('^gfx/npc/', 'union', 'NPC art unused in code, built in the NPC layer'),
  @('^gfx/prop/(tree/(tree_bottom_1|tree_top_1)|structure/wall_medium)\.png$', 'used', 'Prop, loaded'),
  @('^gfx/prop/(tree/orchard_\d|structure/stonehenge|structure/gate)', 'union', 'Prop used only by the root world.mdb'),
  @('^gfx/prop/', 'not-shipped', 'Spare prop art'),
  @('^gfx/ring/ring_magic\.png$', 'not-shipped', 'Ring entry never loaded'),
  @('^gfx/ring/', 'used', 'Ring menu art, loaded'),
  @('^gfx/status/', 'used', 'Status icon, loaded'),
  @('^gfx/tent/tent_128\.png$', 'used', 'Tent, loaded'),
  @('^gfx/tent/', 'not-shipped', 'Tent frame never loaded'),
  @('^gfx/weapon/(steelsword|axe)/(weapon_|draw/)', 'used', 'Weapon frame, loaded'),
  @('^gfx/weapon/(katana|broadsword)/(weapon_|draw/|defend_)', 'union', 'Weapon set of hero.mdb, union layer'),
  @('^gfx/weapon/(steelsword|axe)/defend_', 'union', 'Defend frame beyond the code'),
  @('^gfx/weapon/stat_', 'union', 'Weapon status picture beyond the code'),
  @('^gfx/weapon/', 'not-shipped', 'Superseded or development weapon frames'),
  @('^gfx/weather/(snow1|rain1|dust1)\.png$', 'used', 'Weather, used'),
  @('^gfx/weather/', 'not-shipped', 'Weather frame loaded but never drawn'),
  @('^gfx/blank\.png$', 'used', 'Blank, loaded'),
  @('^gfx/[^/]+\.png$', 'not-shipped', 'Root gfx file never loaded'),
  @('^sfx/music/music\.mp3$', 'used', 'Music object constructed at start (never played in the original)'),
  @('^sfx/hero/regen\.wav$', 'used', 'Regeneration sound, loaded'),
  @('^sfx/ruby\.avi$', 'not-shipped', 'Video constructed but never shown'),
  @('^sfx/music/music\.wma$', 'not-shipped', 'Legacy WMA music; the MP3 ships'),
  @('^sfx/', 'union', 'Sound beyond the code, built in the audio layer')
)

$rows = foreach ($f in Get-ChildItem -LiteralPath $root -Recurse -File -Force) {
  $rel = $f.FullName.Substring($root.Length + 1).Replace('\', '/')
  $key = $rel.ToLowerInvariant()
  $hit = $rules | Where-Object { $key -match $_[0] } | Select-Object -First 1
  if (-not $hit) { throw "No disposition rule for $rel" }
  $disposition = $hit[1]; $note = $hit[2]
  if ($key -match '^sfx/(sword_(hit|swing)_\d\.wav)$') {
    # Decided by content, not name: only a byte-identical copy of the sfx/hero/attack file is a duplicate.
    $twin = Join-Path $root ('sfx' + [IO.Path]::DirectorySeparatorChar + 'hero' + [IO.Path]::DirectorySeparatorChar + 'attack' + [IO.Path]::DirectorySeparatorChar + $f.Name)
    if ((Test-Path -LiteralPath $twin) -and (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $twin -Algorithm SHA256).Hash) {
      $disposition = 'not-shipped'; $note = 'Byte-identical duplicate of the file under sfx/hero/attack'
    } else { $disposition = 'union'; $note = 'Differs from the sfx/hero/attack file of the same name, so not a duplicate; built in the audio layer' }
  }
  [pscustomobject]@{ Path = $rel; Bytes = $f.Length; Disposition = $disposition; Note = $note }
}
$lines = @('path' + "`t" + 'bytes' + "`t" + 'disposition' + "`t" + 'note')
$lines += $rows | Sort-Object { $_.Path } -CaseSensitive | ForEach-Object { "$($_.Path)`t$($_.Bytes)`t$($_.Disposition)`t$($_.Note)" }
$out = Join-Path $PSScriptRoot '..\docs\asset-catalogue.tsv'
[System.IO.File]::WriteAllText($out, (($lines -join "`n") + "`n"), (New-Object System.Text.UTF8Encoding($false)))
"{0} files catalogued -> {1}" -f $rows.Count, (Resolve-Path $out)
