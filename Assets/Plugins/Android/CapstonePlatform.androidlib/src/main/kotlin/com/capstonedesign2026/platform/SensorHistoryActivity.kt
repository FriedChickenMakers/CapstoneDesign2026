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
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import kotlin.math.ceil
import kotlin.math.max
import kotlin.math.min

/** Native debug chart. Reads synced health records; it does not start sensor collection. */
class SensorHistoryActivity : Activity() {
    private lateinit var content: LinearLayout
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private var loading = false

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        content = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(16), dp(16), dp(16), dp(16))
            setBackgroundColor(Color.WHITE)
        }
        setContentView(ScrollView(this).apply { addView(content) })
        refresh()
    }

    override fun onDestroy() {
        scope.cancel()
        super.onDestroy()
    }

    private fun refresh() {
        if (loading) return
        loading = true
        render(null)
        scope.launch {
            val result = try {
                withContext(Dispatchers.IO) { HealthRepository.readHeartHistory(applicationContext) }
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (_: Exception) {
                JSONObject().put("status", "ERROR")
            }
            loading = false
            render(result)
        }
    }

    private fun render(result: JSONObject?) {
        content.removeAllViews()
        label("시간별 심박수", 24f)
        label("최근 24시간의 1시간 평균 심박수(BPM). Health Connect에 동기화된 측정 기록만 표시하며, 기록이 없는 시간은 빈칸입니다.", 15f)
        content.addView(Button(this).apply {
            text = if (loading) "조회 중…" else "새로고침"
            isEnabled = !loading
            setOnClickListener { refresh() }
        })
        if (result == null) return
        val status = result.optString("status")
        label(when (status) {
            "AVAILABLE", "STALE" -> "측정 기록 ${result.optInt("sampleCount")}개"
            "PARTIAL" -> "일부 기록만 읽었습니다. 아래 평균은 잠정 값입니다. 다시 조회해 주세요."
            "PERMISSION_DENIED" -> "심박수 읽기 권한이 필요합니다. 앱 설정의 Health Perm에서 허용해 주세요."
            "UNSUPPORTED", "SERVICE_UNAVAILABLE" -> "이 기기에서 Health Connect를 사용할 수 없습니다."
            "NO_DATA" -> "최근 24시간에 동기화된 심박수 기록이 없습니다."
            else -> "심박수 기록을 읽지 못했습니다. 다시 조회해 주세요."
        }, 15f)
        val start = result.optLong("queryStartEpochMs")
        val end = result.optLong("queryEndEpochMs")
        if (start > 0 && end > start) label("조회 구간: ${instant(start)} ~ ${instant(end)}", 13f)
        val source = result.optString("source")
        if (source.isNotBlank()) label("출처: $source", 14f)
        val measured = result.optLong("measuredAtEpochMs")
        if (measured > 0) label("마지막 측정: ${instant(measured)} · 실시간 측정값이 아닙니다.", 13f)
        val hours = result.optJSONArray("hourlyHeartRate")
        val values = (0 until 24).map { index ->
            val hour = hours?.optJSONObject(index)
            if (hour == null || hour.isNull("meanBpm")) null else
                hour.optDouble("meanBpm").takeIf { it.isFinite() }
        }
        label("1시간 평균 · ${values.count { it != null }}/24 구간", 18f)
        val graph = GraphView(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(320))
            viewport.setMinX(0.0)
            viewport.setMaxX(23.0)
            viewport.isXAxisBoundsManual = true
            viewport.setMinY(min(40.0, values.filterNotNull().minOrNull() ?: 40.0))
            viewport.setMaxY(max(120.0, ceil((values.filterNotNull().maxOrNull() ?: 120.0) / 10.0) * 10.0))
            viewport.isYAxisBoundsManual = true
            gridLabelRenderer.numHorizontalLabels = 4
            gridLabelRenderer.numVerticalLabels = 5
            gridLabelRenderer.verticalAxisTitle = "BPM"
            gridLabelRenderer.labelFormatter = object : DefaultLabelFormatter() {
                override fun formatLabel(value: Double, isValueX: Boolean): String {
                    if (!isValueX) return String.format(Locale.US, "%.0f", value)
                    if (start <= 0) return "-${23 - value.toInt()}h"
                    return SimpleDateFormat("HH:mm", Locale.getDefault()).format(
                        Date(start + (value.toInt() + 1) * 3_600_000L))
                }
            }
        }
        var segment = ArrayList<DataPoint>()
        fun flush() {
            if (segment.isEmpty()) return
            graph.addSeries(LineGraphSeries(segment.toTypedArray()).apply {
                color = 0xffb85d34.toInt()
                thickness = 5
                isDrawDataPoints = true
                dataPointsRadius = 5f
            })
            segment = ArrayList()
        }
        values.forEachIndexed { index, value ->
            if (value == null) flush() else segment.add(DataPoint(index.toDouble(), value))
        }
        flush()
        content.addView(graph)
    }

    private fun instant(epochMs: Long) = SimpleDateFormat("MM/dd HH:mm", Locale.getDefault()).format(Date(epochMs))

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
