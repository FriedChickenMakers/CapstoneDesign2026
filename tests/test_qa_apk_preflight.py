"""Synthetic metadata; the offline gate never touches a device or an enrollment."""
import importlib.util
import contextlib
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import warnings
import zipfile
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'scripts'))
spec=importlib.util.spec_from_file_location('qa_apk_preflight',ROOT/'scripts/check-qa-apk.py')
gate=importlib.util.module_from_spec(spec);spec.loader.exec_module(gate)

class QaApkPreflightTests(unittest.TestCase):
    def info(self):
        return dict(package='org.example.syntheticqa',versionCode=2,versionName='0.3.0-qa.1',debuggable=True,
                    minSdk=29,targetSdk=36,certificateSha256=['a'*64],sha256='b'*64,bytes=123,
                    forbiddenBundledFiles=[])
    def check(self,info,previous=None,distribution='managed-qa'):
        return gate.evaluate(info,'org.example.syntheticqa','0.3.0-qa.1',distribution,previous)
    def test_matching_managed_package_and_stable_update_are_accepted(self):
        current=self.info();old=dict(current,versionCode=1,sha256='c'*64)
        self.assertEqual(self.check(current,old),[])
        self.assertEqual(self.check(current,current),[])
    def test_wrong_package_signer_and_downgrade_are_rejected_before_adb(self):
        current=self.info();old=dict(current,package='org.example.other',versionCode=3,certificateSha256=['d'*64])
        errors=self.check(current,old)
        self.assertIn('update-package-changed',errors)
        self.assertIn('update-signing-certificate-changed',errors)
        self.assertIn('update-version-downgrade',errors)
    def test_changed_bytes_with_same_version_code_are_not_a_frozen_qa_build(self):
        current=self.info();old=dict(current,sha256='c'*64)
        self.assertIn('changed-build-needs-incremented-version-code',self.check(current,old))
    def test_non_debuggable_app_does_not_claim_operator_enrollment_works(self):
        self.assertIn('operator-run-as-enrollment-unavailable',self.check(dict(self.info(),debuggable=False)))
        errors=self.check(dict(self.info(),debuggable=False),distribution='release')
        self.assertIn('nondebug-enrollment-provisioning-not-implemented',errors)
    def test_bootstrap_to_final_requires_debug_transition_and_stable_identity(self):
        previous=dict(self.info(),versionCode=1,sha256="c"*64)
        final=dict(self.info(),debuggable=False)
        self.assertEqual(self.check(final,previous,"bootstrap-final"),[])
        self.assertIn("bootstrap-apk-required",self.check(final,None,"bootstrap-final"))
        self.assertIn("final-qa-is-debuggable",self.check(self.info(),previous,"bootstrap-final"))
        self.assertIn("previous-apk-is-not-debuggable-bootstrap",self.check(final,dict(previous,debuggable=False),"bootstrap-final"))
        self.assertIn("update-signing-certificate-changed",self.check(final,dict(previous,certificateSha256=["d"*64]),"bootstrap-final"))
        self.assertIn("changed-build-needs-incremented-version-code",self.check(final,dict(previous,versionCode=2),"bootstrap-final"))
    def test_bundled_enrollment_private_key_or_generic_version_is_blocked(self):
        for name in ['assets/personalization-enrollment.json','assets/signing.jks']:
            self.assertIn('enrollment-or-private-key-bundled',self.check(dict(self.info(),forbiddenBundledFiles=[name])))
        self.assertIn('qa-build-needs-distinct-version-name',self.check(dict(self.info(),versionName='0.1.0-mockup')))
    def test_tool_metadata_parsing_keeps_digest_not_certificate_subject(self):
        info=gate.parse_metadata("package: name='org.example.syntheticqa' versionCode='2' versionName='0.3.0-qa.1'\nsdkVersion:'29'\ntargetSdkVersion:'36'\napplication-debuggable\n",
            'Signer #1 certificate DN: CN=Synthetic fixture\nSigner #1 certificate SHA-256 digest: '+'a'*64+'\n')
        self.assertTrue(info['debuggable']);self.assertEqual(info['certificateSha256'],['a'*64])
        self.assertNotIn('Synthetic fixture',str(info))
        with self.assertRaises(gate.PreflightError):gate.parse_metadata('invalid','invalid')
    def test_verifier_must_share_the_final_candidate_signer_and_fixed_target(self):
        candidate=dict(self.info(),package='com.capstonedesign2026.qa',debuggable=False)
        verifier=dict(self.info(),package='com.capstonedesign2026.qa.verifier')
        self.assertEqual(gate.evaluate_verifier(candidate,verifier),[])
        self.assertIn('qa-verifier-apk-required',gate.evaluate_verifier(candidate,None))
        self.assertIn('verifier-signing-certificate-mismatch',gate.evaluate_verifier(candidate,dict(verifier,certificateSha256=['c'*64])))
        self.assertIn('verifier-fixed-target-mismatch',gate.evaluate_verifier(candidate,dict(verifier,package='org.example.other')))
        self.assertIn('verifier-bundles-enrollment-or-private-key',gate.evaluate_verifier(candidate,dict(verifier,forbiddenBundledFiles=['assets/enrollments.json'])))

    def automatic(self, payload=None, entries=None):
        if entries is None:
            entries=[(gate.AUTOMATIC_CONFIG_ASSET,json.dumps(payload).encode())]
        data=io.BytesIO()
        with warnings.catch_warnings():
            warnings.simplefilter('ignore',UserWarning) # Deliberate duplicate ZIP fixture.
            with zipfile.ZipFile(data,'w',compression=zipfile.ZIP_DEFLATED) as archive:
                for name,body in entries:archive.writestr(name,body)
        data.seek(0)
        with zipfile.ZipFile(data) as archive:return gate.automatic_configuration(archive)

    def config(self):return {'endpoint':'https://telemetry.example.test','automaticEnrollmentKey':'e'*64}

    def test_missing_automatic_config_preserves_manual_but_required_release_is_blocked(self):
        missing=self.automatic(entries=[]);self.assertEqual(missing,{'present':False,'valid':False,'errors':[]})
        self.assertEqual(self.check(dict(self.info(),automaticEnrollment=missing)),[])
        errors=gate.evaluate(dict(self.info(),debuggable=False,automaticEnrollment=missing),'org.example.syntheticqa','0.3.0-qa.1','release',require_automatic_enrollment=True)
        self.assertIn('automatic-enrollment-config-required',errors)

    def test_only_explicit_valid_automatic_release_passes_and_contains_no_registration_secret(self):
        automatic=self.automatic(self.config());self.assertEqual(automatic,{'present':True,'valid':True,'errors':[]})
        info=dict(self.info(),debuggable=False,automaticEnrollment=automatic)
        self.assertEqual(gate.evaluate(info,'org.example.syntheticqa','0.3.0-qa.1','release',require_automatic_enrollment=True),[])
        self.assertIn('nondebug-enrollment-provisioning-not-implemented',self.check(info,distribution='release'))
        rendered=json.dumps(automatic)
        self.assertNotIn(self.config()['endpoint'],rendered);self.assertNotIn(self.config()['automaticEnrollmentKey'],rendered)

    def test_exact_config_fields_exclude_participant_tokens_subjects_and_extra_fields(self):
        candidates=[{},[],None,dict(self.config(),token=''),dict(self.config(),subject='synthetic'),dict(self.config(),participantId='synthetic'),dict(self.config(),extra=True)]
        for config in candidates:
            with self.subTest(type=type(config).__name__):
                automatic=self.automatic(config);self.assertFalse(automatic['valid']);self.assertTrue(automatic['errors'])
                self.assertTrue(self.check(dict(self.info(),automaticEnrollment=automatic)))

    def test_key_requires_exact_lowercase_hex_string(self):
        for key in ['a'*63,'a'*65,'A'*64,'g'*64,'a'*64+'\n',None,64,True]:
            with self.subTest(type=type(key).__name__):
                automatic=self.automatic(dict(self.config(),automaticEnrollmentKey=key))
                self.assertIn('automatic-enrollment-config-key-invalid',automatic['errors']);self.assertFalse(automatic['valid'])

    def test_https_origin_rejects_path_auth_query_fragment_port_and_parser_whitespace(self):
        invalid=['http://telemetry.example.test','https://user:pass@telemetry.example.test','https://telemetry.example.test/path',
                 'https://telemetry.example.test?x=1','https://telemetry.example.test?','https://telemetry.example.test#',
                 'https:///','https://telemetry.example.test:99999','https://telemetry.example.test:invalid','https://telemetry.example.test:',
                 '\nhttps://telemetry.example.test','https://telemetry.example.test\n','https://telemetry.example.test\\path','https://[broken']
        for endpoint in invalid:
            with self.subTest(endpoint=endpoint):
                automatic=self.automatic(dict(self.config(),endpoint=endpoint));self.assertFalse(automatic['valid']);self.assertTrue(automatic['errors'])
                self.assertTrue(self.check(dict(self.info(),automaticEnrollment=automatic)))
        for endpoint in ['https://telemetry.example.test/','https://telemetry.example.test:8443','https://[::1]:8443']:
            self.assertTrue(self.automatic(dict(self.config(),endpoint=endpoint))['valid'])

    def test_duplicate_json_fields_bad_encoding_and_bad_json_have_only_generic_errors(self):
        for raw in [b'\xff',b'{"automaticEnrollmentKey":"synthetic-secret-fragment"',
                    ('{"endpoint":"https://telemetry.example.test","automaticEnrollmentKey":"'+'a'*64+'","automaticEnrollmentKey":"'+'b'*64+'"}').encode()]:
            automatic=self.automatic(entries=[(gate.AUTOMATIC_CONFIG_ASSET,raw)])
            self.assertFalse(automatic['valid']);self.assertIn('automatic-enrollment-config-unreadable',automatic['errors'])
            self.assertNotIn('synthetic-secret-fragment',json.dumps(automatic))

    def test_duplicate_zip_asset_and_unexpected_location_are_blocked(self):
        raw=json.dumps(self.config()).encode()
        automatic=self.automatic(entries=[(gate.AUTOMATIC_CONFIG_ASSET,raw)]*2)
        self.assertIn('automatic-enrollment-config-duplicate',automatic['errors'])
        for name in ['assets/other/capstone-enrollment-config.json','assets/Capstone-Enrollment-Config.json',
                     'assets\\capstone-enrollment-config.json','./assets/capstone-enrollment-config.json',gate.AUTOMATIC_CONFIG_ASSET+'/']:
            automatic=self.automatic(entries=[(name,raw)])
            self.assertIn('automatic-enrollment-config-unexpected-path',automatic['errors'])
            self.assertTrue(self.check(dict(self.info(),automaticEnrollment=automatic)))

    def test_config_size_is_bounded_before_decompression_and_accepts_exact_limit(self):
        raw=json.dumps(self.config()).encode()
        boundary=raw+b' '*(4096-len(raw))
        self.assertTrue(self.automatic(entries=[(gate.AUTOMATIC_CONFIG_ASSET,boundary)])['valid'])
        oversized=self.automatic(entries=[(gate.AUTOMATIC_CONFIG_ASSET,boundary+b' ')])
        self.assertIn('automatic-enrollment-config-too-large',oversized['errors'])

    def test_inspect_reports_booleans_not_config_contents_and_retains_forbidden_file_gate(self):
        with tempfile.TemporaryDirectory() as folder:
            apk=Path(folder)/'synthetic.apk'
            with zipfile.ZipFile(apk,'w') as archive:
                archive.writestr(gate.AUTOMATIC_CONFIG_ASSET,json.dumps(self.config()))
                archive.writestr('assets/personalization-enrollment.json','{"token":"synthetic-private"}')
            with patch.object(gate,'command',return_value='synthetic metadata'),patch.object(gate,'parse_metadata',return_value=self.info()):
                info=gate.inspect(apk,'unused','unused')
            self.assertTrue(info['automaticEnrollment']['valid'])
            self.assertIn('enrollment-or-private-key-bundled',self.check(info))
            self.assertNotIn(self.config()['endpoint'],json.dumps(info));self.assertNotIn(self.config()['automaticEnrollmentKey'],json.dumps(info))

    def test_cli_flag_switches_operational_check_without_emitting_key(self):
        with tempfile.TemporaryDirectory() as folder:
            info=dict(self.info(),debuggable=False,automaticEnrollment=self.automatic(self.config()))
            arguments=['check-qa-apk','--apk','synthetic.apk','--expected-package',info['package'],
                       '--expected-version',info['versionName'],'--distribution','release','--require-automatic-enrollment','--output',folder]
            output=io.StringIO()
            with patch.object(sys,'argv',arguments),patch.object(gate,'inspect',return_value=info),contextlib.redirect_stdout(output):
                self.assertEqual(gate.main(),0)
            report=json.loads((Path(folder)/'result.json').read_text())
            self.assertTrue(report['automaticEnrollmentRequired']);self.assertIn('automatic first-LIVE',report['operationalChecksStillRequired'][0])
            self.assertNotIn('bootstrap provisioning',report['operationalChecksStillRequired'][0])
            self.assertNotIn(self.config()['automaticEnrollmentKey'],json.dumps(report)+output.getvalue())

if __name__=='__main__':unittest.main()
