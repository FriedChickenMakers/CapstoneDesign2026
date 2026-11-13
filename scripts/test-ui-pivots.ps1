param(
    [string]$RunId = ('batch-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')),
    [string]$UnityExe = (Join-Path $env:ProgramFiles 'Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe'),
    [int]$TimeoutSeconds = 600,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
if ($RunId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw 'RunId must be a simple unique directory name.' }
$projectRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts/ui-pivots-20261106'))
$runRoot = [IO.Path]::GetFullPath((Join-Path $artifactRoot $RunId))
if (-not $runRoot.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe output path.' }
if (Test-Path -LiteralPath $runRoot) { throw 'RunId already exists; each run must create a fresh isolated project.' }
if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) { throw 'Unity executable is missing.' }
$isolated = Join-Path $runRoot 'project'
$reportPath = Join-Path $runRoot 'report.json'
$logPath = Join-Path $runRoot 'unity.log'
$sourceFiles = @()
New-Item -ItemType Directory -Path (Join-Path $isolated 'Assets/Editor/Automation'), (Join-Path $isolated 'Assets/Resources'), (Join-Path $isolated 'Assets/Tests/Prototype/Editor'), (Join-Path $isolated 'Packages'), (Join-Path $isolated 'ProjectSettings') -Force | Out-Null
function Copy-Source([string]$Relative, [string]$Destination) {
    $source = Join-Path $projectRoot $Relative
    Copy-Item -LiteralPath $source -Destination $Destination -Recurse
    $script:sourceFiles += Get-ChildItem -LiteralPath $source -File -Recurse | ForEach-Object {
        [ordered]@{ path = $_.FullName.Substring($projectRoot.Length + 1); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
}
# Copy source/resources only. No scene, Library, user preferences, logs, save or device state.
Copy-Source 'Assets/Scripts' (Join-Path $isolated 'Assets/Scripts')
foreach ($resource in @('FocusMissions', 'Fonts')) { Copy-Source ('Assets/Resources/' + $resource) (Join-Path $isolated ('Assets/Resources/' + $resource)) }
foreach ($file in @('UiPivotValidation.cs', 'UiPivotValidation.cs.meta')) { Copy-Source ('Assets/Editor/Automation/' + $file) (Join-Path $isolated ('Assets/Editor/Automation/' + $file)) }
foreach ($file in @('GardenThemeSurfaceTests.cs', 'GardenThemeSurfaceTests.cs.meta', 'Capstone.Prototype.Tests.asmdef', 'Capstone.Prototype.Tests.asmdef.meta')) {
    Copy-Source ('Assets/Tests/Prototype/Editor/' + $file) (Join-Path $isolated ('Assets/Tests/Prototype/Editor/' + $file))
}
foreach ($file in @('manifest.json', 'packages-lock.json')) { Copy-Source ('Packages/' + $file) (Join-Path $isolated ('Packages/' + $file)) }
Copy-Source 'ProjectSettings/ProjectVersion.txt' (Join-Path $isolated 'ProjectSettings/ProjectVersion.txt')
$product = 'UiPivot-' + $RunId
$guid = [Guid]::NewGuid().ToString('N')
# Distinct identity is present before Unity starts, so PlayerPrefs/persistent paths never point at the real app.
@"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!129 &1
PlayerSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 28
  productGUID: $guid
  companyName: CapstoneSyntheticValidation
  productName: $product
  activeInputHandler: 2
  applicationIdentifier:
    Standalone: com.capstone.synthetic.uipivot
"@ | Set-Content -LiteralPath (Join-Path $isolated 'ProjectSettings/ProjectSettings.asset') -Encoding utf8
$before = @(Get-Process -Name Unity -ErrorAction SilentlyContinue | Select-Object Id, StartTime)
$manifest = [ordered]@{ runId = $RunId; utc = [DateTime]::UtcNow.ToString('O'); isolatedProject = $isolated; unityExecutable = $UnityExe;
    launchMode = 'Hidden -batchmode -nographics'; company = 'CapstoneSyntheticValidation'; product = $product;
    preexistingUnityPids = @($before.Id); copiedSources = $sourceFiles; sourceProjectUntouched = $true }
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot 'manifest.json') -Encoding utf8
if ($PrepareOnly) { Write-Output "Prepared isolated project: $isolated"; return }
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $isolated + '"'), '-executeMethod', 'CapstoneDesign.EditorTools.UiPivotValidation.RunBatch', '-pivotReport', ('"' + $reportPath + '"'), '-logFile', ('"' + $logPath + '"'))
$process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Started isolated batch Unity PID $($process.Id); preexisting Unity PIDs $($before.Id -join ', ') untouched."
$watch = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(1000)) {
    if ($watch.Elapsed.TotalSeconds -gt $TimeoutSeconds) {
        # Only the process created above belongs to this run. Never stop or interact with the original Editor.
        $process.Kill(); $process.WaitForExit(); throw "Isolated Unity timed out; inspect $logPath"
    }
}
$exitCode = $process.ExitCode
$after = @(Get-Process -Name Unity -ErrorAction SilentlyContinue | Select-Object Id, StartTime)
$preserved = @($before | Where-Object { $id = $_.Id; $start = $_.StartTime; -not ($after | Where-Object { $_.Id -eq $id -and $_.StartTime -eq $start }) }).Count -eq 0
[ordered]@{ exitCode = $exitCode; childPid = $process.Id; seconds = [Math]::Round($watch.Elapsed.TotalSeconds, 2); preexistingProcessesRemain = $preserved } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'process-result.json') -Encoding utf8
if (-not (Test-Path -LiteralPath $reportPath)) { throw "Unity did not emit a validation report (exit $exitCode); inspect $logPath" }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
Write-Output "UiPivot checks $($report.passed) passed, $($report.failed) failed; $($report.assertions) assertions. Report: $reportPath"
if ($report.failed -gt 0 -or $exitCode -ne 0) { throw 'Isolated UI pivot validation failed.' }
