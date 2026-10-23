# Runs the pure-C# Core tests outside Unity (no editor needed): .\run-core-tests.ps1 [-Filter <dotnet test filter>]
param([string]$Filter)
$project = Join-Path $PSScriptRoot 'Tests/Core.Tests/Core.Tests.csproj'
$dotnetArgs = @('test', $project, '--nologo')
if ($Filter) { $dotnetArgs += @('--filter', $Filter) }
& dotnet @dotnetArgs
exit $LASTEXITCODE
