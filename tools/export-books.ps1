# Exports the original's books (data\books\<title>\pgNN.txt) to one JSON file the game reads.
#   .\tools\export-books.ps1 -Books <...\Overworld\data\books> -Out <books.json>
# The page files are ASCII with a few UTF-8 curly apostrophes (U+2019), decoded strictly as UTF-8 and kept
# verbatim (the hand-wrapped line breaks are real "\r\n" in the files and stay in the text). Books keep the folder
# order the original listed them in (Directory.GetDirectories, alphabetical), pages the sorted file-name order.
param(
    [Parameter(Mandatory)][string]$Books,
    [Parameter(Mandatory)][string]$Out
)
$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false, $true)  # throws on an invalid byte rather than guessing

function Quote([string]$s) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('"')
    foreach ($c in $s.ToCharArray()) {
        $code = [int]$c
        if ($code -eq 34) { [void]$sb.Append('\"') }
        elseif ($code -eq 92) { [void]$sb.Append('\\') }
        elseif ($code -eq 10) { [void]$sb.Append('\n') }
        elseif ($code -eq 13) { [void]$sb.Append('\r') }
        elseif ($code -eq 9) { [void]$sb.Append('\t') }
        elseif ($code -lt 32 -or $code -gt 126) { [void]$sb.Append(('\u{0:x4}' -f $code)) }
        else { [void]$sb.Append($c) }
    }
    [void]$sb.Append('"')
    $sb.ToString()
}

$root = (Resolve-Path -LiteralPath $Books).Path
$dirs = Get-ChildItem -LiteralPath $root -Directory | Sort-Object { $_.Name } -Culture '' -CaseSensitive:$false
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('{')
$lines.Add('  "_source": "Overworld/data/books, one directory per book (the directory name is the title), one pgNN.txt per page, decoded as UTF-8; RECONSTRUCTION 11.",')
$lines.Add('  "books": [')
$bi = 0
foreach ($d in $dirs) {
    $bi++
    $files = Get-ChildItem -LiteralPath $d.FullName -File | Sort-Object Name
    $lines.Add('    {"title": ' + (Quote $d.Name) + ', "pages": [')
    $pi = 0
    foreach ($f in $files) {
        $pi++
        $text = $utf8.GetString([IO.File]::ReadAllBytes($f.FullName))
        $lines.Add('      ' + (Quote $text) + $(if ($pi -lt $files.Count) { ',' } else { '' }))
    }
    $lines.Add('    ]}' + $(if ($bi -lt $dirs.Count) { ',' } else { '' }))
}
$lines.Add('  ]')
$lines.Add('}')
$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
[IO.File]::WriteAllText($Out, ($lines -join "`n") + "`n", (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Exported $($dirs.Count) books to $Out"
