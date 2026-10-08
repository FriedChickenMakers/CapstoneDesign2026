#!/usr/bin/env python3
"""Run Android storage regression checks using an installed development APK and isolated private files.

Usage: python3 scripts/test-daily-acceleration-device.py SERIAL
Build/install the current APK first. The caller owns device display-setting restoration.
"""
import os
import pathlib
import shlex
import subprocess
import sys
import time

root = pathlib.Path(__file__).resolve().parent.parent
serial = sys.argv[1]
package = 'com.capstonedesign2026.mockup'
out = pathlib.Path(os.environ.get('CAPSTONE_ARTIFACTS', root / 'artifacts/daily-device-test'))
out.mkdir(parents=True, exist_ok=True)
classes = out / 'classes'
classes.mkdir(exist_ok=True)
sdk = pathlib.Path('/opt/unity/current/Editor/Data/PlaybackEngines/AndroidPlayer/SDK')
android = sdk / 'platforms/android-36/android.jar'
lib = root / 'Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/CapstonePlatform.androidlib/build/intermediates/compile_library_classes_jar/debug/bundleLibCompileToJarDebug/classes.jar'

def run(args):
    result = subprocess.run([str(x) for x in args], text=True, capture_output=True)
    if result.returncode:
        print(result.stdout + result.stderr, file=sys.stderr)
        result.check_returncode()
    return result.stdout

def shell(*args):
    return run(['adb', '-s', serial, 'shell', shlex.join(args)]).strip()

run(['/opt/android-jdk/bin/javac', '-source', '17', '-target', '17', '-cp', str(android)+':'+str(lib), '-d', classes, root/'tests/native/DailyAccelerationDeviceTest.java'])
run(['/opt/android-jdk/bin/jar', 'cf', out/'tests.jar', '-C', classes, '.'])
subprocess.run([str(sdk/'build-tools/36.0.0/d8'), '--lib', str(android), '--output', str(out/'tests.zip'), str(out/'tests.jar')], env={**os.environ, 'JAVA_HOME':'/opt/android-jdk'}, check=True)
remote = '/data/local/tmp/capstone-daily-regression-' + str(time.time_ns()) + '.zip'
run(['adb', '-s', serial, 'push', out/'tests.zip', remote])
shell('chmod', '444', remote)
apk = shell('pm', 'path', package).removeprefix('package:')
private = shell('run-as', package, 'pwd')
# Keep test files outside files/platform so no fabricated readings enter the real collector.
isolated = private + '/cache/daily-regression-' + str(time.time_ns())
command = 'CLASSPATH=' + shlex.quote(remote + ':' + apk) + ' app_process /system/bin com.capstonedesign2026.platform.DailyAccelerationDeviceTest ' + shlex.quote(isolated)
try:
    result = shell('run-as', package, 'sh', '-c', command)
finally:
    shell('rm', '-f', remote)
(out/'result.txt').write_text(result+'\n')
print(result)
