# Pester (5+) check for tools/player-args.ps1: a spaced argument must reach a child process intact when launched
# the way play.ps1 launches the player (one Start-Process -ArgumentList string). Run: Invoke-Pester tools/tests
BeforeAll {
. (Join-Path $PSScriptRoot '../player-args.ps1')

function Get-ChildArguments([string[]]$Arguments) {
    $echo = Join-Path ([IO.Path]::GetTempPath()) ('echo-args-' + [guid]::NewGuid().ToString('N') + '.ps1')
    $out = $echo + '.json'
    Set-Content -Path $echo -Value '$args | ConvertTo-Json -Compress | Set-Content -Path $env:ECHO_ARGS_OUT -Encoding UTF8'
    $env:ECHO_ARGS_OUT = $out
    try {
        $commandLine = '-NoProfile -ExecutionPolicy Bypass -File ' + (ConvertTo-PlayerArgument $echo) + ' ' + (Join-PlayerArguments $Arguments)
        Start-Process -FilePath powershell.exe -ArgumentList $commandLine -Wait -WindowStyle Hidden
        $json = Get-Content -Raw $out
        $parsed = ConvertFrom-Json $json   # 5.1 returns the array as one object; flatten it
        return , @($parsed | ForEach-Object { $_ })
    } finally {
        Remove-Item $echo, $out -ErrorAction SilentlyContinue
        Remove-Item Env:ECHO_ARGS_OUT -ErrorAction SilentlyContinue
    }
}
}

Describe 'ConvertTo-PlayerArgument' {
    It 'leaves a plain argument alone' { ConvertTo-PlayerArgument '-muted' | Should -Be '-muted' }
    It 'quotes an argument holding a space' { ConvertTo-PlayerArgument 'Dragon Almanac,5' | Should -Be '"Dragon Almanac,5"' }
    It 'quotes an empty argument' { ConvertTo-PlayerArgument '' | Should -Be '""' }
    It 'escapes embedded quotes' { ConvertTo-PlayerArgument 'say "hi" now' | Should -Be '"say \"hi\" now"' }
    It 'doubles a trailing backslash inside quotes' { ConvertTo-PlayerArgument 'C:\my dir\' | Should -Be '"C:\my dir\\"' }
}

Describe 'spaced arguments reach the child intact' {
    It 'passes each argument whole' {
        $given = @('-overworld-book', 'Dragon Almanac,5', '-overworld-script', 'go north; open "the door"', 'C:\my dir\', 'plain')
        $got = Get-ChildArguments $given
        $got.Count | Should -Be $given.Count
        for ($i = 0; $i -lt $given.Count; $i++) { $got[$i] | Should -Be $given[$i] }
    }
}
