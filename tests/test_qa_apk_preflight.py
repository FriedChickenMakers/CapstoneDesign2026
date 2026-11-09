"""Synthetic metadata; the offline gate never touches a device or an enrollment."""
import importlib.util
from pathlib import Path
import sys
import unittest
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

if __name__=='__main__':unittest.main()
