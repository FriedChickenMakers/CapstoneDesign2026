param([string]$UnityMonoRoot=$env:UNITY_MONO_ROOT)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if([string]::IsNullOrWhiteSpace($UnityMonoRoot)) {
    $UnityMonoRoot=Join-Path $env:ProgramFiles 'Unity/Hub/Editor/6000.3.24f1/Editor/Data/MonoBleedingEdge'
}
& python (Join-Path $PSScriptRoot 'create-combined-branch-fixtures.py')
if($LASTEXITCODE -ne 0){throw 'Original branch fixture generation failed.'}
$output=Join-Path $projectRoot ('artifacts/branch-save-fixtures/import-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $output | Out-Null
$sources=& (Join-Path $PSScriptRoot 'garden-domain-sources.ps1') -ProjectRoot $projectRoot
$sources+=Join-Path $projectRoot 'tests/CombinedBranchFixtureTests.cs'
$binary=Join-Path $output 'combined-fixtures.exe'
$mono=Join-Path $UnityMonoRoot 'bin/mono.exe'
& $mono (Join-Path $UnityMonoRoot 'lib/mono/4.5/mcs.exe') -langversion:latest -r:System.Xml.dll -r:System.Core.dll ('-out:'+$binary) @sources
if($LASTEXITCODE -ne 0){throw 'Combined fixture import compilation failed.'}
& $mono $binary (Join-Path $projectRoot 'artifacts/branch-save-fixtures/previous/state.xml') (Join-Path $projectRoot 'artifacts/branch-save-fixtures/latest/state.xml') |
    Tee-Object -FilePath (Join-Path $output 'tests.log')
if($LASTEXITCODE -ne 0){throw 'Original-branch fixture import failed.'}
$hashes=@($sources | ForEach-Object {
    [ordered]@{path=$_.Substring($projectRoot.Length+1);sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}
})
[ordered]@{passed=$true;synthetic=$true;utc=[DateTime]::UtcNow.ToString('O');sources=$hashes} |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'report.json') -Encoding utf8
