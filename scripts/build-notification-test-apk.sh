#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="${NOTIFICATION_ANDROID_OUTPUT:-${repo_root}/artifacts/notification-android-tests}"
gradle_home="${UNITY_GRADLE_HOME:-/opt/unity/editors/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/Tools/gradle}"
java_bin="${JAVA_BIN:-/opt/android-jdk/bin/java}"
mkdir -p "$output"
python3 - "$repo_root" "$output" <<'PY'
import pathlib,sys
repo,out=map(lambda x:pathlib.Path(x).resolve(),sys.argv[1:])
project=out/'project'
def write(name,text):
 p=project/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text)
write('settings.gradle', '''pluginManagement { repositories { google(); mavenCentral(); gradlePluginPortal() } }
dependencyResolutionManagement { repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS); repositories { google(); mavenCentral() } }
rootProject.name = 'SyntheticNotificationValidation'
include ':app', ':platform'
''')
write('build.gradle', "plugins { id 'com.android.application' version '9.0.0' apply false; id 'com.android.library' version '9.0.0' apply false }\n")
write('gradle.properties', 'org.gradle.jvmargs=-Xmx2048M\nandroid.useAndroidX=true\nandroid.builtInKotlin=true\norg.gradle.workers.max=2\nandroid.enableJetifier=true\n')
write('app/build.gradle', '''plugins { id 'com.android.application' }
android {
 namespace 'com.capstonedesign2026.notificationpreview'
 compileSdk 36
 defaultConfig { applicationId 'com.capstonedesign2026.notificationpreview'; minSdk 29; targetSdk 36; versionCode 2; versionName 'virtual-clock-tests' }
 compileOptions { sourceCompatibility JavaVersion.VERSION_17; targetCompatibility JavaVersion.VERSION_17 }
}
dependencies { implementation project(':platform') }
''')
write('app/src/main/AndroidManifest.xml', '''<manifest xmlns:android="http://schemas.android.com/apk/res/android">
 <application android:label="Synthetic notification validation" android:theme="@android:style/Theme.Material.Light.NoActionBar" android:allowBackup="false" />
 <instrumentation android:name="com.capstonedesign2026.platform.NotificationValidationInstrumentation" android:targetPackage="com.capstonedesign2026.notificationpreview" android:label="Virtual clock notification validation" />
</manifest>
''')
native=repo/'Assets/Plugins/Android/CapstonePlatform.androidlib'
def quoted(path): return "'"+str(path).replace('\\','\\\\').replace("'","\\'")+"'"
gradle=(native/'build.gradle').read_text().replace("'src/main/AndroidManifest.xml'",quoted(native/'src/main/AndroidManifest.xml'))
gradle=gradle.replace("['src/main/kotlin']",'['+', '.join(map(quoted,[native/'src/main/kotlin',repo/'tests/android/notifications',project/'persona']))+']')
gradle=gradle.replace("['src/main/res']",'['+quoted(native/'src/main/res')+']')
write('platform/build.gradle',gradle)
write('persona/NotificationPersonaEvaluation.kt',(repo/'tests/native/NotificationPersonaEvaluation.kt').read_text())
PY
launcher=("$gradle_home"/lib/gradle-launcher-*.jar)
"$java_bin" -classpath "${launcher[0]}" org.gradle.launcher.GradleMain \
  -p "$output/project" --no-daemon --max-workers=2 :app:assembleDebug
cp "$output/project/app/build/outputs/apk/debug/app-debug.apk" "$output/notification-tests.apk"
printf 'Synthetic APK: %s/notification-tests.apk\n' "$output"
