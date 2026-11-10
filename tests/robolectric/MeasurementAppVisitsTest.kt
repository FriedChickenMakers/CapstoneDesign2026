package com.capstonedesign2026.platform

import android.app.Activity
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.Robolectric
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import org.robolectric.shadows.ShadowSystemClock
import java.time.Duration

/** Actual Application lifecycle dispatch, no Unity helper invocation or device records. */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [33])
class MeasurementAppVisitsTest {
    private fun current() = JSONObject(MeasurementAppVisits.current())
    @Test fun hotReturnAfterActualStopRotatesVisitAndShortStopDoesNot() {
        val controller=Robolectric.buildActivity(Activity::class.java).setup()
        MeasurementAppVisits.initialize(controller.get())
        val first=current().getString("visit_key")
        assertTrue(current().getBoolean("foreground"))
        controller.pause().stop()
        assertFalse(current().getBoolean("foreground"))
        ShadowSystemClock.advanceBy(Duration.ofSeconds(10))
        controller.start().resume()
        assertEquals(first,current().getString("visit_key"))
        controller.pause().stop()
        ShadowSystemClock.advanceBy(Duration.ofSeconds(31))
        controller.start().resume()
        assertTrue(current().getBoolean("foreground"))
        assertNotEquals(first,current().getString("visit_key"))
        controller.pause().stop().destroy()
    }
    @Test fun pauseWithoutStopIncludingLongDialogDoesNotInventAnotherVisit() {
        val controller=Robolectric.buildActivity(Activity::class.java).setup()
        MeasurementAppVisits.initialize(controller.get())
        val first=current().getString("visit_key")
        controller.pause()
        ShadowSystemClock.advanceBy(Duration.ofSeconds(60))
        controller.resume()
        assertTrue(current().getBoolean("foreground"))
        assertEquals(first,current().getString("visit_key"))
        MeasurementAppVisits.initialize(controller.get())
        assertEquals(first,current().getString("visit_key"))
        controller.pause().stop().destroy()
    }
    @Test fun duplicateStopCannotMoveBoundaryAndThresholdIsInclusive() {
        var elapsed=1000L;var ids=0
        val clock=ForegroundVisitClock({elapsed}){(++ids).toString()}
        clock.start();val first=clock.key
        clock.stop();elapsed+=20000;clock.stop();elapsed+=10000
        clock.start();assertNotEquals(first,clock.key);assertEquals(2,ids)
        clock.start();assertEquals(2,ids)
        clock.stop();elapsed+=29999;clock.start();assertEquals(2,ids)
    }
}
