package com.capstonedesign2026.qa.verifier;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import org.junit.Test;
import org.kxml2.io.KXmlParser;
import static org.junit.Assert.*;

public final class QaStateChecksTest {
    private static final String QUEUE = "<EventQueueState><SchemaVersion>1</SchemaVersion><Enabled>true</Enabled>"
        + "<ConsentStartedUtcMs>100</ConsentStartedUtcMs><MeasurementConsentUtcMs>120</MeasurementConsentUtcMs>"
        + "<DeletePending>false</DeletePending><EnrollmentRevoked>false</EnrollmentRevoked>"
        + "<EnrollmentFingerprint>aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa</EnrollmentFingerprint><Events /></EventQueueState>";

    private QaStateChecks.QueueState parse(String xml) throws Exception {
        return QaStateChecks.queue(xml.getBytes(StandardCharsets.UTF_8), new KXmlParser());
    }
    private void reject(String xml) throws Exception {
        try { parse(xml); fail("Malformed state was accepted"); } catch (Exception expected) { }
    }
    @Test public void exactBoundaryAndFlags() throws Exception {
        QaStateChecks.QueueState state = parse(QUEUE);
        assertTrue(state.enabled); assertFalse(state.deletePending); assertFalse(state.enrollmentRevoked);
        assertEquals(1, state.schema); assertEquals(100, state.consentStarted); assertEquals(120, state.measurementConsent);
    }
    @Test public void duplicatesMissingAndNestedFieldsAreNotAccepted() throws Exception {
        reject(QUEUE.replace("</EventQueueState>", "<Enabled>false</Enabled></EventQueueState>"));
        reject(QUEUE.replace("<Enabled>true</Enabled>", "<Events><Enabled>true</Enabled></Events>"));
        reject(QUEUE.replace("<MeasurementConsentUtcMs>120</MeasurementConsentUtcMs>", ""));
    }
    @Test public void unsafeOrInvalidXmlFailsClosed() throws Exception {
        reject("<!DOCTYPE EventQueueState [<!ENTITY secret SYSTEM 'file:///does-not-exist'>]>" + QUEUE);
        reject(QUEUE.replace("<Enabled>true</Enabled>", "<Enabled>1</Enabled>"));
        reject(QUEUE.replace("<SchemaVersion>1", "<SchemaVersion>2"));
        reject(QUEUE.replace(">120<", ">9223372036854775808<"));
        reject(QUEUE.substring(0, QUEUE.length() - 4));
    }
    @Test public void enrollmentHashRequiresExactBytes() throws Exception {
        byte[] bytes = "abc".getBytes(StandardCharsets.UTF_8);
        assertTrue(QaStateChecks.hashMatches(bytes, "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
        assertFalse(QaStateChecks.hashMatches("abcd".getBytes(StandardCharsets.UTF_8), "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
        assertFalse(QaStateChecks.hashMatches(bytes, null));
        assertFalse(QaStateChecks.hashMatches(bytes, "not-a-hash"));
    }
    @Test public void boundedReaderPreservesSourceBytes() throws Exception {
        Path file = Files.createTempFile("synthetic-qa-verifier-", ".bin");
        byte[] bytes = new byte[]{1, 2, 3, 4};
        try {
            Files.write(file, bytes);
            assertArrayEquals(bytes, QaStateChecks.readBounded(file.toFile(), 4));
            try { QaStateChecks.readBounded(file.toFile(), 3); fail("Oversized input accepted"); } catch (java.io.IOException expected) { }
            assertArrayEquals(bytes, Files.readAllBytes(file));
        } finally { Files.delete(file); }
    }
    @Test public void bindingMustMatchExactRegistrationAndNormalizesTrailingSlash() throws Exception {
        String endpoint="https://example.invalid", token="synthetic-enrollment-token-for-verifier-test-only";
        byte[] digest=java.security.MessageDigest.getInstance("SHA-256").digest((endpoint+"\n"+token).getBytes(StandardCharsets.UTF_8));
        StringBuilder hex=new StringBuilder();for(byte b:digest)hex.append(String.format("%02x",b&255));
        QaStateChecks.QueueState state=parse(QUEUE.replace("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",hex.toString()));
        assertTrue(QaStateChecks.bindingMatches(state,endpoint,token));
        assertTrue(QaStateChecks.bindingMatches(state,endpoint+"/",token));
        assertFalse(QaStateChecks.bindingMatches(state,endpoint,token+"-different"));
        assertFalse(QaStateChecks.bindingMatches(state,"https://other.invalid",token));
        reject(QUEUE.replace("<EnrollmentFingerprint>aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa</EnrollmentFingerprint>",""));
    }
    @Test public void dotNetXmlDeclarationNamespacesAndBomAreAccepted() throws Exception {
        String xml="\ufeff<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"+QUEUE.replace("<EventQueueState>",
            "<EventQueueState xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">");
        assertTrue(parse(xml).enabled);
    }
}
