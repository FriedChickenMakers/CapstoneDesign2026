#!/usr/bin/env python3
"""Physical A7 gate: remove our task from Recents, then inspect summary windows."""
import argparse
import datetime
import hashlib
import json
import pathlib
import subprocess
import time

parser = argparse.ArgumentParser()
parser.add_argument('--serial', required=True)
parser.add_argument('--apk', required=True)
parser.add_argument('--output', required=True)
parser.add_argument('--skip-install', action='store_true', help='Verify installed APK SHA-256 instead of reinstalling')
args = parser.parse_args()
out = pathlib.Path(args.output)
out.mkdir(parents=True, exist_ok=True)
out.chmod(0o700)
package = 'com.capstonedesign2026.mockup'
component = package + '/com.unity3d.player.UnityPlayerGameActivity'


def adb(*arguments, check=True, timeout=40):
    result = subprocess.run(['adb', '-s', args.serial, *arguments],
                            capture_output=True, text=True, timeout=timeout)
    if check and result.returncode:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip())
    return result.stdout.strip()


def save(name, value):
    (out / name).write_text(json.dumps(value, indent=2) + '\n')


def read(run_id, suffix):
    value = adb('exec-out', 'run-as', package, 'cat',
                f'files/platform/summaries/{run_id}.{suffix}', check=False)
    return value if value.startswith('{') else ''


def process_ids():
    return {'ui': adb('shell', 'pidof', package, check=False),
            'sensor': adb('shell', 'pidof', package + ':sensor', check=False)}


def elapsed_ms():
    return int(float(adb('shell', 'cat', '/proc/uptime').split()[0]) * 1000)


def screen_off():
    return 'Display Power: state=OFF' in adb('shell', 'dumpsys', 'power')


def task_present():
    return package in adb('shell', 'dumpsys', 'activity', 'recents')


assert adb('shell', 'getprop', 'ro.product.model') == 'SM-T500'
apk_sha = hashlib.sha256(pathlib.Path(args.apk).read_bytes()).hexdigest()
if args.skip_install:
    installed = adb('shell', 'pm', 'path', package).removeprefix('package:')
    if '\n' in installed or not installed.startswith('/data/app/'):
        raise RuntimeError('Unexpected installed APK path')
    if adb('shell', 'sha256sum', installed).split()[0] != apk_sha:
        raise RuntimeError('Installed APK differs from retained build')
else:
    adb('install', '-r', args.apk, timeout=180)
save('device.json', {'model': 'SM-T500', 'android': adb('shell', 'getprop', 'ro.build.version.release'),
                     'apkSha256': apk_sha,
                     'serial': 'redacted'})

for phase, window_ms, duration_ms in [('short', 5000, 60000), ('hour', 600000, 3600000)]:
    run = 'recents_' + phase + '_' + datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    raw_before = adb('exec-out', 'run-as', package, 'sha256sum',
                     'files/platform/sensor_samples.jsonl', check=False)
    save(phase + '-raw-before.json', {'signature': raw_before})
    adb('shell', 'input', 'keyevent', 'KEYCODE_WAKEUP')
    adb('shell', 'input', 'swipe', '600', '1800', '600', '400', '250')
    adb('shell', 'am', 'start', '-n', component, '--es', 'testScene', 'SensorSummaryExperiment',
        '--es', 'summaryWindowMs', str(window_ms), '--es', 'summaryDurationMs', str(duration_ms),
        '--es', 'summaryRunId', run)
    limit = time.monotonic() + 45
    while True:
        metadata = read(run, 'meta.json')
        if metadata:
            break
        if time.monotonic() >= limit:
            raise RuntimeError('Service did not start from one app launch')
        time.sleep(.2)
    meta = json.loads(metadata)
    save(phase + '-meta.json', meta)
    before_removal = process_ids()
    if not before_removal['ui'] or not before_removal['sensor'] or before_removal['ui'] == before_removal['sensor']:
        raise RuntimeError('UI and sensor service did not start in distinct processes')
    adb('shell', 'input', 'keyevent', 'KEYCODE_HOME')
    adb('shell', 'input', 'keyevent', 'KEYCODE_APP_SWITCH')
    time.sleep(.6)
    if not task_present():
        raise RuntimeError('Our app is not in Recents before removal')
    # A7's 1200x1928 Samsung Recents grid: most recent card, upper right.
    # Dismiss that card only; never press Close all or force-stop the package.
    adb('shell', 'input', 'swipe', '925', '650', '925', '100', '350')
    time.sleep(.4)
    removal_elapsed = elapsed_ms()
    after_removal = process_ids()
    services = adb('shell', 'dumpsys', 'activity', 'services', package)
    removal = {'runId': run, 'taskAbsent': not task_present(),
               'removedElapsedMs': removal_elapsed,
               'beforeRemovalPids': before_removal, 'afterRemovalPids': after_removal,
               'sensorServicePresent': 'SensorForegroundService' in services,
               'method': 'swipe this app card from Samsung Recents'}
    save(phase + '-removal.json', removal)
    if not removal['taskAbsent'] or not removal['sensorServicePresent']:
        raise RuntimeError('Recents removal did not leave a foreground sensor service')
    adb('shell', 'input', 'keyevent', 'KEYCODE_SLEEP')
    off_deadline = time.monotonic() + 5
    while not screen_off() and time.monotonic() < off_deadline:
        time.sleep(.1)
    if not screen_off():
        raise RuntimeError('Display did not turn OFF')
    save(phase + '-status.json', {'state': 'RUNNING', 'runId': run,
                                 'startedHostUtc': datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(f'{phase}: app removed from Recents; screen OFF; waiting {duration_ms // 1000}s', flush=True)
    # Only host sleeps while measuring; no periodic ADB requests wake the device.
    remaining = duration_ms / 1000 + 3
    while remaining > 0:
        step = min(30, remaining)
        time.sleep(step)
        remaining -= step
        print(f'{phase}: {int(remaining)}s until retrieval', flush=True)
    try:
        ending_screen_off = screen_off()
    except (RuntimeError, subprocess.TimeoutExpired):
        subprocess.run(['adb', 'connect', args.serial], capture_output=True, text=True, timeout=20)
        ending_screen_off = screen_off()
    finish = read(run, 'finished.json')
    deadline = time.monotonic() + 90
    while not finish and time.monotonic() < deadline:
        time.sleep(5)
        finish = read(run, 'finished.json')
    raw = read(run, 'jsonl')
    rows = [json.loads(line) for line in raw.splitlines() if line.strip()]
    save(phase + '-windows.json', rows)
    save(phase + '-finished.json', json.loads(finish) if finish else {'missing': True})
    post_removal = [r for r in rows if r['startElapsedMs'] >= removal_elapsed]
    expected_post = 8 if phase == 'short' else 5
    checks = {'taskAbsent': removal['taskAbsent'],
              'separateProcessesAtLaunch': bool(before_removal['ui'] and before_removal['sensor'] and before_removal['ui'] != before_removal['sensor']),
              'sensorProcessSurvivedRemoval': bool(after_removal['sensor']),
              'allExpectedWindows': len(rows) == duration_ms // window_ms,
              'postRemovalWindows': len(post_removal) >= expected_post,
              'postRemovalAccelerometer': all(r['accelerationSamples'] > 0 for r in post_removal),
              'postRemovalGyro': all(r['gyroSamples'] > 0 for r in post_removal),
              'postRemovalOrientation': all(r['orientationSamples'] > 0 for r in post_removal),
              'postRemovalCoverage': all(r['lastEventElapsedMs'] is not None and
                                         r['firstEventElapsedMs'] is not None and
                                         r['lastEventElapsedMs'] - r['firstEventElapsedMs'] >= window_ms * .8
                                         for r in post_removal),
              'screenOffAtEnd': ending_screen_off,
              'screenOffAtEveryFlush': bool(rows) and all(not r['screenInteractiveAtFlush'] for r in rows),
              'rawFileUnchanged': raw_before == adb('exec-out', 'run-as', package, 'sha256sum',
                                                   'files/platform/sensor_samples.jsonl', check=False),
              'finishedWithoutWriteError': bool(finish) and not json.loads(finish).get('writeError', True),
              'serviceStoppedAtDuration': 'SensorForegroundService' not in adb('shell', 'dumpsys',
                                                                                 'activity', 'services', package)}
    result = {'passed': all(checks.values()), 'checks': checks,
              'postRemovalRows': len(post_removal), 'totalRows': len(rows),
              'windowMs': window_ms, 'durationMs': duration_ms}
    save(phase + '-result.json', result)
    save(phase + '-status.json', {'state': 'PASS' if result['passed'] else 'FAIL', 'runId': run})
    print(f'{phase}: {json.dumps(result)}', flush=True)
    if not result['passed']:
        raise SystemExit(1)

print('PASS: sensor summaries continued after Recents dismissal through the one-hour gate', flush=True)
