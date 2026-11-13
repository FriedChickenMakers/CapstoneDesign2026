package com.capstonedesign2026.platform

import android.content.Context
import android.content.ContextWrapper
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config
import java.io.File

/** Synthetic enrollments, controlled transport faults, actual AtomicFile outbox. */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [33], shadows = [AtomicFileRenameShadow::class])
class MeasurementUploadTest {
    private val context get() = RuntimeEnvironment.getApplication() as Context
    private val file get() = File(context.filesDir, "personalization-enrollment.json")
    private lateinit var binding: String
    private var since = 0L
    private fun epoch() = NativeLocalReset.epoch(context)
    private fun batch() = JSONObject(MeasurementTelemetry.batch(context, binding))
    private fun events() = batch().getJSONArray("events")
    private fun accepted(body: String) = JSONObject().put("accepted_event_ids", JSONArray().apply {
        val e = JSONObject(body).getJSONArray("events")
        for (i in 0 until e.length()) put(e.getJSONObject(i).getString("event_id"))
    }).toString()
    private fun event(n: Int) = JSONObject(MeasurementTelemetry.activity(context, "activity_started",
        n.toString(16).padStart(32, '0'), "free-mission:M01", since, 540)).getBoolean("ok")
    @Before fun setup() {
        context.getSharedPreferences("garden_activity_notifications", Context.MODE_PRIVATE).edit().putBoolean("live", true).commit()
        MeasurementTelemetry.configure(context, "", false, 0)
        File(context.filesDir, "personalization-local-reset").delete()
        file.writeText(JSONObject().put("endpoint", "https://synthetic.invalid").put("token", "synthetic-test-token-" + "x".repeat(32)).toString())
        binding = MeasurementUpload.enrollment(context)!!.binding
        MeasurementTelemetry.dataset = "synthetic"
        since = System.currentTimeMillis()
        MeasurementTelemetry.configure(context, binding, true, since)
    }
    @Test fun disconnectedAndServerErrorsPreserveExactEventsUntilSuccessfulRetry() {
        assertTrue(event(1)); assertTrue(event(2))
        val original = events().toString()
        assertEquals(MeasurementUpload.Outcome.RETRY, MeasurementUpload.upload(context, epoch()) { _, _ -> throw java.io.IOException("synthetic offline") })
        assertEquals(original, events().toString())
        assertEquals(MeasurementUpload.Outcome.RETRY, MeasurementUpload.upload(context, epoch()) { _, _ -> MeasurementUpload.Reply(503, "") })
        val other = object : ContextWrapper(context) { override fun getFilesDir() = File(context.cacheDir,"other-synthetic-outbox").apply { mkdirs() } }
        MeasurementTelemetry.configure(other, "", false, 0)
        MeasurementTelemetry.configure(context, binding, true, since)
        assertEquals(original, events().toString())
        assertEquals(MeasurementUpload.Outcome.COMPLETE, MeasurementUpload.upload(context, epoch()) { _, body ->
            assertEquals(original, JSONObject(body).getJSONArray("events").toString())
            MeasurementUpload.Reply(200, accepted(body))
        })
        assertEquals(0, events().length())
    }
    @Test fun partialAcknowledgementRemovesOnlyAcceptedIdsAndRetriesRemainder() {
        assertTrue(event(1)); assertTrue(event(2))
        val first = events().getJSONObject(0).getString("event_id")
        assertEquals(MeasurementUpload.Outcome.RETRY, MeasurementUpload.upload(context, epoch()) { _, _ ->
            MeasurementUpload.Reply(200, JSONObject().put("accepted_event_ids", JSONArray().put(first)).toString())
        })
        assertEquals(1, events().length()); assertNotEquals(first,events().getJSONObject(0).getString("event_id"))
    }
    @Test fun malformedUnknownAndRedirectAcknowledgementsNeverEraseTheOutbox() {
        assertTrue(event(1)); val before = events().toString()
        for (reply in listOf(MeasurementUpload.Reply(302, ""), MeasurementUpload.Reply(200, "bad-json"),
            MeasurementUpload.Reply(200, "{\"accepted_event_ids\":[\"${"f".repeat(32)}\"]}"))) {
            assertEquals(MeasurementUpload.Outcome.RETRY, MeasurementUpload.upload(context,epoch()) { _, _ -> reply })
            assertEquals(before, events().toString())
        }
    }
    @Test fun concurrentForegroundAckRemainsIdempotent() {
        assertTrue(event(1))
        assertEquals(MeasurementUpload.Outcome.COMPLETE, MeasurementUpload.upload(context,epoch()) { _, body ->
            val parsed=JSONObject(body);val ids=JSONObject(accepted(body)).getJSONArray("accepted_event_ids").toString()
            MeasurementTelemetry.acknowledge(context,binding,parsed.getString("collection_id"),ids,ids)
            MeasurementUpload.Reply(200,accepted(body))
        })
        assertEquals(0,events().length());assertTrue(event(1));assertEquals(0,events().length())
    }
    @Test fun withdrawalDuringNetworkCannotRestoreOrAcknowledgeNewCollection() {
        assertTrue(event(1));val old=batch().getString("collection_id")
        MeasurementUpload.upload(context,epoch()) { _, body ->
            MeasurementTelemetry.configure(context,"",false,0)
            MeasurementTelemetry.configure(context,binding,true,since)
            assertTrue(event(2))
            MeasurementUpload.Reply(200,accepted(body))
        }
        assertNotEquals(old,batch().getString("collection_id"));assertEquals(1,events().length())
    }
    @Test fun registrationChangeDuringRequestPreservesPendingOldEvidence() {
        assertTrue(event(1));val before=events().toString()
        assertEquals(MeasurementUpload.Outcome.INACTIVE, MeasurementUpload.upload(context,epoch()) { _, body ->
            file.writeText(JSONObject(file.readText()).put("token","synthetic-new-token-"+"y".repeat(32)).toString())
            MeasurementUpload.Reply(200,accepted(body))
        })
        assertEquals(before,events().toString())
    }
    @Test fun absentRegistrationMockResetOrCancellationCannotSend() {
        assertTrue(event(1));var requests=0
        val transport={ _:MeasurementUpload.Enrollment, _:String -> requests++;MeasurementUpload.Reply(200,"{}") }
        assertEquals(MeasurementUpload.Outcome.RETRY,MeasurementUpload.upload(context,epoch(),stopped={true},transport=transport))
        assertEquals(MeasurementUpload.Outcome.INACTIVE,MeasurementUpload.upload(context,"stale-epoch",transport=transport))
        context.getSharedPreferences("garden_activity_notifications",Context.MODE_PRIVATE).edit().putBoolean("live",false).commit()
        assertEquals(MeasurementUpload.Outcome.INACTIVE,MeasurementUpload.upload(context,epoch(),transport=transport))
        context.getSharedPreferences("garden_activity_notifications",Context.MODE_PRIVATE).edit().putBoolean("live",true).commit()
        file.delete()
        assertEquals(MeasurementUpload.Outcome.INACTIVE,MeasurementUpload.upload(context,epoch(),transport=transport))
        assertEquals(0,requests);assertEquals(1,events().length())
    }
    @Test fun batchesAreBoundedAndFinalHeartbeatConfirmsWholeQueue() {
        for(i in 1..401)assertTrue(event(i))
        var requests=0
        val send={ _:MeasurementUpload.Enrollment, body:String ->
            requests++;assertTrue(JSONObject(body).getJSONArray("events").length()<=100)
            MeasurementUpload.Reply(200,accepted(body))
        }
        assertEquals(MeasurementUpload.Outcome.RETRY,MeasurementUpload.upload(context,epoch(),transport=send))
        assertEquals(4,requests);assertEquals(1,events().length())
        assertEquals(MeasurementUpload.Outcome.COMPLETE,MeasurementUpload.upload(context,epoch()) { _, body ->
            assertTrue(JSONObject(body).getLong("complete_through_utc_ms")>=since)
            MeasurementUpload.Reply(200,accepted(body))
        })
        assertEquals(0,events().length())
    }
    @Test fun encryptedOriginOnlyAndNoEmbeddedOrUserinfoCredentials() {
        val original=JSONObject(file.readText())
        for(endpoint in listOf("http://synthetic.invalid","https://name:pass@synthetic.invalid","https://synthetic.invalid/path",
            "https://synthetic.invalid/?query=x","https://synthetic.invalid/#fragment","https:///")) {
            file.writeText(JSONObject(original.toString()).put("endpoint",endpoint).toString())
            assertNull(MeasurementUpload.enrollment(context))
        }
        file.writeText(original.toString());assertEquals(binding,MeasurementUpload.enrollment(context)!!.binding)
    }
    @Test fun appVisitIsRandomSessionDeduplicatedAndContainsNoUserContent() {
        val session="c".repeat(32)
        assertTrue(JSONObject(MeasurementTelemetry.appSession(context,session)).getBoolean("ok"))
        assertTrue(JSONObject(MeasurementTelemetry.appSession(context,session)).getBoolean("ok"))
        assertEquals(1,events().length());val event=events().getJSONObject(0)
        assertEquals("app_session_started",event.getString("kind"));assertEquals(session,event.getString("session_key"))
        assertTrue(event.getLong("elapsed_ms")>=0);assertEquals("",event.getString("activity_id"))
        assertFalse(event.has("note"));assertFalse(event.has("device_id"));assertFalse(event.has("screen"))
        MeasurementTelemetry.configure(context,"",false,0)
        assertFalse(JSONObject(MeasurementTelemetry.appSession(context,"d".repeat(32))).getBoolean("ok"))
    }
    @Test fun fullOutboxAppVisitReturnsTerminalCapacityRatherThanTransientStorageFailure() {
        for(i in 1..MeasurementBuffer.CAPACITY)assertTrue(event(i))
        val before=batch().getLong("dropped_attempts")
        val result=JSONObject(MeasurementTelemetry.appSession(context,"e".repeat(32)))
        assertFalse(result.getBoolean("ok"));assertEquals("capacity",result.getString("failure"))
        assertEquals(before+1,batch().getLong("dropped_attempts"))
        assertEquals(MeasurementBuffer.CAPACITY,JSONObject(File(context.filesDir,"measurement-outbox.json").readText()).getJSONArray("events").length())
    }
    @Test fun appVisitStorageFailureIsRetryableWithSameVisitKey() {
        val outbox=File(context.filesDir,"measurement-outbox.json");val saved=outbox.readBytes()
        assertTrue(outbox.delete());assertTrue(outbox.mkdir());File(outbox,"synthetic-blocker").writeText("fixture")
        val session="f".repeat(32)
        assertEquals("storage",JSONObject(MeasurementTelemetry.appSession(context,session)).getString("failure"))
        assertTrue(outbox.deleteRecursively());outbox.writeBytes(saved)
        assertTrue(JSONObject(MeasurementTelemetry.appSession(context,session)).getBoolean("ok"))
        assertEquals(1,events().length());assertEquals(session,events().getJSONObject(0).getString("session_key"))
    }
    private fun automatic() {
        file.writeText(JSONObject().put("endpoint", "https://synthetic.invalid")
            .put("token", "a".repeat(64)).put("automaticEnrollmentKey", "b".repeat(64)).toString())
        binding = MeasurementUpload.enrollment(context)!!.binding
        MeasurementTelemetry.configure(context, binding, true, since)
    }
    @Test fun automaticEnrollmentOnlyAfter401RetriesIdenticalBatchOnce() {
        automatic(); assertTrue(event(1)); val original=events().toString()
        val bodies=mutableListOf<String>();var registrations=0
        val outcome=MeasurementUpload.upload(context,epoch(),registrationTransport={ registration,version ->
            registrations++;assertEquals("a".repeat(64),registration.token)
            assertTrue(version.matches(Regex("[A-Za-z0-9._-]{1,48}")))
            MeasurementUpload.Reply(200,"{\"registered\":true}")
        }) { _,body ->
            bodies.add(body)
            assertEquals(original,JSONObject(body).getJSONArray("events").toString())
            assertFalse(body.contains("automaticEnrollmentKey"));assertFalse(body.contains("b".repeat(64)))
            if(bodies.size==1)MeasurementUpload.Reply(401,"") else MeasurementUpload.Reply(200,accepted(body))
        }
        assertEquals(MeasurementUpload.Outcome.COMPLETE,outcome);assertEquals(1,registrations)
        assertEquals(2,bodies.size);assertEquals(bodies[0],bodies[1]);assertEquals(0,events().length())
    }
    @Test fun manualAndAuthenticatedOrNon401AutomaticUploadsNeverRegister() {
        assertTrue(event(1));var registrations=0
        val register={ _:MeasurementUpload.Enrollment,_:String -> registrations++;MeasurementUpload.Reply(200,"{\"registered\":true}") }
        assertEquals(MeasurementUpload.Outcome.RETRY,MeasurementUpload.upload(context,epoch(),registrationTransport=register) { _,_ -> MeasurementUpload.Reply(401,"") })
        assertEquals(0,registrations);assertEquals(1,events().length())
        automatic();assertTrue(event(2));val before=events().toString()
        for(status in listOf(403,429,500,503,302)) {
            assertEquals(MeasurementUpload.Outcome.RETRY,MeasurementUpload.upload(context,epoch(),registrationTransport=register) { _,_ -> MeasurementUpload.Reply(status,"") })
            assertEquals(before,events().toString())
        }
        assertEquals(MeasurementUpload.Outcome.COMPLETE,MeasurementUpload.upload(context,epoch(),registrationTransport=register) { _,body -> MeasurementUpload.Reply(200,accepted(body)) })
        assertEquals(0,registrations)
    }
    @Test fun registrationFailuresAndLooseSuccessShapesPreserveExactOutbox() {
        automatic();assertTrue(event(1));val original=events().toString()
        val failures=listOf(MeasurementUpload.Reply(401,""),MeasurementUpload.Reply(403,""),MeasurementUpload.Reply(404,""),
            MeasurementUpload.Reply(429,""),MeasurementUpload.Reply(503,""),MeasurementUpload.Reply(302,""),
            MeasurementUpload.Reply(201,"{\"registered\":true}"),MeasurementUpload.Reply(200,"not-json"),
            MeasurementUpload.Reply(200,"{}"),MeasurementUpload.Reply(200,"{\"registered\":false}"),
            MeasurementUpload.Reply(200,"{\"registered\":\"true\"}"))
        for(reply in failures) {
            var sends=0
            assertEquals(MeasurementUpload.Outcome.RETRY,MeasurementUpload.upload(context,epoch(),registrationTransport={ _,_ -> reply }) { _,_ -> sends++;MeasurementUpload.Reply(401,"") })
            assertEquals(1,sends);assertEquals(original,events().toString())
        }
        assertEquals(MeasurementUpload.Outcome.RETRY,MeasurementUpload.upload(context,epoch(),registrationTransport={ _,_ -> throw java.io.IOException("synthetic offline") }) { _,_ -> MeasurementUpload.Reply(401,"") })
        assertEquals(original,events().toString())
    }
    @Test fun repeated401AfterRegistrationCannotLoopOrAcknowledge() {
        automatic();assertTrue(event(1));val original=events().toString();var registrations=0;var sends=0
        assertEquals(MeasurementUpload.Outcome.RETRY,MeasurementUpload.upload(context,epoch(),registrationTransport={ _,_ -> registrations++;MeasurementUpload.Reply(200,"{\"registered\":true}") }) { _,_ -> sends++;MeasurementUpload.Reply(401,"") })
        assertEquals(1,registrations);assertEquals(2,sends);assertEquals(original,events().toString())
    }
    @Test fun changedRegistrationBefore401PreventsEnrollmentRequest() {
        automatic();assertTrue(event(1));val original=events().toString();var registrations=0
        assertEquals(MeasurementUpload.Outcome.INACTIVE,MeasurementUpload.upload(context,epoch(),registrationTransport={ _,_ -> registrations++;MeasurementUpload.Reply(200,"{\"registered\":true}") }) { _,_ ->
            file.writeText(JSONObject(file.readText()).put("automaticEnrollmentKey","c".repeat(64)).toString())
            MeasurementUpload.Reply(401,"")
        })
        assertEquals(0,registrations);assertEquals(original,events().toString())
    }
    @Test fun collectionChangedDuringEnrollmentCannotRetryOldBatch() {
        automatic();assertTrue(event(1));val old=batch().getString("collection_id");var sends=0
        assertEquals(MeasurementUpload.Outcome.INACTIVE,MeasurementUpload.upload(context,epoch(),registrationTransport={ _,_ ->
            MeasurementTelemetry.configure(context,"",false,0)
            MeasurementTelemetry.configure(context,binding,true,since);assertTrue(event(2))
            MeasurementUpload.Reply(200,"{\"registered\":true}")
        }) { _,_ -> sends++;MeasurementUpload.Reply(401,"") })
        assertEquals(1,sends);assertNotEquals(old,batch().getString("collection_id"));assertEquals(1,events().length())
    }
    @Test fun suspendedOrStoppedDuringEnrollmentCannotRetryOrClearQueuedEvents() {
        automatic();assertTrue(event(1));val original=events().toString();var sends=0;var stopped=false
        assertEquals(MeasurementUpload.Outcome.RETRY,MeasurementUpload.upload(context,epoch(),stopped={stopped},registrationTransport={ _,_ ->
            stopped=true;MeasurementUpload.Reply(200,"{\"registered\":true}")
        }) { _,_ -> sends++;MeasurementUpload.Reply(401,"") })
        assertEquals(1,sends);assertEquals(original,events().toString())
        assertEquals(MeasurementUpload.Outcome.INACTIVE,MeasurementUpload.upload(context,epoch(),registrationTransport={ _,_ ->
            MeasurementTelemetry.suspend();MeasurementUpload.Reply(200,"{\"registered\":true}")
        }) { _,_ -> sends++;MeasurementUpload.Reply(401,"") })
        assertEquals(2,sends)
        MeasurementTelemetry.configure(context,binding,true,since);assertEquals(original,events().toString())
    }
    @Test fun resetEpochChangesDuringEnrollmentFenceTheOldWorker() {
        automatic();assertTrue(event(1));val original=events().toString();val beforeEpoch=epoch();var sends=0
        val fence=LocalResetEpoch(context.filesDir);val reset="synthetic-automatic-enrollment-reset"
        try {
            assertEquals(MeasurementUpload.Outcome.INACTIVE,MeasurementUpload.upload(context,beforeEpoch,registrationTransport={ _,_ ->
                assertTrue(fence.begin(reset));MeasurementUpload.Reply(200,"{\"registered\":true}")
            }) { _,_ -> sends++;MeasurementUpload.Reply(401,"") })
            assertEquals(1,sends)
        } finally { fence.finish(reset) { /* Test only the durable fence; erase no evidence. */ } }
        assertEquals(original,events().toString())
        assertEquals(MeasurementUpload.Outcome.INACTIVE,MeasurementUpload.upload(context,beforeEpoch) { _,_ -> throw AssertionError("Stale worker sent") })
    }
    @Test fun eventAddedWhileRegisteringIsNotAcknowledgedByTheEarlierBatch() {
        automatic();assertTrue(event(1));val old=events().getJSONObject(0).getString("event_id");val bodies=mutableListOf<String>()
        assertEquals(MeasurementUpload.Outcome.COMPLETE,MeasurementUpload.upload(context,epoch(),registrationTransport={ _,_ ->
            assertTrue(event(2));MeasurementUpload.Reply(200,"{\"registered\":true}")
        }) { _,body ->
            bodies.add(body)
            if(bodies.size==1)MeasurementUpload.Reply(401,"") else MeasurementUpload.Reply(200,accepted(body))
        })
        assertEquals(bodies[0],bodies[1]);assertEquals(1,events().length())
        assertNotEquals(old,events().getJSONObject(0).getString("event_id"))
    }
    @Test fun automaticKeyAndTokenAreStrictAndNeverAlterBindingFormula() {
        automatic();val valid=file.readText();val before=binding
        file.writeText(JSONObject(valid).apply { remove("automaticEnrollmentKey") }.toString())
        assertEquals(before,MeasurementUpload.enrollment(context)!!.binding)
        for(bad in listOf<Any>("A".repeat(64),"b".repeat(63),"b".repeat(64)+"\n",JSONObject.NULL,100)) {
            file.writeText(JSONObject(valid).put("automaticEnrollmentKey",bad).toString());assertNull(MeasurementUpload.enrollment(context))
        }
        file.writeText(JSONObject(valid).put("token","synthetic-manual-token-"+"x".repeat(32)).toString());assertNull(MeasurementUpload.enrollment(context))
        file.writeText(valid);assertEquals(before,MeasurementUpload.enrollment(context)!!.binding)
    }
    private class RecordingConnection(url:java.net.URL,private val code:Int,private val response:ByteArray):java.net.HttpURLConnection(url) {
        val written=java.io.ByteArrayOutputStream();var closed=false
        override fun connect() {} ; override fun disconnect() { closed=true }; override fun usingProxy()=false
        override fun getOutputStream():java.io.OutputStream=written
        override fun getResponseCode()=code
        override fun getInputStream():java.io.InputStream=response.inputStream()
    }
    @Test fun enrollmentHttpUsesOnlyExpectedBodyAndHeaderNeverRedirectsOrAcceptsHttp() {
        automatic();val enrollment=MeasurementUpload.enrollment(context)!!
        var connection:RecordingConnection?=null
        val reply=MeasurementUpload.register(enrollment,"0.1.0-qa.synthetic") { url ->
            assertEquals("https://synthetic.invalid/enrollments",url.toString())
            RecordingConnection(url,200,"{\"registered\":true}".toByteArray()).also { connection=it }
        }
        val http=connection!!;assertEquals(200,reply.status);assertFalse(http.instanceFollowRedirects)
        assertEquals(10000,http.connectTimeout);assertEquals(15000,http.readTimeout)
        assertEquals("b".repeat(64),http.getRequestProperty("X-Enrollment-Key"));assertNull(http.getRequestProperty("Authorization"))
        val body=JSONObject(http.written.toString("UTF-8"));assertEquals(setOf("token","app_version"),body.keys().asSequence().toSet())
        assertEquals(enrollment.token,body.getString("token"));assertTrue(http.closed)
        val redirected=MeasurementUpload.register(enrollment,"synthetic") { url -> RecordingConnection(url,302,ByteArray(0)) }
        assertEquals(302,redirected.status);assertEquals("",redirected.body)
        try { MeasurementUpload.httpPost("http://synthetic.invalid/enrollments","{}","X-Enrollment-Key","fixture") { throw AssertionError("Must reject before connecting") };fail("HTTP must fail") } catch (_:IllegalArgumentException) {}
        try { MeasurementUpload.register(enrollment,"synthetic") { url -> RecordingConnection(url,200,ByteArray(65537)) };fail("Oversize must fail") } catch (_:IllegalArgumentException) {}
    }
    @Test fun firstAutomaticAppVisitSchedulesNativeWorkBeforeServerRegistration() {
        automatic()
        androidx.work.testing.WorkManagerTestInitHelper.initializeTestWorkManager(context,
            androidx.work.Configuration.Builder().setExecutor(androidx.work.testing.SynchronousExecutor()).build())
        MeasurementUpload.cancel(context)
        assertTrue(JSONObject(MeasurementTelemetry.appSession(context,"d".repeat(32))).getBoolean("ok"))
        val work=androidx.work.WorkManager.getInstance(context)
        assertEquals(1,work.getWorkInfosForUniqueWork(MeasurementUpload.SOON).get().size)
        assertEquals(1,work.getWorkInfosForUniqueWork(MeasurementUpload.PERIODIC).get().size)
        assertEquals(1,events().length())
        MeasurementUpload.cancel(context)
    }

}
