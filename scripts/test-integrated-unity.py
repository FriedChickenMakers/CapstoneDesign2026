#!/usr/bin/env python3
"""Prepare/run isolated Linux UI and Play gates; never run Unity as root.

Prepare copies product sources/assets and explicitly selected validators, never
Library, user preferences or saves. Sequential cases share one movable project
and its newly generated cache; Play output keeps its required project sibling.
"""
from __future__ import annotations
import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import shutil
import signal
import struct
import subprocess
import time
import uuid
import zlib

ROOT = Path(__file__).resolve().parents[1]
UNITY = Path('/opt/unity/editors/6000.3.24f1/Editor/Unity')
COMPANY = 'CapstoneSyntheticValidation'


def case(source, product, flag, checks, captures, extra=(), play=0, pointer=True):
    return dict(source=source, product=product, flag=flag, checks=checks,
                captures=captures, extra=list(extra), play=play, pointer=pointer)


CASES = {
    'daily': case('Automation/DailyMissionValidation', 'DailyMissions', '-dailyReport', 23,
                  ['home-fit-locked-320x568.png', 'emotion-home-complete-390x844.png', 'journal-current-month-360x640.png'], ['-dailyCapture']),
    'guided': case('Automation/GuidedFocusValidation', 'GuidedFocus', '-guidedReport', 12,
                   ['P01-visual-practice-320x568.png', 'P02-visual-practice-390x844.png'], ['-guidedCapture']),
    'basic-slides': case('Automation/BasicFocusSlidesValidation', 'BasicSlides', '-basicSlidesReport', 7,
                         ['P08-overview-320x568.png', 'P28-complete-390x844.png'], ['-basicSlidesCapture']),
    'combined-memory': case('Automation/CombinedPlantMemoryValidation', 'CombinedMemory', '-combinedMemoryReport', 19,
                            ['garden-memory-popup-360x640.png', 'next-individual-390x844.png']),
    'full-reset-ui': case('Automation/FullResetValidation', 'FullReset', '-fullResetReport', 8,
                          ['settings-reset-complete-360x640.png', 'restart-pending-reset-360x640.png']),
    'memos': case('GardenPlantMemoValidation', 'PlantMemos', '-memoReport', 4, [], pointer=False),
    'memo-popup': case('GardenPlantMemoPopupValidation', 'MemoPopup', '-memoPopupReport', 17,
                       ['garden-tap-popup-360x640.png', 'album-records-bottom-390x844.png']),
    'mission-growth-play': case('GardenMissionGrowthPlayValidation', 'DailyEmotionPlay', '-missionPlayOutput', 0,
                                ['garden-after-daily-emotion-memo-garden-target-only.png', 'garden-after-mission-and-emotion-restarts-garden-target-only.png'], play=3),
    'full-reset-play': case('GardenFullResetPlayValidation', 'FullResetPlay', '-fullResetPlayOutput', 0,
                            ['after-reset-garden-target-only.png', 'restarted-fresh-garden-garden-target-only.png'], play=2, pointer=False),
}
REQUIRED_CASES = tuple(name for name in CASES if name not in ('memos', 'memo-popup'))
FONT_ASSET = 'Assets/Art/Prototype/Fonts/NotoSansKR SDF.asset'
DEFERRED = {
    'GardenPlantMemoValidation': 'Historical paper-overlay MemoView is retired by GardenHomePresenter; combined leaf geometry/render/picking is checked by CombinedPlantMemoryValidation.',
    'GardenPlantMemoPopupValidation': 'Historical MemoView/studio paper picking and manual archive fixture; current leaf popup/record lifecycle is checked by CombinedPlantMemoryValidation.',
    'GardenPlantAlbumValidation': 'Historical manual photo-save -> empty garden -> new-plant contract; latest automatic archive is covered by CombinedPlantMemoryValidation and MissionGrowth Play.',
    'GardenPlantAlbumPlayValidation': 'Historical interactive-only manual archive contract; not treated as current QA success or silently skipped after failure.',
}


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''): h.update(block)
    return h.hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.tmp')
    temporary.write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n')
    temporary.chmod(0o600)
    temporary.replace(path)


def boundary(path):
    raw = Path(path).absolute()
    for part in (raw, *raw.parents):
        if part.is_symlink(): raise ValueError('Linked evidence/project paths are not allowed')
    resolved = raw.resolve()
    if not resolved.is_relative_to(ROOT / 'artifacts') or resolved == ROOT / 'artifacts':
        raise ValueError('Use a private, fresh directory beneath repository artifacts')
    return resolved


def sources():
    found = set()
    # Android plugins are irrelevant to standalone Editor fixtures; scene/resource
    # dependencies stay intact, including their metadata. No user state is copied.
    for folder in ('Assets', 'Packages', 'ProjectSettings'):
        for path in (ROOT / folder).rglob('*'):
            if path.is_symlink(): raise ValueError('Source symlinks require an explicit audit')
            if not path.is_file(): continue
            rel = path.relative_to(ROOT)
            if folder == 'Assets' and rel.parts[1] in ('Editor', 'Editor.meta', 'Tests', 'Tests.meta', 'Plugins', 'Plugins.meta'): continue
            if rel.as_posix() == 'ProjectSettings/ProjectSettings.asset': continue
            found.add(rel.as_posix())
    editor = {item['source'] for item in CASES.values()} | {
        'Automation/SolidIslandValidation', 'Automation/ScreenDensityTextValidation', 'Automation/KenneyGardenBuilder',
        'Automation/NavigationLayoutValidation', 'Automation/FocusMissionValidation'}
    for name in editor:
        for suffix in ('.cs', '.cs.meta'):
            found.add('Assets/Editor/' + name + suffix)
    return sorted(found)


def player_settings(product, color):
    return ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!129 &1\nPlayerSettings:\n'
            '  m_ObjectHideFlags: 0\n  serializedVersion: 28\n  productGUID: ' + uuid.uuid4().hex + '\n'
            '  companyName: ' + COMPANY + '\n  productName: ' + product + '\n'
            '  activeInputHandler: 2\n  m_ActiveColorSpace: ' + str(color) + '\n'
            '  applicationIdentifier:\n    Standalone: com.capstone.synthetic.integratedqa\n')


def prepare(bundle):
    if bundle.exists(): raise ValueError('Preparation requires a fresh directory')
    bundle.mkdir(parents=True, mode=0o700)
    project = bundle / 'project'
    hashes = {}
    for rel in sources():
        source, target = ROOT / rel, project / rel
        before = digest(source)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        if digest(target) != before or digest(source) != before: raise RuntimeError('Source changed during copying: ' + rel)
        hashes[rel] = before
    baseline = bundle / 'baseline' / FONT_ASSET
    baseline.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(project / FONT_ASSET, baseline)
    original = ROOT / 'ProjectSettings/ProjectSettings.asset'
    match = re.search(r'^\s*m_ActiveColorSpace:\s*([01])\s*$', original.read_text(), re.M)
    if not match: raise ValueError('Source color space is missing')
    color = int(match.group(1))
    (project / 'ProjectSettings/ProjectSettings.asset').write_text(player_settings('Prepared-' + bundle.name, color))
    commit = subprocess.check_output(['git', '-c', 'safe.directory=' + str(ROOT), 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    manifest = dict(schemaVersion=1, sourceRoot=str(ROOT), sourceCommit=commit, preparedUtc=time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
                    runnerSha256=digest(Path(__file__)), sourceHashes=hashes, sourcePlayerSettingsSha256=digest(original), colorSpace=color,
                    company=COMPANY, currentProject='project', cases=CASES, requiredCases=REQUIRED_CASES, deferred=DEFERRED, libraryCopied=False,
                    privacy='Synthetic identity before Editor startup; codex HOME unchanged for license; no enrollment or personal saves copied; each run has private synthetic roots.')
    write_json(bundle / 'manifest.json', manifest)
    print('PREPARED', bundle, 'files=' + str(len(hashes)), flush=True)
    return manifest


def verify_sources(bundle, manifest, snapshot_only=False):
    project = boundary(bundle / manifest['currentProject'])
    errors = []
    for rel, expected in manifest['sourceHashes'].items():
        for prefix, root in (('copy', project),) if snapshot_only else (('source', ROOT), ('copy', project)):
            path = root / rel
            if not path.is_file() or path.is_symlink() or digest(path) != expected: errors.append(prefix + ':' + rel)
    if not snapshot_only and digest(ROOT / 'ProjectSettings/ProjectSettings.asset') != manifest['sourcePlayerSettingsSha256']: errors.append('source:ProjectSettings/ProjectSettings.asset')
    if digest(Path(__file__)) != manifest['runnerSha256']: errors.append('runner changed after preparation')
    return errors


def restore_import_cache(bundle, manifest, run):
    """Only this font's generated feature table may change; preserve proof first."""
    if FONT_ASSET not in manifest['sourceHashes']: return []
    baseline = bundle / 'baseline' / FONT_ASSET
    target = bundle / manifest['currentProject'] / FONT_ASSET
    expected = manifest['sourceHashes'][FONT_ASSET]
    if digest(target) == expected: return []
    if not baseline.is_file() or digest(baseline) != expected: raise ValueError('Frozen font baseline is unavailable')
    pattern = r'(?ms)^  m_FontFeatureTable:.*?(?=^  m_ShouldReimportFontFeatures:)'
    old, changed = baseline.read_text(), target.read_text()
    if len(re.findall(pattern, old)) != 1 or len(re.findall(pattern, changed)) != 1 or re.sub(pattern, '<GENERATED_FONT_FEATURES>', old) != re.sub(pattern, '<GENERATED_FONT_FEATURES>', changed):
        raise ValueError('Font mutation extends beyond the reviewed generated feature table')
    saved = run / 'import-mutations' / FONT_ASSET
    saved.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(target, saved)
    observed = digest(saved); shutil.copy2(baseline, target)
    return [dict(path=FONT_ASSET, preparedSha256=expected, importedSha256=observed, restoredSha256=digest(target),
                 scope='Unity-generated font feature/kerning table only; original asset untouched', retainedCopy=str(saved))]


def processes():
    result = {}
    for folder in Path('/proc').iterdir():
        if not folder.name.isdigit(): continue
        try:
            stat = (folder / 'stat').read_text().rsplit(')', 1)[1].split()
            if stat[0] == 'Z': continue
            cmd = (folder / 'cmdline').read_bytes().split(b'\0')
            result[int(folder.name)] = (int(stat[1]), stat[19], cmd)
        except (FileNotFoundError, PermissionError, ProcessLookupError): pass
    return result


def unity_pids(snapshot):
    return {pid: data[1] for pid, data in snapshot.items() if data[2] and Path(os.fsdecode(data[2][0])).name == 'Unity'}


def track_children(child, tracked, before):
    current = processes()
    owners = {child.pid} | {pid for pid, start in tracked.items() if pid in current and current[pid][1] == start}
    changed = True
    while changed:
        added = {pid for pid, data in current.items() if data[0] in owners and pid not in before}
        changed = not added.issubset(owners); owners |= added
    tracked.update({pid: current[pid][1] for pid in owners if pid in current and pid not in before})


def terminate_owned(child, tracked):
    # The process group catches ordinary workers; recorded start times also cover
    # a child that created its own session. Preexisting license/editor PIDs are excluded.
    for sig, wait in ((signal.SIGTERM, 5), (signal.SIGKILL, 3)):
        current = processes()
        alive = {pid for pid, start in tracked.items() if pid in current and current[pid][1] == start}
        if child.poll() is None:
            try: os.killpg(child.pid, sig)
            except ProcessLookupError: pass
        for pid in alive:
            try: os.kill(pid, sig)
            except ProcessLookupError: pass
        if child.poll() is None:
            try: child.wait(timeout=wait)
            except subprocess.TimeoutExpired: pass
        if not alive: break
        time.sleep(.1)


def png_info(path):
    raw = path.read_bytes()
    if raw[:8] != b'\x89PNG\r\n\x1a\n': raise ValueError('Invalid PNG signature')
    at, width, height, ended, has_pixels = 8, 0, 0, False, False
    while at + 12 <= len(raw):
        length = struct.unpack('>I', raw[at:at+4])[0]; kind = raw[at+4:at+8]
        end = at + length + 12
        if end > len(raw) or zlib.crc32(raw[at+4:end-4]) & 0xffffffff != struct.unpack('>I', raw[end-4:end])[0]: raise ValueError('Truncated or corrupt PNG')
        if kind == b'IHDR': width, height = struct.unpack('>II', raw[at+8:at+16])
        if kind == b'IDAT' and length: has_pixels = True
        if kind == b'IEND': ended = True; break
        at = end
    if not ended or not has_pixels or min(width, height) < 32: raise ValueError('Incomplete or empty-size PNG')
    return dict(width=width, height=height, sha256=hashlib.sha256(raw).hexdigest())


def validate_report(config, report_path):
    report = json.loads(report_path.read_text())
    if report.get('assertions', 0) <= 0: raise ValueError('No executed assertions')
    if config['play']:
        if report.get('stage') != 'pass' or report.get('phase') != config['play'] or report.get('runtimeErrors') != 0 or report.get('error') or report.get('environmentRestored') is not True:
            raise ValueError('Incomplete/failed Play lifecycle or restoration')
        if config['play'] == 3 and (report.get('batch') is not True or report.get('gizmosRestored') is not True): raise ValueError('Batch Play/view restoration incomplete')
    else:
        checks = report.get('checks', [])
        if report.get('failed') != 0 or report.get('errors') or report.get('passed') != config['checks'] or len(checks) != config['checks'] or any(check.get('status') != 'pass' for check in checks):
            raise ValueError('Incomplete/failed UI checks; inspect raw report')
    if config['pointer'] and (report.get('clicks', 0) <= 0 or report.get('raycasts', 0) <= 0): raise ValueError('Missing real pointer/raycast evidence')
    captures = report.get('captures', [])
    if not captures: raise ValueError('No screenshots recorded')
    index = {}
    for name in captures:
        path = Path(name)
        path = path if path.is_absolute() else report_path.parent / path
        if path.is_symlink() or not path.resolve().is_relative_to(report_path.parent.resolve()): raise ValueError('Capture escaped private report directory')
        index[path.name] = png_info(path)
    missing = set(config['captures']) - set(index)
    if missing: raise ValueError('Missing mandatory screenshots: ' + ', '.join(sorted(missing)))
    return dict(assertions=report['assertions'], screenshots=index, checks=report.get('passed'), phase=report.get('phase'))


def run_case(bundle, manifest, name, unity, timeout, snapshot_only=False, attempt=None):
    config = CASES[name]; run = bundle / 'runs' / (name + ('-' + attempt if attempt else ''))
    if unity_pids(processes()): raise RuntimeError('A Unity Editor is already running; finish it before preparing this gate')
    if run.exists(): raise ValueError('Case already has evidence; use a fresh bundle for a retry')
    mismatches = verify_sources(bundle, manifest, snapshot_only)
    if mismatches: raise ValueError('Prepared sources no longer match: ' + ', '.join(mismatches[:8]))
    run.mkdir(parents=True, mode=0o700)
    project = boundary(bundle / manifest['currentProject'])
    if config['play']:
        target = run / 'project'
        project.rename(target)  # Move the owned cache; never clone or touch the original project's Library.
        project = target; manifest['currentProject'] = str(project.relative_to(bundle)); write_json(bundle / 'manifest.json', manifest)
        # Bee stores absolute paths to its previous project. Keep import/package
        # caches, but rebuild only this owned disposable compilation cache.
        bee = project / 'Library/Bee'
        if bee.exists(): shutil.rmtree(bee)
    product = config['product'] + '-' + bundle.name + ('-' + attempt if attempt else '')
    settings = project / 'ProjectSettings/ProjectSettings.asset'; settings.write_text(player_settings(product, manifest['colorSpace']))
    output = run / ('play' if config['play'] else 'renders')
    if not config['play']: output.mkdir(mode=0o700)
    report_path = output / ('play-report.json' if config['play'] else 'report.json')
    arguments = [str(unity), '-batchmode', '-force-glcore', '-screen-width', '390', '-screen-height', '844',
                 '-projectPath', str(project), '-executeMethod', 'CapstoneDesign.EditorTools.' + Path(config['source']).name + '.RunBatch',
                 config['flag'], str(output if config['play'] else report_path), '-logFile', str(run / 'unity.log')] + config['extra']
    env = {k: v for k, v in os.environ.items() if not k.startswith('CAPSTONE_')}
    env.update(HOME='/home/codex', USER='codex', LOGNAME='codex', DISPLAY=':99',
               CAPSTONE_SMOKE_SAVE_ROOT=str(run / 'startup-synthetic-save'), CAPSTONE_PERSONALIZATION_TEST_ROOT=str(run / 'unregistered-synthetic-client'))
    before = processes(); existing_unity = unity_pids(before)
    if existing_unity: raise RuntimeError('A Unity Editor is already running; finish it before this sequential gate')
    started = time.monotonic(); child = None; tracked = {}; result = dict(case=name, status='FAIL', command=arguments,
        company=COMPANY, product=product, project=str(project), settingsSha256=digest(settings), unityAccount='codex', home=env['HOME'], display=env['DISPLAY'],
        scope='Frozen prepared snapshot; original source drift is recorded separately' if snapshot_only else 'Prepared snapshot and current source must match')
    old_handlers = {}
    try:
        def interrupted(signum, frame): raise InterruptedError('Interrupted signal ' + str(signum))
        for sig in (signal.SIGTERM, signal.SIGINT): old_handlers[sig] = signal.signal(sig, interrupted)
        with (run / 'process.log').open('wb') as log:
            child = subprocess.Popen(arguments, cwd=project, env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            result['childPid'] = child.pid
            while child.poll() is None:
                track_children(child, tracked, before)
                if time.monotonic() - started > timeout: raise TimeoutError('Owned Unity gate exceeded its deadline')
                time.sleep(.2)
            if child.returncode != 0: raise RuntimeError('Unity exit code ' + str(child.returncode))
        result.update(validate_report(config, report_path)); result['status'] = 'PASS'
    except (Exception, KeyboardInterrupt) as error:
        result['error'] = type(error).__name__ + ': ' + str(error)
    finally:
        if child is not None: terminate_owned(child, tracked)
        for sig, handler in old_handlers.items(): signal.signal(sig, handler)
        result['seconds'] = round(time.monotonic() - started, 2)
        try: result['importMutations'] = restore_import_cache(bundle, manifest, run)
        except Exception as error: result['status']='FAIL'; result['importMutationError']=str(error)
        result['originalSourceDrift'] = [item for item in verify_sources(bundle, manifest) if item.startswith('source:')]
        result['sourceMismatches'] = verify_sources(bundle, manifest, snapshot_only)
        if result['sourceMismatches']: result['status'] = 'FAIL'
        remaining = processes()
        result['ownedProcessesRemaining'] = [pid for pid, start in tracked.items() if pid in remaining and remaining[pid][1] == start]
        if result['ownedProcessesRemaining']: result['status'] = 'FAIL'
        write_json(run / 'result.json', result)
    print(name, result['status'], result.get('error', ''), flush=True)
    return result


def source_lock_path():
    # A freshly cloned project has not created Library yet. The runner owns only
    # this shared lock file; it never copies another project's cache here.
    directory = ROOT / 'Library'
    directory.mkdir(exist_ok=True)
    return directory / 'capstone-editor.lock'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--prepare', type=Path)
    mode.add_argument('--prepared', type=Path)
    parser.add_argument('--case', action='append', choices=tuple(CASES))
    parser.add_argument('--attempt', help='Fresh evidence suffix for a retry; earlier results remain untouched')
    parser.add_argument('--unity', type=Path, default=UNITY)
    parser.add_argument('--timeout', type=int, default=1200)
    parser.add_argument('--snapshot-only', action='store_true', help='Validate the frozen copied source; record concurrent original-source changes separately')
    parser.add_argument('--record-runner-update', action='store_true', help='Record a reviewed harness revision when resuming an existing frozen bundle')
    args = parser.parse_args()
    if args.timeout <= 0: parser.error('Timeout must be positive')
    if args.attempt and not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]{0,39}',args.attempt): parser.error('Use a simple attempt name')
    bundle = boundary(args.prepare or args.prepared)
    if args.prepare:
        prepare(bundle); return 0
    account = pwd.getpwuid(os.geteuid())
    if account.pw_name != 'codex' or os.environ.get('HOME') != account.pw_dir:
        parser.error('Run as OS user codex with HOME set to that account home directory; never root Unity')
    if not args.unity.is_file(): parser.error('Unity executable missing')
    manifest = json.loads((bundle / 'manifest.json').read_text())
    if manifest.get('sourceRoot') != str(ROOT) or manifest.get('company') != COMPANY: parser.error('Unrecognized prepared project identity')
    results = []
    with (bundle / '.runner.lock').open('a') as lock, source_lock_path().open('a') as source_lock:
        for descriptor in (lock, source_lock): fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        if args.record_runner_update:
            previous = manifest['runnerSha256']; current = digest(Path(__file__))
            manifest.setdefault('runnerRevisions', []).append(dict(previousSha256=previous, revisedSha256=current, utc=time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())))
            manifest['runnerSha256']=current; manifest['requiredCases']=REQUIRED_CASES; manifest['deferred']=DEFERRED
            saved=bundle/'runner-revisions'/current; saved.parent.mkdir(exist_ok=True); shutil.copy2(Path(__file__),saved)
            write_json(bundle/'manifest.json',manifest)
        for name in args.case or REQUIRED_CASES:
            result = run_case(bundle, manifest, name, args.unity, args.timeout, args.snapshot_only, args.attempt); results.append(result)
            write_json(bundle / 'last-run.json', dict(status='PASS' if all(r['status']=='PASS' for r in results) else 'FAIL', results=results))
            if result['status'] != 'PASS': return 1
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
