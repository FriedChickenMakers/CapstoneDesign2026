package com.capstonedesign2026.qa.verifier;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.security.MessageDigest;
import java.nio.charset.StandardCharsets;
import org.xmlpull.v1.XmlPullParser;

/** Read-only checks. No raw input or exception message is returned to the operator. */
final class QaStateChecks {
    private QaStateChecks() { }

    static byte[] readBounded(File file, int maximum) throws IOException {
        if (!file.isFile() || file.length() <= 0 || file.length() > maximum)
            throw new IOException("Invalid file size");
        try (FileInputStream input = new FileInputStream(file);
             ByteArrayOutputStream output = new ByteArrayOutputStream()) {
            byte[] chunk = new byte[4096];
            for (int count; (count = input.read(chunk)) != -1;) {
                if (output.size() + count > maximum) throw new IOException("Input limit exceeded");
                output.write(chunk, 0, count);
            }
            return output.toByteArray();
        }
    }

    static boolean hashMatches(byte[] bytes, String expected) throws Exception {
        if (expected == null || !expected.matches("[a-fA-F0-9]{64}")) return false;
        byte[] wanted = new byte[32];
        for (int i = 0; i < wanted.length; i++)
            wanted[i] = (byte) Integer.parseInt(expected.substring(i * 2, i * 2 + 2), 16);
        return MessageDigest.isEqual(wanted, MessageDigest.getInstance("SHA-256").digest(bytes));
    }

    static final class QueueState {
        boolean enabled, deletePending, enrollmentRevoked;
        long consentStarted, measurementConsent;
        int schema;
        String enrollmentFingerprint;
    }

    static boolean bindingMatches(QueueState state, String endpoint, String token) throws Exception {
        if (endpoint == null || token == null || state.enrollmentFingerprint == null) return false;
        String normalized = endpoint.replaceAll("/+$", "");
        return hashMatches((normalized + "\n" + token).getBytes(StandardCharsets.UTF_8), state.enrollmentFingerprint);
    }

    static QueueState queue(byte[] bytes, XmlPullParser parser) throws Exception {
        // Android Xml.newPullParser enables DOCDECL by default. Disable it
        // before providing any bytes, then verify that it really is off. Plain
        // kxml2 already reports false and may reject setting this optional feature.
        if (parser.getFeature(XmlPullParser.FEATURE_PROCESS_DOCDECL))
            parser.setFeature(XmlPullParser.FEATURE_PROCESS_DOCDECL, false);
        if (parser.getFeature(XmlPullParser.FEATURE_PROCESS_DOCDECL)) throw new IOException("Unsafe parser");
        // Unity/Mono XmlWriter may emit a UTF-8 BOM. Some Android pull-parser
        // implementations retain it when an explicit encoding is supplied.
        int offset=bytes.length>=3 && (bytes[0]&255)==0xef && (bytes[1]&255)==0xbb && (bytes[2]&255)==0xbf ? 3 : 0;
        parser.setInput(new ByteArrayInputStream(bytes, offset, bytes.length-offset), "UTF-8");
        QueueState result = new QueueState();
        int seen = 0;
        boolean root = false;
        for (int token = parser.nextToken(); token != XmlPullParser.END_DOCUMENT; token = parser.nextToken()) {
            if (token == XmlPullParser.DOCDECL) throw new IOException("Document types forbidden");
            if (token != XmlPullParser.START_TAG) continue;
            if (parser.getDepth() == 1) {
                if (root || !"EventQueueState".equals(parser.getName())) throw new IOException("Unexpected root");
                root = true;
                continue;
            }
            if (parser.getDepth() != 2) continue;
            String name = parser.getName();
            int bit;
            switch (name) {
                case "SchemaVersion": bit = 1; break;
                case "Enabled": bit = 2; break;
                case "ConsentStartedUtcMs": bit = 4; break;
                case "MeasurementConsentUtcMs": bit = 8; break;
                case "DeletePending": bit = 16; break;
                case "EnrollmentRevoked": bit = 32; break;
                case "EnrollmentFingerprint": bit = 64; break;
                default: continue; // This is a registration-preservation check, not the full event validator.
            }
            if ((seen & bit) != 0) throw new IOException("Duplicate field");
            seen |= bit;
            String value = parser.nextText().trim();
            if (bit == 64) {
                if (!value.matches("[a-fA-F0-9]{64}")) throw new IOException("Invalid binding");
                result.enrollmentFingerprint = value;
            } else if (bit == 2 || bit == 16 || bit == 32) {
                if (!"true".equals(value) && !"false".equals(value)) throw new IOException("Invalid boolean");
                boolean flag = "true".equals(value);
                if (bit == 2) result.enabled = flag;
                else if (bit == 16) result.deletePending = flag;
                else result.enrollmentRevoked = flag;
            } else {
                if (!value.matches("[0-9]{1,19}")) throw new IOException("Invalid number");
                long number = Long.parseLong(value);
                if (bit == 1) {
                    if (number != 1) throw new IOException("Unsupported schema");
                    result.schema = 1;
                } else if (bit == 4) result.consentStarted = number;
                else result.measurementConsent = number;
            }
        }
        if (!root || seen != 127) throw new IOException("Missing field");
        return result;
    }
}
