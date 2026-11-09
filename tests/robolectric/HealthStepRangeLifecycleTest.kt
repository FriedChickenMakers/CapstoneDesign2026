package com.capstonedesign2026.platform

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.delay
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.After
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

/** Synthetic query lambdas exercise the real busy/generation/coroutine lifecycle. */
@RunWith(RobolectricTestRunner::class)
@Config(sdk=[33])
class HealthStepRangeLifecycleTest {
    @Before fun setup() { HealthRepository.resetLocalData() }
    @After fun cleanup() { HealthRepository.resetLocalData() }
    private fun start(id: String, query: suspend () -> JSONObject) =
        JSONObject(HealthRepository.beginStepRangeQuery(1, 2, id, query))
    private suspend fun settled(): JSONObject = withTimeout(3000) {
        while (JSONObject(HealthRepository.stepRangeSnapshot()).getBoolean("refreshing")) delay(5)
        JSONObject(HealthRepository.stepRangeSnapshot())
    }
    @Test fun timeoutReportsErrorAndNextQueryCanStart() = runBlocking {
        assertEquals("AVAILABLE", start("timeout") { withTimeout(1) { delay(1000); JSONObject() } }.getString("status"))
        val failed=settled()
        assertEquals("ERROR",failed.getString("status"));assertEquals("timeout",failed.getString("requestId"))
        assertFalse(failed.getBoolean("queryComplete"))
        assertEquals("AVAILABLE", start("next") { JSONObject().put("status","AVAILABLE").put("queryComplete",true) }.getString("status"))
        assertEquals("next",settled().getString("requestId"))
    }
    @Test fun actualCancellationReleasesBusyWithoutPublishingError() = runBlocking {
        start("cancelled") { throw CancellationException("synthetic cancellation") }
        val cancelled=settled()
        assertEquals("NO_DATA",cancelled.getString("status"))
        assertEquals("AVAILABLE",start("next") { JSONObject().put("status","AVAILABLE") }.getString("status"))
        assertEquals("next",settled().getString("requestId"))
    }
    @Test fun resetCancellationCannotReleaseNewQueryOrOverwriteItsResult() = runBlocking {
        val entered=CompletableDeferred<Unit>()
        start("old") { entered.complete(Unit); delay(10000); JSONObject().put("status","OLD") }
        withTimeout(3000) { entered.await() }
        HealthRepository.resetLocalData()
        val release=CompletableDeferred<Unit>()
        start("new") { release.await(); JSONObject().put("status","AVAILABLE") }
        delay(50)
        val busy=JSONObject(HealthRepository.stepRangeSnapshot())
        assertEquals("new",busy.getString("requestId"));assertTrue(busy.getBoolean("refreshing"))
        assertEquals("STEP_QUERY_BUSY",start("blocked") { JSONObject() }.getString("errorCode"))
        release.complete(Unit)
        assertEquals("new",settled().getString("requestId"))
    }
}
