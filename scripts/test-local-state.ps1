param([string]$UnityMonoRoot = $env:UNITY_MONO_ROOT)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($UnityMonoRoot)) {
    $UnityMonoRoot = Join-Path $env:ProgramFiles 'Unity/Hub/Editor/6000.3.24f1/Editor/Data/MonoBleedingEdge'
}
$projectRoot = Split-Path $PSScriptRoot -Parent
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('capstone-local-state-tests-' + [Guid]::NewGuid().ToString('N'))
$mono = Join-Path $UnityMonoRoot 'bin/mono.exe'
$compiler = Join-Path $UnityMonoRoot 'lib/mono/4.5/mcs.exe'
$sources = @('Assets/Scripts/Runtime/LocalGardenState.cs', 'Assets/Scripts/Runtime/FocusMissionCatalog.cs', 'Assets/Scripts/Runtime/FocusDrawingData.cs', 'Assets/Scripts/Runtime/GardenPlantAppearance.cs')
$sources += @('Assets/Scripts/Runtime/MindfulnessContent.cs', 'Assets/Scripts/Runtime/MbctContent.cs')
$sources += @('Catalog', 'Math', 'Recipe', 'Generator', 'Geometry') | ForEach-Object { 'Assets/Scripts/ProceduralPlants/Plant' + $_ + '.cs' }
$sources += 'tests/LocalGardenStateTests.cs'
$sources = $sources | ForEach-Object { Join-Path $projectRoot $_ }
New-Item -ItemType Directory -Path $testDirectory | Out-Null
try {
    foreach ($mode in @('release', 'development')) {
        $testBinary = Join-Path $testDirectory ('local-state-tests-' + $mode + '.exe')
        $compilerOptions = @('-langversion:latest', '-r:System.Xml.dll', '-r:System.Core.dll', "-out:$testBinary")
        if ($mode -eq 'development') { $compilerOptions += '-define:DEVELOPMENT_BUILD' }
        & $mono $compiler @compilerOptions @sources
        if ($LASTEXITCODE -ne 0) { throw "Local state $mode test compilation failed." }
        & $mono $testBinary
        if ($LASTEXITCODE -ne 0) { throw "Local state $mode tests failed." }
    }
}
finally {
    foreach ($mode in @('release', 'development')) {
        $testBinary = Join-Path $testDirectory ('local-state-tests-' + $mode + '.exe')
        if (Test-Path -LiteralPath $testBinary) { Remove-Item -LiteralPath $testBinary }
    }
    Remove-Item -LiteralPath $testDirectory
}
