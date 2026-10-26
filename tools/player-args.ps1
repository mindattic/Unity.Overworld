# Quoting for the command line play.ps1 hands to Start-Process. Windows PowerShell 5.1 joins -ArgumentList elements with
# spaces and quotes none of them, so an argument holding a space (-overworld-script "go north", -overworld-book
# 'Dragon Almanac,5') would reach the player as several. Dot-source this file; play.ps1 and the Pester test do.

function ConvertTo-PlayerArgument {
    <# One argument, quoted by the rules the player's CommandLineToArgvW-style parser undoes: wrapped in double quotes when
       empty or holding whitespace or a quote; each embedded quote escaped as \"; backslashes before a quote (or before the
       closing quote) doubled. #>
    param([AllowNull()][string]$Argument)
    if ($null -eq $Argument) { $Argument = '' }
    if ($Argument -ne '' -and $Argument -notmatch '[\s"]') { return $Argument }
    $escaped = [regex]::Replace($Argument, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function Join-PlayerArguments {
    <# The whole command line: every argument quoted, joined by single spaces. Pass the result as one -ArgumentList string. #>
    param([string[]]$Arguments)
    return (@($Arguments | ForEach-Object { ConvertTo-PlayerArgument $_ }) -join ' ')
}
