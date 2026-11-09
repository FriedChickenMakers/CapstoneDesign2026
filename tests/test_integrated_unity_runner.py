"""Synthetic runner contracts only: these tests never start a Unity Editor."""
import importlib.util
import json
import os
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest import mock
import zlib

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('integrated_unity', ROOT / 'scripts/test-integrated-unity.py')
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


def png():
    def chunk(kind, payload):
        body = kind + payload
        return struct.pack('>I', len(payload)) + body + struct.pack('>I', zlib.crc32(body) & 0xffffffff)
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB',32,32,8,2,0,0,0)) + chunk(b'IDAT',zlib.compress((b'\x00'+b'\x22\x88\x44'*32)*32)) + chunk(b'IEND',b'')


class IntegratedUnityRunnerTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix='synthetic-integrated-runner-')
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name); self.bundle = self.root / 'artifacts' / 'synthetic'
        self.project = self.bundle / 'project'
        for base in (self.root, self.project):
            (base/'ProjectSettings').mkdir(parents=True,exist_ok=True)
            (base/'ProjectSettings/ProjectSettings.asset').write_text('m_ActiveColorSpace: 0\n')
            (base/'source.cs').write_text('// synthetic immutable source\n')
        self.manifest = dict(currentProject='project', sourceHashes={'source.cs':runner.digest(self.root/'source.cs')},
            sourcePlayerSettingsSha256=runner.digest(self.root/'ProjectSettings/ProjectSettings.asset'),
            runnerSha256=runner.digest(Path(runner.__file__)),colorSpace=0)
        self.config = runner.case('Synthetic','Synthetic','-report',1,['proof.png'])
        self.patch = mock.patch.object(runner,'ROOT',self.root);self.patch.start();self.addCleanup(self.patch.stop)
        self.active = mock.patch.object(runner,'unity_pids',return_value={});self.active.start();self.addCleanup(self.active.stop)
        self.fake = self.root / 'fake-editor'
        self.fake.write_text('#!'+sys.executable+'\n' + '''import json,pathlib,sys
args=sys.argv; path=pathlib.Path(args[args.index('-report')+1]); assert path.parent.is_dir(), 'runner must create the screenshot directory before the validator'
(path.parent/'proof.png').write_bytes(bytes.fromhex("''' + png().hex() + '''"))
path.write_text(json.dumps(dict(assertions=4,passed=1,failed=0,errors=[],clicks=1,raycasts=1,captures=['proof.png'],checks=[dict(name='synthetic',status='pass')])))
'''); self.fake.chmod(0o700)

    def test_fake_subprocess_success_requires_report_png_and_unchanged_sources(self):
        with mock.patch.dict(runner.CASES, {'synthetic':self.config}):
            result=runner.run_case(self.bundle,self.manifest,'synthetic',self.fake,10)
        self.assertEqual(result['status'],'PASS');self.assertEqual(result['screenshots']['proof.png']['width'],32)
        self.assertEqual(result['sourceMismatches'],[]);self.assertEqual(result['ownedProcessesRemaining'],[])
        self.assertEqual(result['home'],'/home/codex')
        with mock.patch.dict(runner.CASES, {'synthetic':self.config}),self.assertRaisesRegex(ValueError,'already has evidence'):
            runner.run_case(self.bundle,self.manifest,'synthetic',self.fake,10)

    def test_changed_source_fails_before_launch(self):
        (self.root/'source.cs').write_text('// changed synthetic source')
        with mock.patch.dict(runner.CASES, {'synthetic':self.config}),self.assertRaisesRegex(ValueError,'sources no longer match'):
            runner.run_case(self.bundle,self.manifest,'synthetic',self.fake,10)
        self.assertFalse((self.bundle/'runs/synthetic').exists())

    def test_timeout_terminates_owned_process(self):
        self.fake.write_text('#!'+sys.executable+'\nimport signal,time\nsignal.signal(signal.SIGTERM,signal.SIG_IGN)\nwhile True:time.sleep(.1)\n')
        with mock.patch.dict(runner.CASES, {'synthetic':self.config}):
            result=runner.run_case(self.bundle,self.manifest,'synthetic',self.fake,.15)
        self.assertEqual(result['status'],'FAIL');self.assertIn('TimeoutError',result['error'])
        self.assertEqual(result['ownedProcessesRemaining'],[])

    def test_incomplete_reports_and_escaped_captures_fail(self):
        output=self.bundle/'report';output.mkdir();path=output/'report.json'
        report=dict(assertions=4,passed=1,failed=0,errors=[],clicks=1,raycasts=1,captures=['proof.png'],checks=[dict(name='synthetic',status='pass')])
        (output/'proof.png').write_bytes(png());path.write_text(json.dumps(report));runner.validate_report(self.config,path)
        for key,value in [('assertions',0),('failed',1),('clicks',0),('checks',[]),('captures',[])]:
            invalid={**report,key:value};path.write_text(json.dumps(invalid))
            with self.subTest(key=key),self.assertRaises(ValueError):runner.validate_report(self.config,path)
        path.write_text(json.dumps({**report,'captures':['../proof.png']}))
        with self.assertRaisesRegex(ValueError,'escaped'):runner.validate_report(self.config,path)

    def test_cli_creates_lock_directory_in_a_fresh_clone(self):
        self.assertFalse((self.root/'Library').exists())
        manifest={**self.manifest,'sourceRoot':str(self.root),'company':runner.COMPANY}
        (self.bundle/'manifest.json').write_text(json.dumps(manifest))
        args=['test-integrated-unity.py','--prepared',str(self.bundle),'--case','daily','--unity',str(self.fake)]
        with mock.patch.object(sys,'argv',args),mock.patch.dict(os.environ,{'HOME':'/home/codex'}), \
             mock.patch.object(runner.pwd,'getpwuid',return_value=mock.Mock(pw_name='codex',pw_dir='/home/codex')), \
             mock.patch.object(runner,'run_case',return_value={'status':'PASS'}) as execute:
            self.assertEqual(runner.main(),0)
            execute.assert_called_once()
        self.assertTrue((self.root/'Library/capstone-editor.lock').is_file())

    def test_snapshot_mode_still_rejects_copied_source_changes(self):
        (self.root/'source.cs').write_text('// concurrent original work')
        self.assertEqual(runner.verify_sources(self.bundle,self.manifest,snapshot_only=True),[])
        self.assertIn('source:source.cs',runner.verify_sources(self.bundle,self.manifest))
        (self.project/'source.cs').write_text('// changed frozen copy')
        self.assertIn('copy:source.cs',runner.verify_sources(self.bundle,self.manifest,snapshot_only=True))

    def test_only_generated_font_feature_table_is_restored_with_evidence(self):
        baseline=self.bundle/'baseline'/runner.FONT_ASSET;target=self.project/runner.FONT_ASSET
        original='  m_Name: Synthetic font\n  m_FontFeatureTable:\n    entries: []\n  m_ShouldReimportFontFeatures: 0\n'
        for p in [baseline,target]:p.parent.mkdir(parents=True,exist_ok=True);p.write_text(original)
        self.manifest['sourceHashes'][runner.FONT_ASSET]=runner.digest(baseline)
        target.write_text(original.replace('entries: []','entries: [synthetic]'))
        result=runner.restore_import_cache(self.bundle,self.manifest,self.bundle/'font-run')
        self.assertEqual(len(result),1);self.assertEqual(target.read_text(),original)
        self.assertTrue(Path(result[0]['retainedCopy']).is_file())
        target.write_text(original.replace('Synthetic font','Unexpected font'))
        with self.assertRaisesRegex(ValueError,'extends beyond'):runner.restore_import_cache(self.bundle,self.manifest,self.bundle/'font-run-2')

    def test_truncated_png_and_linked_boundary_are_rejected(self):
        path=self.bundle/'bad.png';path.write_bytes(png()[:-6])
        with self.assertRaises(ValueError):runner.png_info(path)
        link=self.root/'artifacts/linked';link.symlink_to(self.bundle,target_is_directory=True)
        with self.assertRaisesRegex(ValueError,'Linked'):runner.boundary(link/'nested')


if __name__ == '__main__':unittest.main()
