param([string]$UnityMonoRoot = $env:UNITY_MONO_ROOT)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($UnityMonoRoot)) {
    $UnityMonoRoot = Join-Path $env:ProgramFiles 'Unity/Hub/Editor/6000.3.24f1/Editor/Data/MonoBleedingEdge'
}
$projectRoot = Split-Path $PSScriptRoot -Parent
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('capstone-focus-tests-' + [Guid]::NewGuid().ToString('N'))
$testBinary = Join-Path $testDirectory 'focus-state-tests.exe'
$mono = Join-Path $UnityMonoRoot 'bin/mono.exe'
$compiler = Join-Path $UnityMonoRoot 'lib/mono/4.5/mcs.exe'
$sources = @('Assets/Scripts/Runtime/LocalGardenState.cs', 'Assets/Scripts/Runtime/FocusMissionCatalog.cs', 'Assets/Scripts/Runtime/FocusDrawingData.cs', 'Assets/Scripts/Runtime/GardenPlantAppearance.cs')
$sources += @('Catalog', 'Math', 'Recipe', 'Generator', 'Geometry') | ForEach-Object { 'Assets/Scripts/ProceduralPlants/Plant' + $_ + '.cs' }
$sources += 'tests/FocusMissionStateTests.cs'
$sources = $sources | ForEach-Object { Join-Path $projectRoot $_ }
New-Item -ItemType Directory -Path $testDirectory | Out-Null
try {
    & $mono $compiler -langversion:latest -r:System.Xml.dll -r:System.Core.dll "-out:$testBinary" @sources
    if ($LASTEXITCODE -ne 0) { throw 'Focus mission test compilation failed.' }
    & $mono $testBinary
    if ($LASTEXITCODE -ne 0) { throw 'Focus mission tests failed.' }
}
finally {
    if (Test-Path -LiteralPath $testBinary) { Remove-Item -LiteralPath $testBinary }
    Remove-Item -LiteralPath $testDirectory
}
