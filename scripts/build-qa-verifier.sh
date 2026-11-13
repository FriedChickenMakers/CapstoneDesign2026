#!/usr/bin/env bash
# Builds a fixed-target, read-only verifier. Does not install or launch anything.
set -euo pipefail
if [[ "$(id -un)" != codex || "${HOME:-}" != /home/codex ]]; then
  printf 'Run this build as the existing codex OS user with its normal home.\n' >&2
  exit 2
fi
if [[ ! -r /home/codex/.android/debug.keystore ]]; then
  printf 'Existing codex Gradle debug signing key is required; no key will be created or copied.\n' >&2
  exit 2
fi
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="${QA_VERIFIER_OUTPUT:-$repo_root/artifacts/qa-verifier-$(date -u +%Y%m%dT%H%M%S)-$$}"
gradle_home="${UNITY_GRADLE_HOME:-/opt/unity/editors/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/Tools/gradle}"
java_bin="${JAVA_BIN:-/opt/android-jdk/bin/java}"
python3 - "$repo_root" "$output" <<'PY'
from pathlib import Path
import shutil,sys
repo,out=map(lambda p:Path(p).resolve(),sys.argv[1:])
if out.is_relative_to(repo) and not out.is_relative_to(repo/'artifacts'):
    raise SystemExit('Generated verifier output must be under ignored artifacts or outside the repository')
if out.exists() and any(out.iterdir()):
    raise SystemExit('A fresh verifier output directory is required; no stale sources are reused')
project=out/'project'
def write(name,text):
    path=project/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_text(text,encoding='utf-8')
write('settings.gradle', "pluginManagement { repositories { google(); mavenCentral(); gradlePluginPortal() } }\ndependencyResolutionManagement { repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS); repositories { google(); mavenCentral() } }\nrootProject.name='QaPrivateStateVerifier'\ninclude ':app'\n")
write('build.gradle', "plugins { id 'com.android.application' version '9.0.0' apply false }\n")
write('gradle.properties', 'org.gradle.jvmargs=-Xmx1024M\norg.gradle.workers.max=2\n')
write('app/build.gradle', '''plugins { id 'com.android.application' }
android {
 namespace 'com.capstonedesign2026.qa.verifier'
 compileSdk 36
 defaultConfig { applicationId 'com.capstonedesign2026.qa.verifier'; minSdk 29; targetSdk 36; versionCode 1; versionName 'qa-verifier-1' }
 compileOptions { sourceCompatibility JavaVersion.VERSION_17; targetCompatibility JavaVersion.VERSION_17 }
 testOptions { unitTests.includeAndroidResources = true }
}
dependencies { testImplementation 'junit:junit:4.13.2'; testImplementation 'net.sf.kxml:kxml2:2.3.0'; testImplementation 'org.robolectric:robolectric:4.17' }
''')
write('app/src/main/AndroidManifest.xml', '''<manifest xmlns:android="http://schemas.android.com/apk/res/android">
 <application android:label="QA private state verifier" android:allowBackup="false" />
 <instrumentation android:name="com.capstonedesign2026.qa.verifier.QaEnrollmentVerifier"
  android:targetPackage="com.capstonedesign2026.qa" android:functionalTest="true" android:handleProfiling="false" />
</manifest>
''')
for source in (repo/'tests/android/qaverifier').glob('*.java'):
    part='test' if source.name.endswith('Test.java') else 'main'
    target=project/f'app/src/{part}/java/com/capstonedesign2026/qa/verifier'/source.name
    target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
PY
launcher=("$gradle_home"/lib/gradle-launcher-*.jar)
"$java_bin" -classpath "${launcher[0]}" org.gradle.launcher.GradleMain \
  -p "$output/project" --no-daemon --max-workers=2 :app:testDebugUnitTest :app:assembleDebug
cp "$output/project/app/build/outputs/apk/debug/app-debug.apk" "$output/qa-verifier.apk"
python3 - "$output" <<'PY'
from pathlib import Path
import hashlib,json,subprocess,sys,xml.etree.ElementTree as ET
out=Path(sys.argv[1]);apk=out/'qa-verifier.apk'
subprocess.run(['/opt/android-sdk/build-tools/36.0.0/apksigner','verify',str(apk)],check=True,capture_output=True)
results=list((out/'project/app/build/test-results/testDebugUnitTest').glob('TEST-*.xml'))
if not results: raise SystemExit('Verifier unit test results are missing')
counts={'tests':0,'failures':0,'errors':0,'skipped':0}
for source in results:
    suite=ET.parse(source).getroot()
    for name in counts:counts[name]+=int(suite.attrib.get(name,0))
if counts['failures'] or counts['errors'] or counts['skipped']:raise SystemExit('Verifier checks did not all pass')
report={'status':'PASS','scope':'Build and synthetic host checks only; no device or network execution',
        'apkSha256':hashlib.sha256(apk.read_bytes()).hexdigest(),'apkBytes':apk.stat().st_size,
        'targetPackage':'com.capstonedesign2026.qa','verifierPackage':'com.capstonedesign2026.qa.verifier',
        'existingCodexDebugSigner':True,'hostChecks':counts,
        'requiredArguments':['expectedSha256','expectedVersionCode','expectedMeasurementConsentUtcMs','expectedEnabled']}
(out/'build-result.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({'status':'PASS','apk':str(apk),'hostChecks':counts}))
PY
