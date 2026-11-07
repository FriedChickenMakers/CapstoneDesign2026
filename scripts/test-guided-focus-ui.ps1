param(
    [string]$RunId = ('slides-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')),
    [string]$EvidenceGroup = 'guided-slides-20261107',
    [string]$ReuseProject,
    [string]$UnityExe = (Join-Path $env:ProgramFiles 'Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe'),
    [int]$TimeoutSeconds = 600,
    [switch]$PrepareOnly,
    [switch]$Capture
)
$ErrorActionPreference = 'Stop'
if ($RunId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$' -or $EvidenceGroup -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw 'Use simple unique artifact directory names.' }
$projectRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot ('artifacts/' + $EvidenceGroup)))
$runRoot = [IO.Path]::GetFullPath((Join-Path $artifactRoot $RunId))
if (-not $runRoot.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe output path.' }
if (Test-Path -LiteralPath $runRoot) { throw 'Each run requires a fresh evidence directory; a completed synthetic project may be reused.' }
if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) { throw 'Unity executable is missing.' }
$isolated = Join-Path $runRoot 'p'
if (-not [string]::IsNullOrWhiteSpace($ReuseProject)) {
    $isolated = [IO.Path]::GetFullPath($ReuseProject)
    $artifactBoundary = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    if (-not $isolated.StartsWith($artifactBoundary, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($isolated) -notin @('p', 'project') -or
        -not (Test-Path -LiteralPath (Join-Path $isolated 'Library') -PathType Container)) { throw 'Reuse requires a completed isolated artifacts project with its own cache.' }
    if ((Get-Item -LiteralPath $isolated).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Do not reuse a linked project directory.' }
    $existingSettings = Get-Content -LiteralPath (Join-Path $isolated 'ProjectSettings/ProjectSettings.asset') -Raw
    if ($existingSettings -notmatch '(?m)^\s*companyName:\s*CapstoneSyntheticValidation\s*$' -or
        $existingSettings -notmatch '(?m)^\s*productName:\s*(GuidedFocus-[A-Za-z0-9._-]+|MemoPopup-v07)\s*$') { throw 'Reuse identity is not this synthetic validator.' }
    $active = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($isolated, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($active.Count -gt 0) { throw 'The reused project still belongs to a running Unity process.' }
}
$playOutput = Join-Path $runRoot 'renders'
$reportPath = Join-Path $playOutput 'report.json'
$logPath = Join-Path $runRoot 'unity.log'
$sourceFiles = @()
$baselineFiles = @()
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $isolated 'Assets/Editor'), (Join-Path $isolated 'Packages'), (Join-Path $isolated 'ProjectSettings') -Force | Out-Null
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
# Copy product scene dependencies, never Library, UserSettings, preferences, logs or saves.
# Only this explicitly owned Editor fixture is included; no request is sent to the source Editor.
foreach ($item in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets')) {
    if ($item.Name -in @('Editor', 'Editor.meta', 'Tests', 'Tests.meta')) { continue }
    Copy-Source ('Assets/' + $item.Name) (Join-Path $isolated ('Assets/' + $item.Name))
}
foreach ($file in @('GuidedFocusValidation.cs', 'GuidedFocusValidation.cs.meta')) {
    Copy-Source ('Assets/Editor/Automation/' + $file) (Join-Path $isolated ('Assets/Editor/Automation/' + $file))
}
foreach ($file in @('manifest.json', 'packages-lock.json')) { Copy-Source ('Packages/' + $file) (Join-Path $isolated ('Packages/' + $file)) }
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'ProjectSettings') -File) {
    if ($file.Name -eq 'ProjectSettings.asset') { continue }
    Copy-Source ('ProjectSettings/' + $file.Name) (Join-Path $isolated ('ProjectSettings/' + $file.Name))
}
$product = 'GuidedFocus-' + $RunId
if (-not [string]::IsNullOrWhiteSpace($ReuseProject)) {
    $product = [regex]::Match($existingSettings, '(?m)^\s*productName:\s*(\S+)\s*$').Groups[1].Value
}
$guid = [Guid]::NewGuid().ToString('N')
$sourceSettings = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectSettings.asset') -Raw
$colorSpaceMatch = [regex]::Match($sourceSettings, '(?m)^\s*m_ActiveColorSpace:\s*(\d+)\s*$')
if (-not $colorSpaceMatch.Success) { throw 'Source color space is missing; do not silently test a different rendering configuration.' }
$sourceColorSpace = [int]$colorSpaceMatch.Groups[1].Value
# Identity exists before launch: even early Editor/Play initialization cannot select real app preferences.
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
  m_ActiveColorSpace: $sourceColorSpace
  applicationIdentifier:
    Standalone: com.capstone.synthetic.guidedfocus
"@ | Set-Content -LiteralPath (Join-Path $isolated 'ProjectSettings/ProjectSettings.asset') -Encoding utf8 }
$before = @(Get-Process -Name Unity -ErrorAction SilentlyContinue | Select-Object Id, StartTime)
[ordered]@{ runId = $RunId; utc = [DateTime]::UtcNow.ToString('O'); isolatedProject = $isolated; unityExecutable = $UnityExe;
    launchMode = 'Hidden -batchmode; no GameView/window focus'; capture=[bool]$Capture; company = 'CapstoneSyntheticValidation'; product = $product;
    preexistingUnityPids = @($before.Id); copiedSources = $sourceFiles; sourceProjectUntouched = $true; activeColorSpace = $sourceColorSpace;
    reusedProject = (-not [string]::IsNullOrWhiteSpace($ReuseProject)); previousVersions = $baselineFiles } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot 'manifest.json') -Encoding utf8
if ($PrepareOnly) { Write-Output "Prepared isolated GPU project: $isolated"; return }
$arguments = @('-batchmode', '-screen-width', '390', '-screen-height', '844',
    '-projectPath', ('"' + $isolated + '"'), '-executeMethod', 'CapstoneDesign.EditorTools.GuidedFocusValidation.RunBatch',
    '-guidedReport', ('"' + $reportPath + '"'), '-logFile', ('"' + $logPath + '"'))
if ($Capture) { $arguments += @('-force-d3d11', '-guidedCapture') } else { $arguments += '-nographics' }
$process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Started isolated GPU Unity PID $($process.Id); preexisting Unity PIDs $($before.Id -join ', ') untouched."
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
$sourceMismatches = @(foreach ($source in $sourceFiles) {
    $live = Join-Path $projectRoot $source.path
    if (-not (Test-Path -LiteralPath $live -PathType Leaf) -or (Get-FileHash -LiteralPath $live -Algorithm SHA256).Hash -ne $source.sha256) {
        [pscustomobject]@{ path = $source.path; sha256 = $source.sha256 }
    }
})
[ordered]@{ checkedFiles = $sourceFiles.Count; mismatchCount = $sourceMismatches.Count; mismatches = $sourceMismatches } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'source-verification.json') -Encoding utf8
if (-not (Test-Path -LiteralPath $reportPath)) { throw "Unity did not emit a GPU report (exit $exitCode); inspect $logPath" }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
Write-Output "Guided slides $($report.passed) passed, $($report.failed) failed, $($report.assertions) assertions. Report: $reportPath"
if ($report.failed -gt 0 -or $exitCode -ne 0) { throw 'Isolated guided slides validation failed.' }


