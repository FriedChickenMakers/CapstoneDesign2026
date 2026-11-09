param([string]$UnityMonoRoot = $env:UNITY_MONO_ROOT)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($UnityMonoRoot)) {
    $UnityMonoRoot = Join-Path $env:ProgramFiles 'Unity/Hub/Editor/6000.3.24f1/Editor/Data/MonoBleedingEdge'
}
$projectRoot = Split-Path $PSScriptRoot -Parent
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('capstone-activity-journal-' + [Guid]::NewGuid().ToString('N'))
$testBinary = Join-Path $testDirectory 'activity-journal-tests.exe'
$mono = Join-Path $UnityMonoRoot 'bin/mono.exe'
$compiler = Join-Path $UnityMonoRoot 'lib/mono/4.5/mcs.exe'
$sources = @(& (Join-Path $PSScriptRoot 'garden-domain-sources.ps1') -ProjectRoot $projectRoot)
$sources += Join-Path $projectRoot 'Assets/Scripts/Runtime/ActivityJournal.cs'
$sources += Join-Path $projectRoot 'tests/ActivityJournalTests.cs'
New-Item -ItemType Directory -Path $testDirectory | Out-Null
try {
    & $mono $compiler -langversion:latest -r:System.Xml.dll -r:System.Core.dll "-out:$testBinary" @sources
    if ($LASTEXITCODE -ne 0) { throw 'Activity journal compilation failed.' }
    & $mono $testBinary
    if ($LASTEXITCODE -ne 0) { throw 'Activity journal tests failed.' }
}
finally {
    if (Test-Path -LiteralPath $testBinary) { Remove-Item -LiteralPath $testBinary }
    Remove-Item -LiteralPath $testDirectory
}
