package com.capstonedesign2026.qa.verifier;

import android.util.Xml;
import java.nio.charset.StandardCharsets;
import org.junit.Test;
import org.junit.runner.RunWith;
import org.robolectric.RobolectricTestRunner;
import org.robolectric.annotation.Config;
import org.xmlpull.v1.XmlPullParser;
import static org.junit.Assert.*;

/** Real Android parser defaults, which differ from the plain kxml2 host parser. */
@RunWith(RobolectricTestRunner.class)
@Config(sdk = 33, manifest = Config.NONE)
public final class QaAndroidParserTest {
    private static final String QUEUE = "<EventQueueState xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" "
        + "xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"><SchemaVersion>1</SchemaVersion><Enabled>true</Enabled>"
        + "<DeletePending>false</DeletePending><EnrollmentRevoked>false</EnrollmentRevoked>"
        + "<EnrollmentFingerprint>aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa</EnrollmentFingerprint>"
        + "<ConsentStartedUtcMs>100</ConsentStartedUtcMs><MeasurementConsentUtcMs>120</MeasurementConsentUtcMs>"
        + "<Events/><Recorded/></EventQueueState>";

    @Test public void androidDefaultIsDisabledBeforeReadingValidQueue() throws Exception {
        XmlPullParser parser = Xml.newPullParser();
        assertTrue("Android enables document-declaration processing by default", parser.getFeature(XmlPullParser.FEATURE_PROCESS_DOCDECL));
        QaStateChecks.QueueState saved = QaStateChecks.queue(QUEUE.getBytes(StandardCharsets.UTF_8), parser);
        assertFalse(parser.getFeature(XmlPullParser.FEATURE_PROCESS_DOCDECL));
        assertTrue(saved.enabled);
        assertEquals(120, saved.measurementConsent);
    }

    @Test public void dotNetBomAndNamespaceDeclarationAreAcceptedOnAndroid() throws Exception {
        String document = "\ufeff<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + QUEUE;
        QaStateChecks.QueueState saved = QaStateChecks.queue(document.getBytes(StandardCharsets.UTF_8), Xml.newPullParser());
        assertEquals(1, saved.schema);
        assertFalse(saved.deletePending);
    }

    @Test public void documentTypesAndEntitiesRemainRejectedOnAndroid() throws Exception {
        for (String declaration : new String[] {
            "<!DOCTYPE EventQueueState>",
            "<!DOCTYPE EventQueueState [<!ENTITY value '120'>]>",
            "<!DOCTYPE EventQueueState SYSTEM 'https://example.invalid/never-requested.dtd'>"
        }) {
            try {
                QaStateChecks.queue((declaration + QUEUE).getBytes(StandardCharsets.UTF_8), Xml.newPullParser());
                fail("Document declaration was accepted");
            } catch (java.io.IOException | org.xmlpull.v1.XmlPullParserException expected) { }
        }
    }
}
