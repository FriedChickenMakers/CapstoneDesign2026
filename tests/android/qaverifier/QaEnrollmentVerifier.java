package com.capstonedesign2026.qa.verifier;

import android.app.Activity;
import android.app.Instrumentation;
import android.content.Context;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageInfo;
import android.os.Bundle;
import android.util.Xml;
import java.io.File;
import java.nio.charset.StandardCharsets;
import org.json.JSONObject;

/** Fixed QA-package verifier; never starts an Activity, writes data or makes a network request. */
public final class QaEnrollmentVerifier extends Instrumentation {
    private static final String TARGET = "com.capstonedesign2026.qa";
    private Bundle arguments;

    @Override public void onCreate(Bundle supplied) {
        super.onCreate(supplied);
        arguments = supplied == null ? new Bundle() : new Bundle(supplied);
        start();
    }

    @Override public void onStart() {
        Bundle result = new Bundle();
        result.putInt("protocolVersion", 1);
        result.putBoolean("allPassed", false);
        int failure = 1;
        try {
            String expectedHash = arguments.getString("expectedSha256");
            String version = arguments.getString("expectedVersionCode");
            String boundary = arguments.getString("expectedMeasurementConsentUtcMs");
            String enabled = arguments.getString("expectedEnabled");
            boolean valid = expectedHash != null && expectedHash.matches("[a-fA-F0-9]{64}")
                && version != null && version.matches("[1-9][0-9]{0,9}")
                && boundary != null && boundary.matches("[1-9][0-9]{0,18}")
                && "true".equals(enabled); // This verifier only checks active QA enrollment.
            if (!valid) throw new IllegalArgumentException();
            long expectedVersion = Long.parseLong(version), expectedBoundary = Long.parseLong(boundary);
            if (expectedVersion > Integer.MAX_VALUE) throw new IllegalArgumentException();
            result.putBoolean("argumentsValid", true);

            failure = 2;
            Context target = getTargetContext();
            boolean packageMatches = target != null && TARGET.equals(target.getPackageName());
            result.putBoolean("targetPackageMatches", packageMatches);
            if (!packageMatches) throw new IllegalStateException();
            PackageInfo info = target.getPackageManager().getPackageInfo(TARGET, 0);
            result.putLong("targetVersionCode", info.getLongVersionCode());
            result.putBoolean("versionMatches", info.getLongVersionCode() == expectedVersion);
            failure = 3;
            if (info.getLongVersionCode() != expectedVersion) throw new IllegalStateException();
            boolean nonDebug = (target.getApplicationInfo().flags & ApplicationInfo.FLAG_DEBUGGABLE) == 0;
            result.putBoolean("targetNonDebuggable", nonDebug);
            failure = 4;
            if (!nonDebug) throw new IllegalStateException();

            // Package/version checks precede all private-file access. Canonical
            // containment rejects symlinks pointing outside this app's files directory.
            File directory = target.getFilesDir().getCanonicalFile();
            File enrollment = new File(directory, "personalization-enrollment.json").getCanonicalFile();
            File queue = new File(directory, "behavior-queue.xml").getCanonicalFile();
            failure = 5;
            if (!directory.equals(enrollment.getParentFile()) || !directory.equals(queue.getParentFile()))
                throw new IllegalStateException();
            result.putBoolean("enrollmentExists", enrollment.isFile());
            result.putBoolean("behaviorQueueExists", queue.isFile());
            byte[] enrollmentBytes = QaStateChecks.readBounded(enrollment, 1024 * 1024);
            failure = 6;
            boolean hashMatches = QaStateChecks.hashMatches(enrollmentBytes, expectedHash);
            result.putBoolean("enrollmentHashMatches", hashMatches);
            if (!hashMatches) throw new IllegalStateException();

            failure = 7;
            QaStateChecks.QueueState saved = QaStateChecks.queue(
                QaStateChecks.readBounded(queue, 4 * 1024 * 1024), Xml.newPullParser());
            JSONObject registration = new JSONObject(new String(enrollmentBytes, StandardCharsets.UTF_8));
            boolean bound = QaStateChecks.bindingMatches(saved, registration.getString("endpoint"), registration.getString("token"));
            result.putBoolean("queueEnrollmentMatches", bound);
            if (!bound) throw new IllegalStateException();
            result.putBoolean("queueSchemaSupported", saved.schema == 1);
            result.putBoolean("queueEnabledMatches", saved.enabled == Boolean.parseBoolean(enabled));
            result.putBoolean("measurementConsentMatches", saved.measurementConsent == expectedBoundary);
            result.putBoolean("consentBoundaryValid", saved.consentStarted > 0 && saved.measurementConsent >= saved.consentStarted);
            result.putBoolean("queueNotRevoked", !saved.deletePending && !saved.enrollmentRevoked);
            failure = 8;
            if (saved.enabled != Boolean.parseBoolean(enabled) || saved.measurementConsent != expectedBoundary
                || saved.consentStarted <= 0 || saved.measurementConsent < saved.consentStarted
                || saved.deletePending || saved.enrollmentRevoked) throw new IllegalStateException();
            result.putBoolean("allPassed", true);
            failure = 0;
        } catch (Exception ignored) {
            // Never emit the exception, source bytes, file contents or credentials.
        }
        result.putInt("failureCode", failure);
        finish(failure == 0 ? Activity.RESULT_OK : Activity.RESULT_CANCELED, result);
    }
}
