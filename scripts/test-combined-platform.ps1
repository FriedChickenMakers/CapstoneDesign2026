param(
    [string]$RunId = ('native-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')),
    [string]$ToolchainRoot,
    [string]$EvidenceGroup = 'combine-garden-20261107',
    [switch]$Offline
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if ($RunId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw 'Use a simple unique run id.' }
if ([string]::IsNullOrWhiteSpace($ToolchainRoot)) { $ToolchainRoot = Join-Path $repo 'artifacts/full-reset-20261107/toolchain' }
$ToolchainRoot = [IO.Path]::GetFullPath($ToolchainRoot)
$jdk = Get-ChildItem -LiteralPath (Join-Path $ToolchainRoot 'jdk') -Directory | Select-Object -First 1
$gradle = Join-Path $ToolchainRoot 'gradle-9.1.0'
$sdk = Join-Path $ToolchainRoot 'sdk'
if ($null -eq $jdk -or !(Test-Path -LiteralPath (Join-Path $sdk 'platforms/android-36/android.jar'))) { throw 'Portable JDK 17, Gradle 9.1 and SDK 36 are required; no device or original Unity is used.' }
if ($EvidenceGroup -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw 'Use a simple evidence group.' }
$run = Join-Path $repo ('artifacts/' + $EvidenceGroup + '/' + $RunId)
if (Test-Path -LiteralPath $run) { throw 'Each validation needs a fresh artifact directory.' }
$project = Join-Path $run 'project'
New-Item -ItemType Directory -Path $project | Out-Null
$native = Join-Path $repo 'Assets/Plugins/Android/CapstonePlatform.androidlib'
Copy-Item -LiteralPath $native -Destination (Join-Path $project 'platform') -Recurse
New-Item -ItemType Directory -Path (Join-Path $project 'platform/src/test/kotlin') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'tests/android/localreset/LocalDataResetTest.kt') -Destination (Join-Path $project 'platform/src/test/kotlin/LocalDataResetTest.kt')
$addedTests=@('ActivityNotificationSettingsTest.kt','FocusRecommendationMeasurementTest.kt','CombinedPlatformResetTest.kt','AtomicFileRenameShadow.kt')
foreach($test in $addedTests) { Copy-Item -LiteralPath (Join-Path $repo ('tests/robolectric/' + $test)) -Destination (Join-Path $project ('platform/src/test/kotlin/' + $test)) }
@'
pluginManagement { repositories { google(); mavenCentral(); gradlePluginPortal() } }
dependencyResolutionManagement { repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS); repositories { google(); mavenCentral() } }
rootProject.name='SyntheticLocalResetValidation'
include ':platform'
'@ | Set-Content -LiteralPath (Join-Path $project 'settings.gradle') -Encoding utf8
"plugins { id 'com.android.library' version '9.0.0' apply false }" | Set-Content -LiteralPath (Join-Path $project 'build.gradle') -Encoding utf8
@'
org.gradle.jvmargs=-Xmx2048M
android.useAndroidX=true
android.builtInKotlin=true
org.gradle.workers.max=2
android.enableJetifier=true
'@ | Set-Content -LiteralPath (Join-Path $project 'gradle.properties') -Encoding utf8
('sdk.dir=' + $sdk.Replace('\','/')) | Set-Content -LiteralPath (Join-Path $project 'local.properties') -Encoding utf8
# The original toolchain is read-only. All mutable Gradle/Maven/SDK state is copied
# once to this worktree's ignored cache, including SDK auto-install metadata.
$cacheRoot=Join-Path $repo 'artifacts/combine-platform-toolchain'
New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
foreach($part in @('gradle-home','maven-cache','sdk')) {
    $target=Join-Path $cacheRoot $part
    if(!(Test-Path -LiteralPath $target)) { Copy-Item -LiteralPath (Join-Path $ToolchainRoot $part) -Destination $target -Recurse }
}
$sdk=Join-Path $cacheRoot 'sdk'
('sdk.dir=' + $sdk.Replace('\','/')) | Set-Content -LiteralPath (Join-Path $project 'local.properties') -Encoding utf8
$mavenCache = (Join-Path $cacheRoot 'maven-cache').Replace('\','/')
@"
android { testOptions { unitTests {
    includeAndroidResources = true
    all { systemProperty 'maven.repo.local', '$mavenCache' }
} } }
dependencies {
    testImplementation 'junit:junit:4.13.2'
    testImplementation 'org.robolectric:robolectric:4.16'
    testImplementation 'androidx.test:core:1.7.0'
    testImplementation 'androidx.work:work-testing:2.11.2'
}
"@ | Add-Content -LiteralPath (Join-Path $project 'platform/build.gradle') -Encoding utf8
$sources = @(Get-ChildItem -LiteralPath $native -File -Recurse)
$sources += Get-Item -LiteralPath (Join-Path $repo 'tests/native/LocalResetEpochTests.kt'), (Join-Path $repo 'tests/android/localreset/LocalDataResetTest.kt')
$sources += $addedTests | ForEach-Object {Get-Item -LiteralPath (Join-Path $repo ('tests/robolectric/' + $_))}
$sources += Get-Item -LiteralPath (Join-Path $repo 'tests/native/NotificationPolicyTest.kt'), (Join-Path $repo 'tests/native/MeasurementBufferTest.kt')
$sources | ForEach-Object { [ordered]@{ path=$_.FullName.Substring($repo.Length+1); sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'source-hashes.json') -Encoding utf8
$previous = @{}
foreach ($key in @('JAVA_HOME','JAVA_BIN','KOTLIN_GRADLE_LIB','GRADLE_USER_HOME')) { $previous[$key]=[Environment]::GetEnvironmentVariable($key,'Process') }
try {
    $env:JAVA_HOME=$jdk.FullName
    $env:JAVA_BIN=Join-Path $jdk.FullName 'bin/java.exe'
    $env:KOTLIN_GRADLE_LIB=Join-Path $gradle 'lib'
    $env:GRADLE_USER_HOME=Join-Path $cacheRoot 'gradle-home'
    & python (Join-Path $repo 'scripts/run-kotlin-test.py') --output (Join-Path $run 'epoch-tests.jar') --main com.capstonedesign2026.platform.LocalResetEpochTestsKt (Join-Path $native 'src/main/kotlin/com/capstonedesign2026/platform/LocalResetEpoch.kt') (Join-Path $repo 'tests/native/LocalResetEpochTests.kt') *> (Join-Path $run 'host.log')
    if ($LASTEXITCODE -ne 0) { throw 'Epoch host tests failed; see host.log.' }
    foreach($suite in @(@('NotificationPolicyTest','ActivityReminderPolicy'), @('MeasurementBufferTest','MeasurementBuffer'))) {
        $inputs=@((Join-Path $native ('src/main/kotlin/com/capstonedesign2026/platform/'+$suite[1]+'.kt')), (Join-Path $repo ('tests/native/'+$suite[0]+'.kt')))
        if($suite[0] -eq 'NotificationPolicyTest') {$inputs+=Join-Path $native 'src/main/kotlin/com/capstonedesign2026/platform/WalkingAchievementPolicy.kt'}
        & python (Join-Path $repo 'scripts/run-kotlin-test.py') --output (Join-Path $run ($suite[0]+'.jar')) --main ('com.capstonedesign2026.platform.'+$suite[0]+'Kt') @inputs *> (Join-Path $run ($suite[0]+'.log'))
        if($LASTEXITCODE -ne 0) {throw ('Pure native test failed: '+$suite[0])}
    }
    $options=@('-p',$project,'--no-daemon','--max-workers=2',':platform:testDebugUnitTest')
    if($Offline) { $options+='--offline' }
    & (Join-Path $gradle 'bin/gradle.bat') @options *> (Join-Path $run 'gradle.log')
    if ($LASTEXITCODE -ne 0) { throw 'Android compile/integration checks failed; see gradle.log.' }
    $results=@(Get-ChildItem -LiteralPath (Join-Path $project 'platform/build/test-results/testDebugUnitTest') -Filter 'TEST-*.xml' | ForEach-Object {
        [xml]$xml=Get-Content -LiteralPath $_.FullName
        [ordered]@{suite=[string]$xml.testsuite.name;tests=[int]$xml.testsuite.tests;failures=[int]$xml.testsuite.failures;errors=[int]$xml.testsuite.errors}
    })
    [ordered]@{passed=$true;reference='255fb987b4a4f56768d0d626cefff9262f74d6c9';suites=$results;externalServerStarted=$false;deviceAccess=$false;userDataAccess=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'report.json') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $run 'host.log')
    Get-Content -LiteralPath (Join-Path $run 'gradle.log') -Tail 12
    Write-Output ('Evidence: ' + $run)
}
finally {
    foreach($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key,$previous[$key],'Process') }
}
