param(
    [string]$RunId = ('memo-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')),
    [string]$EvidenceGroup = 'mission-coins-memos-20261107',
    [string]$UnityExe = (Join-Path $env:ProgramFiles 'Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe'),
    [int]$TimeoutSeconds = 600,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
if ($RunId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$' -or $EvidenceGroup -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw 'Use simple unique artifact directory names.' }
$projectRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot ('artifacts/' + $EvidenceGroup)))
$runRoot = [IO.Path]::GetFullPath((Join-Path $artifactRoot $RunId))
if (-not $runRoot.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe output path.' }
if (Test-Path -LiteralPath $runRoot) { throw 'Each Play run requires a fresh copied project.' }
if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) { throw 'Unity executable is missing.' }
$isolated = Join-Path $runRoot 'project'
$playOutput = Join-Path $runRoot 'renders'
$reportPath = Join-Path $playOutput 'report.json'
$logPath = Join-Path $runRoot 'unity.log'
$sourceFiles = @()
New-Item -ItemType Directory -Path (Join-Path $isolated 'Assets/Editor'), (Join-Path $isolated 'Packages'), (Join-Path $isolated 'ProjectSettings') -Force | Out-Null
function Copy-Source([string]$Relative, [string]$Destination) {
    $source = Join-Path $projectRoot $Relative
    Copy-Item -LiteralPath $source -Destination $Destination -Recurse
    $script:sourceFiles += Get-ChildItem -LiteralPath $source -File -Recurse | ForEach-Object {
        [ordered]@{ path = $_.FullName.Substring($projectRoot.Length + 1); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
}
# Copy product scene dependencies, never Library, UserSettings, preferences, logs or saves.
# Only this explicitly owned Editor fixture is included; no request is sent to the source Editor.
foreach ($item in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets')) {
    if ($item.Name -in @('Editor', 'Editor.meta', 'Tests', 'Tests.meta')) { continue }
    Copy-Source ('Assets/' + $item.Name) (Join-Path $isolated ('Assets/' + $item.Name))
}
foreach ($file in @('GardenPlantMemoValidation.cs', 'GardenPlantMemoValidation.cs.meta')) {
    Copy-Source ('Assets/Editor/' + $file) (Join-Path $isolated ('Assets/Editor/' + $file))
}
foreach ($file in @('manifest.json', 'packages-lock.json')) { Copy-Source ('Packages/' + $file) (Join-Path $isolated ('Packages/' + $file)) }
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'ProjectSettings') -File) {
    if ($file.Name -eq 'ProjectSettings.asset') { continue }
    Copy-Source ('ProjectSettings/' + $file.Name) (Join-Path $isolated ('ProjectSettings/' + $file.Name))
}
$product = 'PlantMemos-' + $RunId
$guid = [Guid]::NewGuid().ToString('N')
$sourceSettings = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectSettings.asset') -Raw
$colorSpaceMatch = [regex]::Match($sourceSettings, '(?m)^\s*m_ActiveColorSpace:\s*(\d+)\s*$')
if (-not $colorSpaceMatch.Success) { throw 'Source color space is missing; do not silently test a different rendering configuration.' }
$sourceColorSpace = [int]$colorSpaceMatch.Groups[1].Value
# Identity exists before launch: even early Editor/Play initialization cannot select real app preferences.
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
  m_ActiveColorSpace: $sourceColorSpace
  applicationIdentifier:
    Standalone: com.capstone.synthetic.plantmemos
"@ | Set-Content -LiteralPath (Join-Path $isolated 'ProjectSettings/ProjectSettings.asset') -Encoding utf8
$before = @(Get-Process -Name Unity -ErrorAction SilentlyContinue | Select-Object Id, StartTime)
[ordered]@{ runId = $RunId; utc = [DateTime]::UtcNow.ToString('O'); isolatedProject = $isolated; unityExecutable = $UnityExe;
    launchMode = 'Hidden -batchmode -force-d3d11; no GameView/window focus'; company = 'CapstoneSyntheticValidation'; product = $product;
    preexistingUnityPids = @($before.Id); copiedSources = $sourceFiles; sourceProjectUntouched = $true; activeColorSpace = $sourceColorSpace } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot 'manifest.json') -Encoding utf8
if ($PrepareOnly) { Write-Output "Prepared isolated Play project: $isolated"; return }
$arguments = @('-batchmode', '-force-d3d11', '-screen-width', '390', '-screen-height', '844',
    '-projectPath', ('"' + $isolated + '"'), '-executeMethod', 'CapstoneDesign.EditorTools.GardenPlantMemoValidation.RunBatch',
    '-memoReport', ('"' + $reportPath + '"'), '-logFile', ('"' + $logPath + '"'))
$process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Started isolated Play Unity PID $($process.Id); preexisting Unity PIDs $($before.Id -join ', ') untouched."
$watch = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(1000)) {
    if ($watch.Elapsed.TotalSeconds -gt $TimeoutSeconds) {
        $process.Kill(); $process.WaitForExit(); throw "Only the owned isolated child was stopped after timeout; inspect $logPath"
    }
}
$exitCode = $process.ExitCode
$after = @(Get-Process -Name Unity -ErrorAction SilentlyContinue | Select-Object Id, StartTime)
$preserved = @($before | Where-Object { $id = $_.Id; $start = $_.StartTime; -not ($after | Where-Object { $_.Id -eq $id -and $_.StartTime -eq $start }) }).Count -eq 0
[ordered]@{ exitCode = $exitCode; childPid = $process.Id; seconds = [Math]::Round($watch.Elapsed.TotalSeconds, 2); preexistingProcessesRemain = $preserved } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'process-result.json') -Encoding utf8
if (-not (Test-Path -LiteralPath $reportPath)) { throw "Unity did not emit a Play report (exit $exitCode); inspect $logPath" }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
Write-Output "Plant memos $($report.passed) passed, $($report.failed) failed, $($report.assertions) assertions. Report: $reportPath"
if ($report.failed -gt 0 -or $exitCode -ne 0) { throw 'Isolated plant memo validation failed.' }

