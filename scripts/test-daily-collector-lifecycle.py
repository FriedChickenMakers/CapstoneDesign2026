#!/usr/bin/env python3
"""Observe the current opt-in daily collector across foreground/Home/screen-off.

The collector must already have been enabled through normal app consent. This
does not enable, stop, reset, or fabricate sensor collection. It checks current
minute summaries/status, not the retired raw sensor_samples.jsonl stream.
"""
import argparse
import json
import os
from pathlib import Path
import sys
import time

from android_device_session import DeviceError, DeviceSession, DeviceSkip, ROOT, SKIP, durable_json, restore_on_signal, stamp


def snapshot(session, allow_disabled=False):
    script = 'p=files/platform/daily_acceleration/status.json; if [ -f "$p" ]; then cat "$p"; else echo "{}"; fi'
    state = json.loads(session.shell("run-as", session.package, "sh", "-c", script))
    if state.get("status") != "RUNNING":
        if allow_disabled:
            raise DeviceSkip("Daily collector is not RUNNING; enable it through the app before this lifecycle test")
        raise DeviceError("Daily collector stopped or lost its status during the required observation")
    return state


def advanced(before, after):
    return all(int(after.get(key, 0)) > int(before.get(key, 0)) for key in ("lastSampleEpochMs", "lastWriteEpochMs"))


def display_is_off(session):
    power = session.shell("dumpsys", "power")
    return "Display Power: state=OFF" in power or "mWakefulness=Asleep" in power or "mWakefulness=Dozing" in power


def observe(session, seconds, require_screen_off=False):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if require_screen_off and not display_is_off(session):
            raise DeviceError("Display did not remain off during the requested observation")
        time.sleep(min(30, max(0, deadline - time.monotonic())))
    if require_screen_off and not display_is_off(session):
        raise DeviceError("Display was on at the end of the observation")
    return snapshot(session)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--serial", default=os.environ.get("ADB_SERIAL"))
    parser.add_argument("--output")
    parser.add_argument("--seconds", type=int, default=120, help="Each foreground/Home observation, minimum 70 seconds")
    parser.add_argument("--screen-off-seconds", type=int, default=int(os.environ.get("PLATFORM_SCREEN_OFF_SECONDS", "120")))
    args = parser.parse_args()
    if args.seconds < 70 or args.screen_off_seconds < 70:
        parser.error("Each phase needs at least 70 seconds for the one-minute collector cadence")
    output = args.output or Path(os.environ.get("CAPSTONE_ARTIFACTS", ROOT / "artifacts")) / ("collector-" + stamp())
    report = {"scope": "current daily collector timestamp advance", "status": "FAIL", "checks": {},
              "notTested": ["start/stop consent UI", "force-stop/reboot", "battery drain", "lossless samples", "Health Connect"]}
    session = None
    try:
        session = DeviceSession(args.serial, output)
        with restore_on_signal(), session:
            snapshot(session, allow_disabled=True)  # Do not launch/reset an app when the requested service is disabled.
            session.launch()  # Backs up private data/preferences/external garden before launch.
            before = snapshot(session)
            foreground = observe(session, args.seconds)
            report["checks"]["foregroundAdvanced"] = advanced(before, foreground)
            session.shell("input", "keyevent", "KEYCODE_HOME")
            background = observe(session, args.seconds)
            report["checks"]["backgroundAdvanced"] = advanced(foreground, background)
            session.shell("input", "keyevent", "KEYCODE_SLEEP")
            try:
                time.sleep(2)
                screen_off = observe(session, args.screen_off_seconds, require_screen_off=True)
            finally:
                session.shell("input", "keyevent", "KEYCODE_WAKEUP")
            report["checks"]["screenOffAdvanced"] = advanced(background, screen_off)
            report["observations"] = {"before": before, "foreground": foreground, "background": background, "screenOff": screen_off}
            if not all(report["checks"].values()):
                raise DeviceError("Sample/write timestamps did not advance in every required phase")
        report["status"] = "PASS"
        print("PASS current daily collector: foreground/Home/screen-off advanced; display settings restored")
        return 0
    except DeviceSkip as error:
        report.update(status="SKIP", reason=str(error))
        print("SKIP: " + str(error), file=sys.stderr)
        return SKIP
    except (DeviceError, KeyboardInterrupt, ValueError) as error:
        report.update(status="FAIL", reason=str(error))
        print("FAIL: " + str(error), file=sys.stderr)
        return 1
    finally:
        if session:
            durable_json(session.output / "lifecycle-result.json", report)


if __name__ == "__main__":
    sys.exit(main())
