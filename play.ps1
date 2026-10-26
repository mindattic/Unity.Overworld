<#
.SYNOPSIS
    Launches the Overworld player for development, muted.
.DESCRIPTION
    Starts Build/Overworld/Overworld.exe (building it headless with Unity 6000.4.3f1 first when it is missing or -Build
    is given) with MINDATTIC_CLI_MUTED=1 and -muted, so a CLI or agent launching the game never plays sound. The mute
    is Unity.Shared's session mute: it lasts for this run only and never changes the player's saved settings. Pass
    -Sound to launch with audio. -Capture <file.png> saves a screenshot after a few frames and quits (a scripted check
    that the build launches and draws the world). Any other arguments go to the player. The log is Logs/play.log.
.PARAMETER Build
    Rebuild the player before launching.
.PARAMETER Sound
    Launch with audio (no MINDATTIC_CLI_MUTED, no -muted).
.PARAMETER Capture
    Save a screenshot to this file once the world is drawn, then quit (implies -Wait).
.PARAMETER Wait
    Wait for the player to exit and return its exit code.
#>
param(
    [switch]$Build,
    [switch]$Sound,
    [string]$Capture,
    [switch]$Wait,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$PlayerArgs
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$exe = Join-Path $root 'Build/Overworld/Overworld.exe'
$log = Join-Path $root 'Logs/play.log'
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null

if ($Build -or -not (Test-Path $exe)) {
    $unity = if ($env:UNITY_EDITOR) { $env:UNITY_EDITOR } else { 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe' }
    $buildLog = Join-Path $root 'Logs/build-player.log'
    $p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList @(
        '-batchmode', '-projectPath', "`"$root`"",
        '-executeMethod', 'MindAttic.Overworld.Editor.OverworldBuild.BuildPlayer', '-logFile', "`"$buildLog`"")
    if ($p.ExitCode -ne 0 -or -not (Test-Path $exe)) { Write-Error "Player build failed; see $buildLog" }
}

$arguments = @('-logFile', "`"$log`"")
if (-not $Sound) { $arguments += '-muted' }
if ($Capture) {
    $captureFile = if ([IO.Path]::IsPathRooted($Capture)) { $Capture } else { Join-Path (Get-Location) $Capture }
    $arguments += @('-overworld-capture', "`"$([IO.Path]::GetFullPath($captureFile))`"",'-screen-width', '640', '-screen-height', '480', '-screen-fullscreen', '0')
    $Wait = $true
}
if ($PlayerArgs) { $arguments += $PlayerArgs }

# The child inherits this process's environment; set the mute only around the launch.
$previous = $env:MINDATTIC_CLI_MUTED
if ($Sound) { Remove-Item Env:MINDATTIC_CLI_MUTED -ErrorAction SilentlyContinue } else { $env:MINDATTIC_CLI_MUTED = '1' }
try {
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru
} finally {
    if ($null -eq $previous) { Remove-Item Env:MINDATTIC_CLI_MUTED -ErrorAction SilentlyContinue } else { $env:MINDATTIC_CLI_MUTED = $previous }
}

Write-Host ("Launched Overworld (pid {0}, {1}); log {2}" -f $process.Id, $(if ($Sound) { 'with sound' } else { 'muted' }), $log)
if ($Wait) {
    $process.WaitForExit()
    exit $process.ExitCode
}
exit 0
