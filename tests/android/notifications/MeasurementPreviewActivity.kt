package com.capstonedesign2026.platform

/** Launch target only for the isolated Android notification/visit test APK. */
class MeasurementPreviewActivity : android.app.Activity() {
    override fun onCreate(state: android.os.Bundle?) {
        super.onCreate(state)
        MeasurementAppVisits.initialize(this)
        setContentView(android.widget.TextView(this).apply { text="Synthetic notification visit validation" })
    }
}
