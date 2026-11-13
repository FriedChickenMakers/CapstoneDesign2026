#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="${NOTIFICATION_ANDROID_OUTPUT:-${repo_root}/artifacts/notification-android-tests}"
gradle_home="${UNITY_GRADLE_HOME:-/opt/unity/editors/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/Tools/gradle}"
java_bin="${JAVA_BIN:-/opt/android-jdk/bin/java}"
mkdir -p "$output"
python3 - "$repo_root" "$output" <<'PY'
import pathlib,sys,os
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
 <application android:label="Synthetic notification validation" android:theme="@android:style/Theme.Material.Light.NoActionBar" android:allowBackup="false">
  <activity android:name="com.capstonedesign2026.platform.MeasurementPreviewActivity" android:exported="true">
   <intent-filter><action android:name="android.intent.action.MAIN"/><category android:name="android.intent.category.LAUNCHER"/></intent-filter>
  </activity>
 </application>
 <instrumentation android:name="com.capstonedesign2026.platform.MeasurementValidationInstrumentation" android:targetPackage="com.capstonedesign2026.notificationpreview" android:label="Synthetic measurement validation" />
 <instrumentation android:name="com.capstonedesign2026.platform.NotificationValidationInstrumentation" android:targetPackage="com.capstonedesign2026.notificationpreview" android:label="Virtual clock notification validation" />
</manifest>
''')
native=repo/'Assets/Plugins/Android/CapstonePlatform.androidlib'
def quoted(path): return "'"+str(path).replace('\\','\\\\').replace("'","\\'")+"'"
gradle=(native/'build.gradle').read_text().replace("'src/main/AndroidManifest.xml'",quoted(native/'src/main/AndroidManifest.xml'))
gradle=gradle.replace("['src/main/kotlin']",'['+', '.join(map(quoted,[native/'src/main/kotlin',repo/'tests/android/notifications',project/'persona']))+']')
gradle=gradle.replace("['src/main/res']",'['+quoted(native/'src/main/res')+']')
gradle += "\nandroid { sourceSets { main { assets.srcDirs = ["+quoted(native/'src/main/assets')+"] } } }\n"
if os.environ.get('NOTIFICATION_HOST_TESTS') == '1':
 gradle += "\ndependencies { testImplementation 'junit:junit:4.13.2'; testImplementation 'org.robolectric:robolectric:4.17'; testImplementation 'androidx.test:core:1.7.0'; testImplementation 'androidx.work:work-testing:2.11.2' }\n"
 gradle += "android { sourceSets { test { kotlin.srcDirs = ["+quoted(repo/'tests/robolectric')+"] } }; testOptions { unitTests.includeAndroidResources = true; unitTests.all { maxHeapSize = '1g'; systemProperty 'robolectric.dependency.repo.url', 'https://repo.maven.apache.org/maven2'; jvmArgs '--add-opens=java.base/java.lang=ALL-UNNAMED', '--add-opens=java.base/java.util=ALL-UNNAMED', '--add-opens=java.base/java.io=ALL-UNNAMED', '--add-opens=java.base/java.net=ALL-UNNAMED', '--add-opens=java.base/java.security=ALL-UNNAMED', '--add-opens=java.base/java.text=ALL-UNNAMED', '--add-opens=java.base/jdk.internal.access=ALL-UNNAMED', '--add-opens=java.desktop/java.awt.font=ALL-UNNAMED', '--add-opens=jdk.compiler/com.sun.tools.javac.api=ALL-UNNAMED' } } }\n"
write('platform/build.gradle',gradle)
write('persona/NotificationPersonaEvaluation.kt',(repo/'tests/native/NotificationPersonaEvaluation.kt').read_text())
PY
tasks=(:app:assembleDebug)
if [[ "${NOTIFICATION_HOST_TESTS:-0}" == 1 ]]; then tasks+=(:platform:testDebugUnitTest); fi
launcher=("$gradle_home"/lib/gradle-launcher-*.jar)
"$java_bin" -classpath "${launcher[0]}" org.gradle.launcher.GradleMain \
  -p "$output/project" --no-daemon --max-workers=2 "${tasks[@]}"
cp "$output/project/app/build/outputs/apk/debug/app-debug.apk" "$output/notification-tests.apk"
printf 'Synthetic APK: %s/notification-tests.apk\n' "$output"
