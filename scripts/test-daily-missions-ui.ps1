param(
    [string]$RunId = ('batch-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')),
    [string]$EvidenceGroup = 'daily-missions-20261107',
    [string]$ReuseProject,
    [string]$UnityExe = (Join-Path $env:ProgramFiles 'Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe'),
    [int]$TimeoutSeconds = 600,
    [switch]$PrepareOnly,
    [switch]$Capture
)
$ErrorActionPreference = 'Stop'
if (-not $Capture) { throw 'Daily UI pointer checks require a graphics device. Use -Capture for the hidden isolated GPU run; -nographics cannot establish raycast evidence.' }
if ($RunId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw 'RunId must be a simple unique directory name.' }
if ($EvidenceGroup -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw 'EvidenceGroup must be a simple directory name beneath ignored artifacts.' }
$projectRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot ('artifacts/' + $EvidenceGroup)))
$runRoot = [IO.Path]::GetFullPath((Join-Path $artifactRoot $RunId))
if (-not $runRoot.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe output path.' }
if (Test-Path -LiteralPath $runRoot) { throw 'Each run requires a fresh evidence directory; a completed synthetic project may be reused.' }
if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) { throw 'Unity executable is missing.' }
$isolated = Join-Path $runRoot 'project'
if (-not [string]::IsNullOrWhiteSpace($ReuseProject)) {
    $isolated = [IO.Path]::GetFullPath($ReuseProject)
    $artifactBoundary = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    if (-not $isolated.StartsWith($artifactBoundary, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($isolated) -notin @('p', 'project') -or
        -not (Test-Path -LiteralPath (Join-Path $isolated 'Library') -PathType Container)) { throw 'Reuse requires a completed isolated artifacts project with its own cache.' }
    if ((Get-Item -LiteralPath $isolated).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Do not reuse a linked project directory.' }
    $existingSettings = Get-Content -LiteralPath (Join-Path $isolated 'ProjectSettings/ProjectSettings.asset') -Raw
    if ($existingSettings -notmatch '(?m)^\s*companyName:\s*CapstoneSyntheticValidation\s*$' -or
        $existingSettings -notmatch '(?m)^\s*productName:\s*(DailyMissions-[A-Za-z0-9._-]+|MemoPopup-v07)\s*$') { throw 'Reuse identity is not this synthetic validator.' }
    $active = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($isolated, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($active.Count -gt 0) { throw 'The reused project still belongs to a running Unity process.' }
}
$reportPath = Join-Path $runRoot 'report.json'
$logPath = Join-Path $runRoot 'unity.log'
$sourceFiles = @()
$baselineFiles = @()
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $isolated 'Assets/Editor/Automation'), (Join-Path $isolated 'Assets/Resources'), (Join-Path $isolated 'Assets/Tests/Prototype/Editor'), (Join-Path $isolated 'Packages'), (Join-Path $isolated 'ProjectSettings') -Force | Out-Null
function Copy-Source([string]$Relative, [string]$Destination) {
    $source = Join-Path $projectRoot $Relative
    if ([string]::IsNullOrWhiteSpace($ReuseProject)) { Copy-Item -LiteralPath $source -Destination $Destination -Recurse }
    $script:sourceFiles += Get-ChildItem -LiteralPath $source -File -Recurse | ForEach-Object {
        $relativeFile = $_.FullName.Substring($projectRoot.Length + 1)
        $sourceHash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        if (-not [string]::IsNullOrWhiteSpace($ReuseProject)) {
            $target = [IO.Path]::GetFullPath((Join-Path $isolated $relativeFile))
            if (-not $target.StartsWith($isolated + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe reused file target.' }
            $priorHash = if (Test-Path -LiteralPath $target -PathType Leaf) { (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash } else { $null }
            if ($priorHash -ne $sourceHash) {
                $baseline = Join-Path (Join-Path $runRoot 'baseline') $relativeFile
                if ($null -ne $priorHash) {
                    New-Item -ItemType Directory -Path (Split-Path $baseline -Parent) -Force | Out-Null
                    Copy-Item -LiteralPath $target -Destination $baseline
                }
                New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
                Copy-Item -LiteralPath $_.FullName -Destination $target -Force
                $script:baselineFiles += [ordered]@{ path=$relativeFile; previousSha256=$priorHash; replacementSha256=$sourceHash; backup=if($null -ne $priorHash){$baseline}else{$null} }
            }
        }
        [ordered]@{ path = $relativeFile; sha256 = $sourceHash }
    }
}
# Copy source/resources only. No scene, Library, user preferences, logs, save or device state.
Copy-Source 'Assets/Scripts' (Join-Path $isolated 'Assets/Scripts')
foreach ($resource in @('FocusMissions', 'Fonts', 'GardenAtmosphere')) { Copy-Source ('Assets/Resources/' + $resource) (Join-Path $isolated ('Assets/Resources/' + $resource)) }
foreach ($file in @('DailyMissionValidation.cs', 'DailyMissionValidation.cs.meta')) { Copy-Source ('Assets/Editor/Automation/' + $file) (Join-Path $isolated ('Assets/Editor/Automation/' + $file)) }
foreach ($file in @('GardenThemeSurfaceTests.cs', 'GardenThemeSurfaceTests.cs.meta', 'Capstone.Prototype.Tests.asmdef', 'Capstone.Prototype.Tests.asmdef.meta')) {
    Copy-Source ('Assets/Tests/Prototype/Editor/' + $file) (Join-Path $isolated ('Assets/Tests/Prototype/Editor/' + $file))
}
foreach ($file in @('manifest.json', 'packages-lock.json')) { Copy-Source ('Packages/' + $file) (Join-Path $isolated ('Packages/' + $file)) }
Copy-Source 'ProjectSettings/ProjectVersion.txt' (Join-Path $isolated 'ProjectSettings/ProjectVersion.txt')
$product = 'DailyMissions-' + $RunId
if (-not [string]::IsNullOrWhiteSpace($ReuseProject)) {
    $product = [regex]::Match($existingSettings, '(?m)^\s*productName:\s*(\S+)\s*$').Groups[1].Value
}
$guid = [Guid]::NewGuid().ToString('N')
$colorMatch = Select-String -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectSettings.asset') -Pattern '^  m_ActiveColorSpace: ([01])$'
if (-not $colorMatch) { throw 'Source project color space is missing.' }
$colorSpace = [int]$colorMatch.Matches[0].Groups[1].Value
if (-not [string]::IsNullOrWhiteSpace($ReuseProject)) {
    $reuseColor = [regex]::Match($existingSettings, '(?m)^\s*m_ActiveColorSpace:\s*([01])\s*$')
    if (-not $reuseColor.Success -or [int]$reuseColor.Groups[1].Value -ne $colorSpace) { throw 'Reused project color space differs from the source; preserve its settings and choose a matching synthetic project.' }
}
# Distinct identity is present before Unity starts, so PlayerPrefs/persistent paths never point at the real app.
if ([string]::IsNullOrWhiteSpace($ReuseProject)) { @"
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
  m_ActiveColorSpace: $colorSpace
  applicationIdentifier:
    Standalone: com.capstone.synthetic.dailymissions
"@ | Set-Content -LiteralPath (Join-Path $isolated 'ProjectSettings/ProjectSettings.asset') -Encoding utf8 }
$before = @(Get-Process -Name Unity -ErrorAction SilentlyContinue | Select-Object Id, StartTime)
$manifest = [ordered]@{ runId = $RunId; utc = [DateTime]::UtcNow.ToString('O'); isolatedProject = $isolated; unityExecutable = $UnityExe;
    launchMode = 'Hidden -batchmode'; capture = [bool]$Capture; company = 'CapstoneSyntheticValidation'; product = $product; activeColorSpace = $colorSpace;
    preexistingUnityPids = @($before.Id); copiedSources = $sourceFiles; sourceProjectUntouched = $true;
    reusedProject = (-not [string]::IsNullOrWhiteSpace($ReuseProject)); previousVersions = $baselineFiles }
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot 'manifest.json') -Encoding utf8
if ($PrepareOnly) { Write-Output "Prepared isolated project: $isolated"; return }
$arguments = @('-batchmode', '-projectPath', ('"' + $isolated + '"'), '-executeMethod', 'CapstoneDesign.EditorTools.DailyMissionValidation.RunBatch', '-dailyReport', ('"' + $reportPath + '"'), '-logFile', ('"' + $logPath + '"'))
if ($Capture) { $arguments += @('-force-d3d11', '-dailyCapture') } else { $arguments += '-nographics' }
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
$sourceMismatches = @(foreach ($source in $sourceFiles) {
    $live = Join-Path $projectRoot $source.path
    if (-not (Test-Path -LiteralPath $live -PathType Leaf) -or (Get-FileHash -LiteralPath $live -Algorithm SHA256).Hash -ne $source.sha256) {
        [pscustomobject]@{ path = $source.path; sha256 = $source.sha256 }
    }
})
[ordered]@{ checkedFiles = $sourceFiles.Count; mismatchCount = $sourceMismatches.Count; mismatches = $sourceMismatches } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'source-verification.json') -Encoding utf8
if (-not (Test-Path -LiteralPath $reportPath)) { throw "Unity did not emit a validation report (exit $exitCode); inspect $logPath" }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
Write-Output "DailyMissions checks $($report.passed) passed, $($report.failed) failed; $($report.assertions) assertions. Report: $reportPath"
if ($report.failed -gt 0 -or $exitCode -ne 0) { throw 'Isolated daily mission validation failed.' }
