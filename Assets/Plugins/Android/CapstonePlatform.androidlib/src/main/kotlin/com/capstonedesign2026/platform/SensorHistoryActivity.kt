package com.capstonedesign2026.platform

import android.app.Activity
import android.content.res.ColorStateList
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.graphics.drawable.RippleDrawable
import android.graphics.drawable.StateListDrawable
import android.os.Build
import android.os.Bundle
import android.util.TypedValue
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.view.WindowInsets
import android.widget.Button
import android.widget.FrameLayout
import android.widget.LinearLayout
import android.widget.ProgressBar
import android.widget.ScrollView
import android.widget.TextView
import com.jjoe64.graphview.DefaultLabelFormatter
import com.jjoe64.graphview.GraphView
import com.jjoe64.graphview.GridLabelRenderer
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

/** Reads synced heart records; opening or refreshing this view never starts sensor collection. */
class SensorHistoryActivity : Activity() {
    private lateinit var content: LinearLayout
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private var loading = false
    private var detailsExpanded = false

    // Matches GardenUi's paper, ink, green, pale, border and warning colors.
    private val paper = Color.rgb(247, 248, 239)
    private val ink = Color.rgb(36, 60, 47)
    private val muted = Color.rgb(100, 116, 104)
    private val green = Color.rgb(49, 91, 72)
    private val pale = Color.rgb(227, 238, 216)
    private val border = Color.rgb(214, 225, 207)
    private val warning = Color.rgb(145, 79, 42)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        detailsExpanded = savedInstanceState?.getBoolean("detailsExpanded") ?: false
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(paper)
        }
        // Use explicit safe insets on API 29 as well as Android's enforced edge-to-edge windows.
        @Suppress("DEPRECATION")
        window.decorView.systemUiVisibility = View.SYSTEM_UI_FLAG_LAYOUT_STABLE or
            View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN or View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION or
            View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR or View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR
        if (Build.VERSION.SDK_INT >= 30) window.setDecorFitsSystemWindows(false)
        @Suppress("DEPRECATION")
        window.statusBarColor = paper
        @Suppress("DEPRECATION")
        window.navigationBarColor = paper
        root.setOnApplyWindowInsetsListener { view, insets ->
            if (Build.VERSION.SDK_INT >= 30) {
                val safe = insets.getInsets(WindowInsets.Type.systemBars() or WindowInsets.Type.displayCutout())
                view.setPadding(safe.left, safe.top, safe.right, safe.bottom)
            } else {
                @Suppress("DEPRECATION")
                view.setPadding(insets.systemWindowInsetLeft, insets.systemWindowInsetTop,
                    insets.systemWindowInsetRight, insets.systemWindowInsetBottom)
            }
            insets
        }
        val header = readingColumn().apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            setPadding(dp(24), dp(8), dp(24), dp(8))
        }
        label(header, "마음 정원", 18f, green, bold = true).apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        }
        header.addView(action("← 설정", primary = false) { finish() }.apply {
            contentDescription = "설정 화면으로 돌아가기"
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT)
        })
        root.addView(centered(header))
        content = readingColumn().apply { setPadding(dp(24), dp(8), dp(24), dp(24)) }
        root.addView(ScrollView(this).apply {
            isFillViewport = true
            addView(centered(content))
        }, LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f))
        setContentView(root)
        root.requestApplyInsets()
        refresh()
    }

    override fun onSaveInstanceState(outState: Bundle) {
        outState.putBoolean("detailsExpanded", detailsExpanded)
        super.onSaveInstanceState(outState)
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
        label(content, "시간별 심박수", 28f, bold = true, heading = true)
        label(content, "최근 24시간 · 1시간 평균", 18f, muted, top = 8)
        val status = result?.optString("status")
        val notice = HeartHistoryPresentation.notice(status)
        if (notice != null) {
            val card = card(content, pale)
            if (result == null) card.addView(ProgressBar(this).apply {
                indeterminateTintList = ColorStateList.valueOf(green)
                contentDescription = "심박 기록 조회 중"
            }, LinearLayout.LayoutParams(dp(32), dp(32)).apply { bottomMargin = dp(16) })
            label(card, notice.title, 22f, if (notice.warning) warning else ink, bold = true, heading = true).apply {
                accessibilityLiveRegion = View.ACCESSIBILITY_LIVE_REGION_POLITE
            }
            label(card, notice.detail, 18f, muted, top = 8)
            if (notice.returnToPermissions) addAction(card, "설정으로 돌아가기", true) { finish() }
        }
        if (result != null) {
            val hours = result.optJSONArray("hourlyHeartRate")
            val values = (0 until 24).map { index ->
                val hour = hours?.optJSONObject(index)
                if (hour == null || hour.isNull("meanBpm")) null else
                    hour.optDouble("meanBpm").takeIf { it.isFinite() }
            }
            val recordedHours = values.count { it != null }
            if (recordedHours > 0 || status in listOf("AVAILABLE", "STALE", "PARTIAL")) {
                val chartCard = card(content)
                label(chartCard, "시간별 평균 (BPM)", 20f, bold = true, heading = true)
                val measured = result.optLong("measuredAtEpochMs")
                label(chartCard, if (measured > 0) "마지막 측정 ${instant(measured)}" else "확인된 측정 시각 없음",
                    18f, muted, top = 8)
                chartCard.addView(graph(values, result.optLong("queryStartEpochMs")))
                label(chartCard, "기록 ${recordedHours}시간 · 빈 구간 ${24 - recordedHours}시간", 16f, muted, top = 8)
            }
        }
        addAction(content, if (loading) "조회 중…" else "새로고침", notice?.returnToPermissions != true) { refresh() }
            .isEnabled = !loading
        if (result != null) addDetails(result)
    }

    private fun addDetails(result: JSONObject) {
        val details = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(20), dp(20), dp(20), dp(20))
            background = shape(Color.WHITE)
            visibility = if (detailsExpanded) View.VISIBLE else View.GONE
        }
        val toggle = addAction(content, if (detailsExpanded) "기록 정보 접기  −" else "기록 정보 보기  +", false) { }
        toggle.setOnClickListener {
            detailsExpanded = !detailsExpanded
            details.visibility = if (detailsExpanded) View.VISIBLE else View.GONE
            toggle.text = if (detailsExpanded) "기록 정보 접기  −" else "기록 정보 보기  +"
        }
        content.addView(details, LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT,
            ViewGroup.LayoutParams.WRAP_CONTENT).apply { topMargin = dp(8) })
        label(details, "기록 정보", 20f, bold = true, heading = true)
        val source = result.optString("source")
        if (source.isNotBlank()) label(details, "출처 · ${HeartHistoryPresentation.sourceLabel(source)}", 18f, top = 12)
        label(details, "측정 기록 ${result.optInt("sampleCount")}개", 18f, top = 12)
        val start = result.optLong("queryStartEpochMs")
        val end = result.optLong("queryEndEpochMs")
        if (start > 0 && end > start) label(details, "조회 구간\n${instant(start)} ~ ${instant(end)}", 18f, top = 12)
        label(details, "Health Connect에 동기화된 기록이에요. 실시간 측정값이 아니며, 기록이 없는 시간은 0으로 계산하거나 선으로 잇지 않아요.",
            16f, muted, top = 16)
    }

    private fun graph(values: List<Double?>, start: Long): GraphView {
        val recordedHours = values.count { it != null }
        val graph = GraphView(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT,
                dp((280 * max(1f, resources.configuration.fontScale * .75f)).toInt())).apply { topMargin = dp(16) }
            contentDescription = "시간별 평균 심박수 그래프. 단위 BPM, 기록 ${recordedHours}시간, 빈 구간 ${24 - recordedHours}시간."
            viewport.setMinX(0.0)
            viewport.setMaxX(23.0)
            viewport.isXAxisBoundsManual = true
            viewport.setMinY(min(40.0, values.filterNotNull().minOrNull() ?: 40.0))
            viewport.setMaxY(max(120.0, ceil((values.filterNotNull().maxOrNull() ?: 120.0) / 10.0) * 10.0))
            viewport.isYAxisBoundsManual = true
            gridLabelRenderer.apply {
                numHorizontalLabels = if (resources.configuration.fontScale >= 1.6f) 2 else 3
                numVerticalLabels = 5
                textSize = sp(16f)
                horizontalLabelsColor = muted
                verticalLabelsColor = muted
                gridColor = border
                gridStyle = GridLabelRenderer.GridStyle.HORIZONTAL
                isHighlightZeroLines = false
                padding = dp(8)
                labelsSpace = dp(8)
                labelFormatter = object : DefaultLabelFormatter() {
                    override fun formatLabel(value: Double, isValueX: Boolean): String {
                        if (!isValueX) return String.format(Locale.US, "%.0f", value)
                        if (start <= 0) return "-${23 - value.toInt()}h"
                        return SimpleDateFormat("HH:mm", Locale.getDefault()).format(
                            Date(start + (value.toInt() + 1) * 3_600_000L))
                    }
                }
            }
        }
        var segment = ArrayList<DataPoint>()
        fun flush() {
            if (segment.isEmpty()) return
            graph.addSeries(LineGraphSeries(segment.toTypedArray()).apply {
                color = green
                thickness = dp(2)
                isDrawDataPoints = true
                dataPointsRadius = dp(3).toFloat()
            })
            segment = ArrayList()
        }
        values.forEachIndexed { index, value ->
            if (value == null) flush() else segment.add(DataPoint(index.toDouble(), value))
        }
        flush()
        return graph
    }

    private fun readingColumn(): LinearLayout = object : LinearLayout(this) {
        init { orientation = VERTICAL }
        override fun onMeasure(widthMeasureSpec: Int, heightMeasureSpec: Int) {
            super.onMeasure(MeasureSpec.makeMeasureSpec(min(MeasureSpec.getSize(widthMeasureSpec), dp(640)),
                MeasureSpec.EXACTLY), heightMeasureSpec)
        }
    }

    private fun centered(child: View): FrameLayout = FrameLayout(this).apply {
        addView(child, FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT,
            ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.TOP or Gravity.CENTER_HORIZONTAL))
    }

    private fun card(parent: LinearLayout, fill: Int = Color.WHITE): LinearLayout = LinearLayout(this).apply {
        orientation = LinearLayout.VERTICAL
        setPadding(dp(20), dp(20), dp(20), dp(20))
        background = shape(fill)
        parent.addView(this, LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT,
            ViewGroup.LayoutParams.WRAP_CONTENT).apply { topMargin = dp(24) })
    }

    private fun label(parent: LinearLayout, value: String, sizeSp: Float, color: Int = ink,
                      bold: Boolean = false, heading: Boolean = false, top: Int = 0): TextView = TextView(this).apply {
        text = value
        textSize = sizeSp
        setTextColor(color)
        typeface = Typeface.create("sans-serif", if (bold) Typeface.BOLD else Typeface.NORMAL)
        includeFontPadding = false
        setLineSpacing(dp(2).toFloat(), 1.1f)
        isAccessibilityHeading = heading
        parent.addView(this, LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT,
            ViewGroup.LayoutParams.WRAP_CONTENT).apply { topMargin = dp(top) })
    }

    private fun action(title: String, primary: Boolean, clicked: () -> Unit): Button = Button(this).apply {
        text = title
        textSize = 20f
        isAllCaps = false
        typeface = Typeface.create("sans-serif", if (primary) Typeface.BOLD else Typeface.NORMAL)
        setPadding(dp(20), dp(14), dp(20), dp(14))
        minWidth = 0
        minimumWidth = 0
        minHeight = dp(56)
        minimumHeight = dp(56)
        stateListAnimator = null
        setTextColor(ColorStateList(arrayOf(intArrayOf(-android.R.attr.state_enabled), intArrayOf()),
            intArrayOf(muted, if (primary) Color.WHITE else green)))
        val fills = StateListDrawable().apply {
            addState(intArrayOf(-android.R.attr.state_enabled), shape(pale))
            addState(intArrayOf(), shape(if (primary) green else Color.WHITE))
        }
        background = RippleDrawable(ColorStateList.valueOf(0x22315b48), fills, null)
        setOnClickListener { clicked() }
    }

    private fun addAction(parent: LinearLayout, title: String, primary: Boolean, clicked: () -> Unit): Button =
        action(title, primary, clicked).also { parent.addView(it,
            LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply { topMargin = dp(16) }) }

    private fun shape(fill: Int) = GradientDrawable().apply {
        setColor(fill)
        cornerRadius = dp(20).toFloat()
        if (fill != green) setStroke(dp(1), border)
    }

    private fun instant(epochMs: Long) = SimpleDateFormat("M/d HH:mm", Locale.getDefault()).format(Date(epochMs))
    private fun dp(value: Int) = (value * resources.displayMetrics.density).toInt()
    private fun sp(value: Float) = TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_SP, value, resources.displayMetrics)
}
