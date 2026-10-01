#!/usr/bin/env python3
"""Ordered physical-device gate: 5 s x 12, then 600 s x 6. No raw export."""
import argparse,datetime,hashlib,json,pathlib,subprocess,time,sys,re
p=argparse.ArgumentParser();p.add_argument('--serial',required=True);p.add_argument('--output',required=True);p.add_argument('--apk',required=True);p.add_argument('--screen-off',action='store_true',help='Explicitly sleep display and require OFF evidence');p.add_argument('--skip-install',action='store_true',help='Use installed APK only after SHA256 match');args=p.parse_args()
out=pathlib.Path(args.output);out.mkdir(parents=True,exist_ok=True);out.chmod(0o700)
package='com.capstonedesign2026.mockup'
def adb(*a,check=True):
 r=subprocess.run(['adb','-s',args.serial,*a],capture_output=True,text=True,timeout=180 if a and a[0]=='install' else 40)
 if check and r.returncode:raise RuntimeError(r.stderr.strip() or r.stdout.strip())
 return r.stdout.strip()
def read(name):
 value=adb('exec-out','run-as',package,'cat','files/platform/summaries/'+name,check=False)
 return value if value.startswith('{') else ''
def power_state():
 value=adb('shell','dumpsys','power')
 match=re.search(r'Display Power: state=(\w+)',value)
 return {'display':match.group(1) if match else 'UNKNOWN','wakefulness':next((x.strip() for x in value.splitlines() if 'mWakefulness=' in x),'UNKNOWN')}
def raw_signature():return adb('exec-out','run-as',package,'sha256sum','files/platform/sensor_samples.jsonl',check=False)
def save(name,value):
 target=out/name;temporary=out/(name+'.tmp');temporary.write_text(json.dumps(value,indent=2)+'\n');temporary.replace(target)
assert adb('shell','getprop','ro.product.model')=='SM-T500','Explicit target must be the authorized A7 SM-T500'
if args.skip_install:
 apk_path=adb('shell','pm','path',package).removeprefix('package:')
 assert '\n' not in apk_path and apk_path.startswith('/data/app/'), 'Unexpected installed APK path'
 assert adb('shell','sha256sum',apk_path).split()[0]==hashlib.sha256(pathlib.Path(args.apk).read_bytes()).hexdigest(), 'Installed APK differs'
else:adb('install','-r',args.apk)
save('device.json',{'model':'SM-T500','android':adb('shell','getprop','ro.build.version.release'),'serial':'redacted','apkSha256':hashlib.sha256(pathlib.Path(args.apk).read_bytes()).hexdigest()})
for window,duration,phase in [(5000,60000,'short'),(600000,3600000,'hour')]:
 run='summary_'+phase+'_'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
 before=raw_signature();save(phase+'-raw-before.json',{'signature':before});adb('shell','am','force-stop',package)
 if args.screen_off:adb('shell','input','keyevent','KEYCODE_WAKEUP')
 adb('shell','am','start','-n',package+'/com.unity3d.player.UnityPlayerGameActivity','--es','testScene','SensorSummaryExperiment','--es','summaryWindowMs',str(window),'--es','summaryDurationMs',str(duration),'--es','summaryRunId',run)
 deadline=time.monotonic()+45
 while True:
  metadata_text=read(run+'.meta.json')
  if metadata_text:break
  if time.monotonic()>deadline:raise RuntimeError('Service did not create run metadata')
  time.sleep(.25)
 meta=json.loads(metadata_text)
 if args.screen_off:adb('shell','input keyevent KEYCODE_HOME; input keyevent KEYCODE_SLEEP')
 else:adb('shell','input','keyevent','KEYCODE_HOME')
 save(phase+'-meta.json',meta)
 if args.screen_off:
  screen_start=power_state()
  off_deadline=time.monotonic()+5
  while screen_start['display']!='OFF' and time.monotonic()<off_deadline:
   time.sleep(.1);screen_start=power_state()
  screen_start['verifiedElapsedMs']=int(float(adb('shell','cat','/proc/uptime').split()[0])*1000)
  save(phase+'-screen-start.json',screen_start)
  if screen_start['display']!='OFF' or screen_start['verifiedElapsedMs']>meta['startElapsedMs']:
   adb('shell','am','force-stop',package)
   raise RuntimeError('Screen OFF before measurement was not confirmed; run stopped as invalid')
 started=time.monotonic();deadline=started+duration/1000+90
 save('status.json',{'phase':phase,'state':'RUNNING','runId':run,'durationMs':duration,'windowMs':window,'startedHostUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
 print(f'{phase}: background collection running ({duration//1000}s, {window//1000}s windows)',flush=True)
 # No periodic ADB polling during the collection; avoid adding repeated USB/network wakeups.
 remaining=duration/1000+3
 while remaining>0:
  step=min(30,remaining);time.sleep(step);remaining-=step
  print(f'{phase}: host waiting; {int(remaining)}s until device result collection',flush=True)
 # A sleeping Wi-Fi device may lose its ADB transport. Recover only after the
 # measurement wait; never restart the app or discard its completed run.
 try:
  screen_end=power_state() if args.screen_off else None
 except (RuntimeError,subprocess.TimeoutExpired):
  subprocess.run(['adb','connect',args.serial],capture_output=True,text=True,timeout=20)
  screen_end=power_state() if args.screen_off else None
 if args.screen_off:save(phase+'-screen-end.json',screen_end)
 finished=read(run+'.finished.json')
 while not finished and time.monotonic()<deadline:
  time.sleep(5);finished=read(run+'.finished.json')
 raw=read(run+'.jsonl');rows=[json.loads(x) for x in raw.splitlines() if x.strip()]
 save(phase+'-windows.json',rows);save(phase+'-finished.json',json.loads(finished) if finished else {'missing':True})
 expected=duration//window
 checks={'rowCount':len(rows)==expected,'contiguousIndices':[r['index'] for r in rows]==list(range(expected)),
  'exactWindows':all(r['endElapsedMs']-r['startElapsedMs']==window for r in rows),
  'accelerometerEachWindow':all(r['accelerationSamples']>0 for r in rows),
  'gyroEachWindow':all(r['gyroSamples']>0 for r in rows),
  'orientationIfSupported':not any(s['type']==11 and s['registered'] for s in meta['sensors']) or all(r['orientationSamples']>0 for r in rows),
  'windowCoverage':all(r['firstEventElapsedMs'] is not None and r['lastEventElapsedMs']-r['firstEventElapsedMs']>=window*.8 for r in rows),
  'rawFileUnchanged':before==raw_signature(),
  'finishedWithoutWriteError':bool(finished) and not json.loads(finished).get('writeError',True),
  'serviceStopped':'SensorForegroundService' not in adb('shell','dumpsys','activity','services',package)}
 if args.screen_off:
  checks.update({'screenOffAtStart':screen_start['display']=='OFF','screenOffBeforeMeasurement':screen_start['verifiedElapsedMs']<=meta['startElapsedMs'],'screenOffEveryFlush':bool(rows) and all(not r['screenInteractiveAtFlush'] for r in rows),'screenOffAtEnd':screen_end['display']=='OFF'})
 save(phase+'-result.json',{'checks':checks,'passed':all(checks.values()),'rows':len(rows),'expected':expected,'durationMs':duration,'windowMs':window,'screenPowerAtEnd':power_state(),'screenOffRequested':args.screen_off})
 save('status.json',{'phase':phase,'state':'PASS' if all(checks.values()) else 'FAIL','runId':run,'checks':checks})
 print(phase+': '+json.dumps(checks),flush=True)
 if not all(checks.values()):sys.exit(1)
print('PASS: short gate and one-hour A7 background summary experiment',flush=True)
