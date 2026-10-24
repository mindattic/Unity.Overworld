# Exports every user table of an Access .mdb to a JSON file the game reads, so the game never needs Access.
# Needs the Microsoft ACE OLEDB provider (installed with Microsoft Access / the Access Database Engine):
#   .\tools\export-mdb.ps1 -Mdb <file.mdb> -Out <file.json> [-Note "where it came from"]
# Output shape: { "_source": ..., "tables": { "Name": { "columns": [ {"name","type"} ], "rows": [ {col: value} ] } } }
# One row per line so a diff or a validation error line number points at the row. Values are written as the
# database holds them: numbers as numbers (singles in their shortest round-trip text), text as strings, null as null.
param(
    [Parameter(Mandatory)][string]$Mdb,
    [Parameter(Mandatory)][string]$Out,
    [string]$Note = ''
)
$ErrorActionPreference = 'Stop'
$inv = [System.Globalization.CultureInfo]::InvariantCulture

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

function SingleText([single]$v) {
    foreach ($digits in 'G6', 'G7', 'G8') {
        $text = $v.ToString($digits, $inv)
        if ([single]::Parse($text, $inv) -eq $v) { return $text }
    }
    return $v.ToString('G9', $inv)
}

function Value($v) {
    if ($v -is [System.DBNull] -or $null -eq $v) { return 'null' }
    if ($v -is [bool]) { if ($v) { return 'true' } else { return 'false' } }
    if ($v -is [single]) { return SingleText $v }
    if ($v -is [double]) { return $v.ToString('R', $inv) }
    if ($v -is [decimal]) { return $v.ToString($inv) }
    if ($v -is [int16] -or $v -is [int32] -or $v -is [int64] -or $v -is [byte]) { return ([int64]$v).ToString($inv) }
    if ($v -is [datetime]) { return Quote $v.ToString('s') }
    if ($v -is [byte[]]) { return Quote ([Convert]::ToBase64String($v)) }
    return Quote ([string]$v)
}

$full = (Resolve-Path -LiteralPath $Mdb).Path
$conn = $null
foreach ($provider in 'Microsoft.ACE.OLEDB.16.0', 'Microsoft.ACE.OLEDB.12.0') {
    try {
        $c = New-Object System.Data.OleDb.OleDbConnection("Provider=$provider;Data Source=$full;Mode=Read")
        $c.Open(); $conn = $c; break
    } catch { }
}
if (-not $conn) { throw 'No ACE OLEDB provider could open the database (install the Microsoft Access Database Engine of the same bitness as this PowerShell).' }

$tables = $conn.GetSchema('Tables') | Where-Object { $_.TABLE_TYPE -eq 'TABLE' } | ForEach-Object { $_.TABLE_NAME } | Sort-Object
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('{')
$lines.Add('  "_source": ' + (Quote ("$Note [$([IO.Path]::GetFileName($full)), written " + (Get-Item -LiteralPath $full).LastWriteTime.ToString('yyyy-MM-dd') + "]")) + ',')
$lines.Add('  "tables": {')
$tIndex = 0
foreach ($t in $tables) {
    $tIndex++
    $cmd = $conn.CreateCommand(); $cmd.CommandText = "SELECT * FROM [$t]"
    $tbl = New-Object System.Data.DataTable
    (New-Object System.Data.OleDb.OleDbDataAdapter($cmd)).Fill($tbl) | Out-Null
    $cols = @($tbl.Columns | ForEach-Object { '{"name": ' + (Quote $_.ColumnName) + ', "type": ' + (Quote $_.DataType.Name) + '}' })
    $lines.Add('    ' + (Quote $t) + ': {')
    $lines.Add('      "columns": [' + ($cols -join ', ') + '],')
    $lines.Add('      "rows": [')
    $r = 0
    foreach ($row in $tbl.Rows) {
        $r++
        $cells = @(foreach ($col in $tbl.Columns) { (Quote $col.ColumnName) + ': ' + (Value $row[$col]) })
        $lines.Add('        {' + ($cells -join ', ') + '}' + $(if ($r -lt $tbl.Rows.Count) { ',' } else { '' }))
    }
    $lines.Add('      ]')
    $lines.Add('    }' + $(if ($tIndex -lt $tables.Count) { ',' } else { '' }))
}
$lines.Add('  }')
$lines.Add('}')
$conn.Close()
$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
[IO.File]::WriteAllText($Out, ($lines -join "`n") + "`n", (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Exported $($tables.Count) tables from $full to $Out"
