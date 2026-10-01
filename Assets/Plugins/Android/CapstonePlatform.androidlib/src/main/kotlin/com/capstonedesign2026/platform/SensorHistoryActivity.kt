package com.capstonedesign2026.platform

import android.app.Activity
import android.graphics.Color
import android.os.Bundle
import android.view.ViewGroup
import android.widget.Button
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import com.jjoe64.graphview.GraphView
import com.jjoe64.graphview.DefaultLabelFormatter
import com.jjoe64.graphview.series.DataPoint
import com.jjoe64.graphview.series.LineGraphSeries
import kotlin.math.ceil
import kotlin.math.max

/** Temporary native debug screen; chart styling is intentionally minimal. */
class SensorHistoryActivity : Activity() {
    private lateinit var content: LinearLayout

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        content = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(16), dp(16), dp(16), dp(16))
            setBackgroundColor(Color.WHITE)
        }
        setContentView(ScrollView(this).apply { addView(content) })
        render()
    }

    private fun render() {
        content.removeAllViews()
        label("Acceleration history (debug)", 22f)
        label("Mean acceleration magnitude (m/s²), including gravity. " +
            "When the trial is enabled, the sensor samples for 5 seconds each minute. Blank intervals had no samples.", 14f)
        content.addView(Button(this).apply {
            text = "Refresh"
            setOnClickListener { render() }
        })
        val now = System.currentTimeMillis()
        val records = DailyAccelerationStore.readRecent(this, now)
        addChart("Last 24 hours · 1-hour means", records, now, 3_600_000L, 24, 0xff187a92.toInt())
        addChart("Last 1 hour · 1-minute means", records, now, 60_000L, 60, 0xffb85d34.toInt())
    }

    private fun addChart(
        title: String, records: List<DailyAccelerationBuckets.Minute>, now: Long,
        bucketMs: Long, slots: Int, lineColor: Int
    ) {
        val values = DailyAccelerationBuckets.values(records, now, bucketMs, slots)
        label("$title (${values.count { it != null }}/$slots intervals)", 17f)
        val graph = GraphView(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(245))
            viewport.setMinX(0.0)
            viewport.setMaxX((slots - 1).toDouble())
            viewport.isXAxisBoundsManual = true
            viewport.setMinY(0.0)
            viewport.setMaxY(max(12.0, ceil((values.filterNotNull().maxOrNull() ?: 0.0) * 1.2)))
            viewport.isYAxisBoundsManual = true
            gridLabelRenderer.numHorizontalLabels = 5
            gridLabelRenderer.numVerticalLabels = 5
            gridLabelRenderer.labelFormatter = object : DefaultLabelFormatter() {
                override fun formatLabel(value: Double, isValueX: Boolean): String {
                    if (!isValueX) return String.format("%.0f", value)
                    val ago = max(0, slots - 1 - value.toInt())
                    return if (bucketMs == 3_600_000L) "-${ago}h" else "-${ago}m"
                }
            }
        }
        var segment = ArrayList<DataPoint>()
        fun flush() {
            if (segment.isEmpty()) return
            graph.addSeries(LineGraphSeries(segment.toTypedArray()).apply {
                color = lineColor
                thickness = 5
                isDrawDataPoints = true
                dataPointsRadius = 4f
            })
            segment = ArrayList()
        }
        values.forEachIndexed { index, value ->
            if (value == null) flush() else segment.add(DataPoint(index.toDouble(), value))
        }
        flush()
        content.addView(graph)
    }

    private fun label(value: String, sizeSp: Float) {
        content.addView(TextView(this).apply {
            text = value
            textSize = sizeSp
            setTextColor(Color.DKGRAY)
            setPadding(0, dp(8), 0, dp(8))
        })
    }

    private fun dp(value: Int) = (value * resources.displayMetrics.density).toInt()
}
