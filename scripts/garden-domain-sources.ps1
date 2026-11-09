# Shared pure-C# source set for state/mission/plant contracts. No Unity or live data.
param([Parameter(Mandatory=$true)][string]$ProjectRoot)
foreach ($relative in Get-Content (Join-Path $PSScriptRoot 'garden-domain-sources.txt')) {
    if ([string]::IsNullOrWhiteSpace($relative)) { continue }
    $source = Join-Path $ProjectRoot $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing domain source: $relative" }
    $source
}
