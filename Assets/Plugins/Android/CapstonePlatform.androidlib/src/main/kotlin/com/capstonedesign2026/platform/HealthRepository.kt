package com.capstonedesign2026.platform

import android.content.Context
import android.content.pm.PackageManager
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.ExerciseSessionRecord
import androidx.health.connect.client.records.HeartRateRecord
import androidx.health.connect.client.records.SleepSessionRecord
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.records.Record
import androidx.health.connect.client.records.metadata.DataOrigin
import androidx.health.connect.client.request.AggregateRequest
import androidx.health.connect.client.request.ReadRecordsRequest
import androidx.health.connect.client.time.TimeRangeFilter
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.TimeoutCancellationException
import kotlinx.coroutines.withTimeout
import kotlinx.coroutines.ensureActive
import kotlin.coroutines.coroutineContext
import org.json.JSONArray
import kotlin.reflect.KClass
import org.json.JSONObject
import java.time.Duration
import java.time.Instant
import java.time.ZoneId
import java.time.ZonedDateTime
import java.util.Locale

internal object HealthRepository {
    private const val TAG = "CapstoneHealthConnect"
    private const val SAMSUNG_HEALTH_PACKAGE = "com.sec.android.app.shealth"

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val lock = Any()
    private var appContext: Context? = null
    private var refreshing = false
    private var refreshQueued = false
    private var stepRangeBusy = false
    private var stepRangeResult = JSONObject().put("status", PlatformStatus.NO_DATA)
        .put("requestId", "").put("queryComplete", false)
    private var lastSuccessfulRefreshEpochMs = 0L
    private var lastSnapshot = emptySnapshot(
        PlatformStatus.SERVICE_UNAVAILABLE,
        "Health Connect has not been initialized",
    )

    fun initialize(context: Context) {
        appContext = context.applicationContext
        refresh()
    }

    fun requiredPermissions(): Set<String> = setOf(
        HealthPermission.getReadPermission(StepsRecord::class),
        HealthPermission.getReadPermission(SleepSessionRecord::class),
        HealthPermission.getReadPermission(HeartRateRecord::class),
        HealthPermission.getReadPermission(ExerciseSessionRecord::class),
    )

    fun availabilityStatus(): Pair<String, String> {
        val context = appContext ?: return PlatformStatus.SERVICE_UNAVAILABLE to
            "Health Connect has not been initialized"
        return try {
            when (HealthConnectClient.getSdkStatus(context)) {
                HealthConnectClient.SDK_AVAILABLE -> PlatformStatus.AVAILABLE to "Health Connect is available"
                HealthConnectClient.SDK_UNAVAILABLE_PROVIDER_UPDATE_REQUIRED ->
                    PlatformStatus.SERVICE_UNAVAILABLE to "Health Connect must be installed or updated"
                else -> PlatformStatus.UNSUPPORTED to "Health Connect is not supported on this device"
            }
        } catch (exception: Exception) {
            PlatformStatus.ERROR to exception.safeMessage()
        }
    }

    fun refreshRange(startEpochMs: Long, endEpochMs: Long): String {
        if (startEpochMs < 0 || endEpochMs <= startEpochMs || endEpochMs > System.currentTimeMillis() ||
            endEpochMs - startEpochMs > Duration.ofDays(7).toMillis())
            return resultJson(PlatformStatus.ERROR, "Heart interval must be past, positive and at most 7 days", "INVALID_RANGE")
        return refresh(Instant.ofEpochMilli(startEpochMs) to Instant.ofEpochMilli(endEpochMs))
    }

    fun refresh(heartRange: Pair<Instant, Instant>? = null): String {
        val context = appContext
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Health Connect is not initialized")
        val (availability, message) = availabilityStatus()
        if (availability != PlatformStatus.AVAILABLE) {
            synchronized(lock) { lastSnapshot = emptySnapshot(availability, message) }
            return resultJson(availability, message)
        }
        synchronized(lock) {
            if (refreshing) {
                if (heartRange != null)
                    return resultJson(PlatformStatus.ERROR, "Health refresh is already running", "REFRESH_BUSY")
                refreshQueued = true
                return resultJson(PlatformStatus.AVAILABLE, "Health data refresh queued")
            }
            refreshing = true
        }
        scope.launch {
            try {
                refreshInternal(context, heartRange)
            } catch (exception: Exception) {
                if (exception is CancellationException) throw exception
                synchronized(lock) {
                    lastSnapshot = emptySnapshot(PlatformStatus.ERROR, exception.safeMessage())
                }
            } finally {
                val again = synchronized(lock) {
                    refreshing = false
                    refreshQueued.also { refreshQueued = false }
                }
                if (again) refresh()
            }
        }
        return resultJson(PlatformStatus.AVAILABLE, "Health data refresh started")
    }

    fun snapshotJson(): JSONObject = synchronized(lock) {
        JSONObject(lastSnapshot.toString()).put("refreshing", refreshing)
    }

    fun stepRangeSnapshot(): String = synchronized(lock) {
        JSONObject(stepRangeResult.toString()).put("refreshing", stepRangeBusy).toString()
    }

    fun refreshStepsRange(startEpochMs: Long, endEpochMs: Long, requestId: String): String {
        if (requestId.isBlank() || startEpochMs < 0 || endEpochMs <= startEpochMs ||
            endEpochMs > System.currentTimeMillis() + 5_000L ||
            endEpochMs - startEpochMs > Duration.ofDays(30).toMillis())
            return resultJson(PlatformStatus.ERROR, "Invalid step interval", "INVALID_STEP_RANGE")
        val context = appContext
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Health Connect is not initialized")
        synchronized(lock) {
            if (stepRangeBusy) return resultJson(PlatformStatus.ERROR, "Step query is running", "STEP_QUERY_BUSY")
            stepRangeBusy = true
            stepRangeResult = JSONObject().put("status", PlatformStatus.NO_DATA)
                .put("requestId", requestId).put("queryStartEpochMs", startEpochMs)
                .put("queryEndEpochMs", endEpochMs).put("queryComplete", false)
        }
        scope.launch {
            val result = try {
                val (available, availabilityMessage) = availabilityStatus()
                if (available != PlatformStatus.AVAILABLE) {
                    JSONObject().put("status", available).put("message", availabilityMessage)
                } else {
                    val client = HealthConnectClient.getOrCreate(context)
                    val granted = client.permissionController.getGrantedPermissions()
                    if (!granted.contains(HealthPermission.getReadPermission(StepsRecord::class))) {
                        JSONObject().put("status", PlatformStatus.PERMISSION_REQUIRED)
                            .put("message", "Health Connect step permission is required")
                    } else {
                        val query = preferredSteps(client, Instant.ofEpochMilli(startEpochMs),
                            Instant.ofEpochMilli(endEpochMs))
                        val total = query.total
                        JSONObject().put("status", when {
                            !query.complete -> "PARTIAL"
                            total == null -> PlatformStatus.NO_DATA
                            else -> PlatformStatus.AVAILABLE
                        }).put("hasValue", query.complete && total != null)
                            .put("count", total?.count ?: 0L)
                            .put("source", total?.source?.id ?: "")
                            .put("sourceLabel", total?.source?.let(::sourceLabel) ?: "")
                            .put("sourceBreakdown", query.candidates.joinToString("; ") {
                                "${sourceLabel(it.source)} ${String.format(Locale.US, "%,d", it.count)}"
                            })
                            .put("queryComplete", query.complete)
                            .put("message", if (query.complete) "" else "Step records incomplete: ${query.reason}")
                    }
                }
            } catch (exception: Exception) {
                JSONObject().put("status", PlatformStatus.ERROR).put("message", exception.safeMessage())
            }
            result.put("requestId", requestId).put("queryStartEpochMs", startEpochMs)
                .put("queryEndEpochMs", endEpochMs).put("lastUpdatedEpochMs", System.currentTimeMillis())
            synchronized(lock) { stepRangeResult = result; stepRangeBusy = false }
        }
        return resultJson(PlatformStatus.AVAILABLE, "Step query started")
    }

    private suspend fun refreshInternal(context: Context, heartRange: Pair<Instant, Instant>?) {
        val client = HealthConnectClient.getOrCreate(context)
        val granted = client.permissionController.getGrantedPermissions()
        val now = Instant.now()
        val queryTime = now.toEpochMilli()
        val zone = ZoneId.systemDefault()

        val stepsPermission = HealthPermission.getReadPermission(StepsRecord::class)
        val sleepPermission = HealthPermission.getReadPermission(SleepSessionRecord::class)
        val heartPermission = HealthPermission.getReadPermission(HeartRateRecord::class)
        val exercisePermission = HealthPermission.getReadPermission(ExerciseSessionRecord::class)
        val permissionRequestCompleted = PermissionState.wasHealthRequestCompleted(context)

        val steps = if (granted.contains(stepsPermission)) {
            readPreferredSteps(client, now, queryTime, zone)
        } else missingPermissionMetric("steps", permissionRequestCompleted)
        val samsungSteps = if (granted.contains(stepsPermission)) {
            readSamsungSteps(client, now, queryTime, zone)
        } else missingPermissionMetric("Samsung steps", permissionRequestCompleted)
        val sleep = if (granted.contains(sleepPermission)) {
            readSleep(client, now, queryTime)
        } else missingPermissionMetric("sleep", permissionRequestCompleted)
        val heartRate = if (granted.contains(heartPermission)) {
            readHeartRate(client, heartRange?.second ?: now, queryTime, heartRange?.first ?: now.minus(Duration.ofDays(7)))
        } else missingPermissionMetric("heart rate", permissionRequestCompleted)
        val exercise = if (granted.contains(exercisePermission)) {
            readExercise(client, now, queryTime)
        } else missingPermissionMetric("exercise", permissionRequestCompleted)

        val sourcePackages = listOf(steps, samsungSteps, sleep, heartRate, exercise)
            .flatMap { metric -> val sources = metric.optJSONArray("sourcePackages")
                if (sources == null) listOf(metric.optString("sourcePackage")) else
                    (0 until sources.length()).map { sources.getString(it) } }
            .filter { it.isNotEmpty() }
        val hasSamsungData = sourcePackages.any { it == SAMSUNG_HEALTH_PACKAGE }
        val samsungStatus = samsungHealthStatus(context)
        val permissionStatus = if (granted.containsAll(requiredPermissions())) {
            PlatformStatus.AVAILABLE
        } else if (permissionRequestCompleted) {
            PlatformStatus.PERMISSION_DENIED
        } else {
            PlatformStatus.PERMISSION_REQUIRED
        }

        if (listOf(steps, samsungSteps, sleep, heartRate, exercise).any { it.optBoolean("queryComplete") }) {
            lastSuccessfulRefreshEpochMs = queryTime
        }
        val snapshot = JSONObject()
            .put("status", PlatformStatus.AVAILABLE)
            .put("message", "Health Connect query completed")
            .put("permissionStatus", permissionStatus)
            .put("grantedPermissionCount", granted.intersect(requiredPermissions()).size)
            .put("requiredPermissionCount", requiredPermissions().size)
            .put("lastRefreshEpochMs", queryTime)
            .put("lastSuccessfulRefreshEpochMs", lastSuccessfulRefreshEpochMs)
            .put("samsungHealthStatus", samsungStatus.first)
            .put("samsungHealthMessage", samsungStatus.second)
            .put("steps", steps)
            .put("samsungSteps", samsungSteps)
            .put("sleep", sleep)
            .put("heartRate", heartRate)
            .put("exercise", exercise)
            .put(
                "watchData",
                JSONObject()
                    .put("status", if (hasSamsungData) PlatformStatus.AVAILABLE else PlatformStatus.NO_DATA)
                    .put(
                        "message",
                        if (hasSamsungData) "Samsung Health data is present" else
                            "No Samsung Health-origin record was returned",
                    )
                    .put("source", if (hasSamsungData) "Samsung Health" else "")
                    .put("directConnectionStatus", "NOT_CHECKED"),
            )
        synchronized(lock) { lastSnapshot = snapshot }
    }

    private fun window(value: JSONObject, start: Instant, end: Instant, zone: ZoneId = ZoneId.systemDefault()): JSONObject =
        value.put("queryStartEpochMs", start.toEpochMilli()).put("queryEndEpochMs", end.toEpochMilli())
            .put("queryTimeZone", zone.id)

    internal data class PreferredStepsResult(val total: HealthSourcePolicy.StepTotal?,
                                             val complete: Boolean, val reason: String,
                                             val sourcePackages: List<String>,
                                             val candidates: List<HealthSourcePolicy.StepTotal>)

    internal suspend fun preferredSteps(client: HealthConnectClient, start: Instant,
                                        end: Instant): PreferredStepsResult {
        val result = readAll(client, StepsRecord::class, start, end)
        if (!result.complete) return PreferredStepsResult(null, false, result.reason, emptyList(), emptyList())
        val records = result.records.distinctBy { it.metadata.id }
        val spans = records.map { record -> HealthSourcePolicy.StepSpan(
            record.metadata.id, source(record), record.startTime.toEpochMilli(),
            record.endTime.toEpochMilli(), record.count,
        ) }
        return PreferredStepsResult(HealthSourcePolicy.steps(spans, start.toEpochMilli(), end.toEpochMilli()),
            true, result.reason, records.map { it.metadata.dataOrigin.packageName }.distinct().sorted(),
            HealthSourcePolicy.stepCandidates(spans, start.toEpochMilli(), end.toEpochMilli()))
    }

    private suspend fun readPreferredSteps(client: HealthConnectClient, now: Instant,
                                           queryTime: Long, zone: ZoneId): JSONObject {
        val start = now.atZone(zone).toLocalDate().atStartOfDay(zone).toInstant()
        val result = preferredSteps(client, start, now)
        val total = result.total
        val value = when {
            !result.complete -> noDataMetric("Step query incomplete: ${result.reason}", queryTime)
                .put("status", "PARTIAL")
            total == null -> noDataMetric("No steps in this interval", queryTime)
            else -> metric(total.count.toDouble(), String.format(Locale.US, "%,d", total.count),
                "steps", total.source.packageName, 0L, queryTime)
                .put("source", sourceLabel(total.source))
                .put("sourceId", total.source.id)
                .put("recordCount", total.records)
        }
        return window(value, start, now, zone)
            .put("sourcePackages", JSONArray(result.sourcePackages))
            .put("sourceBreakdown", result.candidates.joinToString("; ") {
                "${sourceLabel(it.source)} ${String.format(Locale.US, "%,d", it.count)}"
            })
            .put("sourceFilter", total?.source?.id ?: "NONE")
            .put("queryComplete", result.complete)
            .put("completionReason", result.reason)
            .put("message", if (total == null) value.optString("message") else
                "One preferred origin/device; overlapping sources are not added")
    }

    private suspend fun readSamsungSteps(client: HealthConnectClient, now: Instant,
                                         queryTime: Long, zone: ZoneId): JSONObject {
        val start = now.atZone(zone).toLocalDate().atStartOfDay(zone).toInstant()
        val filter = setOf(DataOrigin(SAMSUNG_HEALTH_PACKAGE))
        val result = try {
            val aggregate = withTimeout(30_000) { HealthQueryLogic.retry { client.aggregate(AggregateRequest(
                metrics = setOf(StepsRecord.COUNT_TOTAL), timeRangeFilter = TimeRangeFilter.between(start, now),
                dataOriginFilter = filter)) } }
            val count = aggregate[StepsRecord.COUNT_TOTAL]
            val value = if (count == null) noDataMetric("No aggregated steps in this interval", queryTime) else
                metric(count.toDouble(), String.format(Locale.US, "%,d", count), "steps", "", 0, queryTime)
            value.put("source", "Samsung Health aggregate (diagnostic)")
                .put("sourcePackages", JSONArray(aggregate.dataOrigins.map { it.packageName }.sorted()))
                .put("queryComplete", true).put("completionReason", "COMPLETE")
                .put("message", "Aggregate; measurement timestamp unavailable; not phone step counter")
        } catch (exception: Exception) {
            coroutineContext.ensureActive()
            if (exception is CancellationException && exception !is TimeoutCancellationException) throw exception
            errorMetric("STEPS_AGGREGATE_FAILED", exception, queryTime).put("queryComplete", false)
                .put("completionReason", if (exception is TimeoutCancellationException) "TIMEOUT" else "READ_FAILED")
        }
        return window(result, start, now, zone).put("sourceFilter", SAMSUNG_HEALTH_PACKAGE)
    }

    private fun source(record: Record): HealthSourcePolicy.Source = HealthSourcePolicy.Source(
        record.metadata.dataOrigin.packageName, record.metadata.device?.type,
    )

    private fun sourceLabel(source: HealthSourcePolicy.Source): String {
        val name = friendlySource(source.packageName).ifBlank { "Health Connect" }
        val device = when (source.deviceType) {
            1 -> "watch"
            4 -> "ring"
            6 -> "fitness band"
            7 -> "chest strap"
            2 -> "phone"
            else -> "device unspecified"
        }
        return "$name · $device"
    }

    private suspend fun <T : Record> readAll(client: HealthConnectClient, type: KClass<T>, start: Instant, end: Instant) =
        HealthQueryLogic.pages { token ->
            val response = client.readRecords(ReadRecordsRequest(recordType = type,
                timeRangeFilter = TimeRangeFilter.between(start, end), ascendingOrder = true,
                pageSize = 1000, pageToken = token))
            HealthQueryLogic.Page(response.records, response.pageToken)
        }

    private fun <T : Record> annotate(value: JSONObject, result: HealthQueryLogic.Result<T>, start: Instant, end: Instant): JSONObject {
        window(value, start, end).put("queryComplete", result.complete).put("completionReason", result.reason)
            .put("pagesRead", result.pages).put("recordCount", result.records.distinctBy { it.metadata.id }.size)
            .put("sourceFilter", "ALL")
            .put("sourcePackages", JSONArray(result.records.map { it.metadata.dataOrigin.packageName }.distinct().sorted()))
        if (!result.complete) value.put("status", "PARTIAL").put("message", "Incomplete interval: ${result.reason}")
        return value
    }

    private suspend fun readSleep(client: HealthConnectClient, now: Instant, queryTime: Long): JSONObject {
        val start = now.minus(Duration.ofHours(48))
        val result = readAll(client, SleepSessionRecord::class, start, now)
        val selected = HealthSourcePolicy.select(result.records.map { record ->
            HealthSourcePolicy.Candidate(record, source(record), record.endTime.toEpochMilli())
        }, queryTime, Duration.ofHours(36).toMillis())
        val latest = selected?.values?.maxByOrNull { it.endTime }
        val value = if (latest == null) noDataMetric("No sleep session in queried interval", queryTime) else {
            val minutes = Duration.between(latest.startTime, latest.endTime).toMinutes()
            metric(minutes.toDouble(), "${minutes / 60}h ${minutes % 60}m session", "session minutes",
                latest.metadata.dataOrigin.packageName, latest.endTime.toEpochMilli(), queryTime,
                Duration.ofHours(36).toMillis()).put("message", "Session length; sleep stages not analyzed")
                .put("source", sourceLabel(selected!!.source))
        }
        return annotate(value, result, start, now)
    }

    private suspend fun readHeartRate(client: HealthConnectClient, now: Instant, queryTime: Long, start: Instant): JSONObject {
        val result = readAll(client, HeartRateRecord::class, start, now)
        val selected = HealthSourcePolicy.select(result.records.flatMap { record -> record.samples.map { sample ->
            HealthSourcePolicy.Candidate(HealthQueryLogic.Sample(record.metadata.id,
                record.metadata.dataOrigin.packageName, sample.time.toEpochMilli(), sample.beatsPerMinute),
                source(record), sample.time.toEpochMilli())
        } }, queryTime, Duration.ofHours(24).toMillis())
        val series = HealthQueryLogic.series(selected?.values.orEmpty(), start.toEpochMilli(), now.toEpochMilli())
        val latest = series.samples.lastOrNull()
        val value = if (latest == null) noDataMetric("No measured heart-rate samples in queried interval", queryTime) else
            metric(latest.beatsPerMinute.toDouble(), "${latest.beatsPerMinute} bpm", "bpm", latest.sourcePackage,
                latest.measuredAtEpochMs, queryTime, Duration.ofHours(24).toMillis())
                .put("source", sourceLabel(selected!!.source))
        value.put("sampleCount", series.samples.size).put("displaySampleCount", series.display.size)
            .put("heartRateSamples", JSONArray().apply { series.display.forEach { sample -> put(JSONObject()
                .put("recordId", sample.recordId).put("sourcePackage", sample.sourcePackage)
                .put("measuredAtEpochMs", sample.measuredAtEpochMs).put("beatsPerMinute", sample.beatsPerMinute)
                .put("segmentId", sample.segmentId)) } })
            .put("missingIntervals", JSONArray().apply { series.gaps.forEach { gap -> put(JSONObject()
                .put("startEpochMs", gap.startEpochMs).put("endEpochMs", gap.endEpochMs).put("sourcePackage", gap.sourcePackage)) } })
        return annotate(value, result, start, now)
    }

    private suspend fun readExercise(client: HealthConnectClient, now: Instant, queryTime: Long): JSONObject {
        val start = now.minus(Duration.ofDays(7))
        val result = readAll(client, ExerciseSessionRecord::class, start, now)
        val selected = HealthSourcePolicy.select(result.records.map { record ->
            HealthSourcePolicy.Candidate(record, source(record), record.endTime.toEpochMilli())
        }, queryTime)
        val latest = selected?.values?.maxByOrNull { it.endTime }
        val value = if (latest == null) noDataMetric("No exercise session in queried interval", queryTime) else {
            val minutes = Duration.between(latest.startTime, latest.endTime).toMinutes()
            metric(minutes.toDouble(), "$minutes min (type ${latest.exerciseType})", "minutes",
                latest.metadata.dataOrigin.packageName, latest.endTime.toEpochMilli(), queryTime)
                .put("source", sourceLabel(selected!!.source))
        }
        return annotate(value, result, start, now)
    }

    private fun metric(
        value: Double,
        displayValue: String,
        unit: String,
        sourcePackage: String,
        measuredAtEpochMs: Long,
        queryTime: Long,
        staleAfterMs: Long = Long.MAX_VALUE,
    ): JSONObject = JSONObject()
        .put(
            "status",
            if (measuredAtEpochMs > 0 && queryTime - measuredAtEpochMs > staleAfterMs) PlatformStatus.STALE else
                PlatformStatus.AVAILABLE,
        )
        .put("hasValue", true)
        .put("value", value)
        .put("displayValue", displayValue)
        .put("unit", unit)
        .put("source", friendlySource(sourcePackage))
        .put("sourcePackage", sourcePackage)
        .put("measuredAtEpochMs", measuredAtEpochMs)
        .put("lastUpdatedEpochMs", queryTime)
        .put("message", "")
        .put("errorCode", "")

    private fun noDataMetric(message: String, queryTime: Long): JSONObject = JSONObject()
        .put("status", PlatformStatus.NO_DATA)
        .put("hasValue", false)
        .put("value", 0.0)
        .put("displayValue", "—")
        .put("unit", "")
        .put("source", "")
        .put("sourcePackage", "")
        .put("measuredAtEpochMs", 0L)
        .put("lastUpdatedEpochMs", queryTime)
        .put("message", message)
        .put("errorCode", "")

    private fun missingPermissionMetric(label: String, requestCompleted: Boolean): JSONObject = JSONObject()
        .put(
            "status",
            if (requestCompleted) PlatformStatus.PERMISSION_DENIED else PlatformStatus.PERMISSION_REQUIRED,
        )
        .put("hasValue", false)
        .put("value", 0.0)
        .put("displayValue", "—")
        .put("unit", "")
        .put("source", "")
        .put("sourcePackage", "")
        .put("measuredAtEpochMs", 0L)
        .put("lastUpdatedEpochMs", 0L)
        .put(
            "message",
            if (requestCompleted) "Health Connect permission denied for $label" else
                "Health Connect permission required for $label",
        )
        .put("errorCode", "")

    private fun errorMetric(code: String, exception: Exception, queryTime: Long): JSONObject =
        JSONObject()
            .put("status", PlatformStatus.ERROR)
            .put("hasValue", false)
            .put("value", 0.0)
            .put("displayValue", "—")
            .put("unit", "")
            .put("source", "")
            .put("sourcePackage", "")
            .put("measuredAtEpochMs", 0L)
            .put("lastUpdatedEpochMs", queryTime)
            .put("message", "Health Connect query failed; check permission and retry")
            .put("errorCode", code)

    private fun emptySnapshot(status: String, message: String): JSONObject = JSONObject()
        .put("status", status)
        .put("message", message)
        .put("permissionStatus", status)
        .put("grantedPermissionCount", 0)
        .put("requiredPermissionCount", requiredPermissions().size)
        .put("lastRefreshEpochMs", 0L)
        .put("lastSuccessfulRefreshEpochMs", lastSuccessfulRefreshEpochMs)
        .put("samsungHealthStatus", samsungHealthStatus().first)
        .put("samsungHealthMessage", samsungHealthStatus().second)
        .put("steps", noDataMetric(message, 0L).put("status", status))
        .put("sleep", noDataMetric(message, 0L).put("status", status))
        .put("heartRate", noDataMetric(message, 0L).put("status", status))
        .put("exercise", noDataMetric(message, 0L).put("status", status))
        .put(
            "watchData",
            JSONObject()
                .put("status", PlatformStatus.DISCONNECTED)
                .put("message", "Samsung Health origin not checked; direct Watch connection not checked")
                .put("source", "").put("directConnectionStatus", "NOT_CHECKED"),
        )

    private fun friendlySource(packageName: String): String = when (packageName) {
        SAMSUNG_HEALTH_PACKAGE -> "Samsung Health"
        "com.android.healthconnect.phone" -> "Health Connect phone"
        "com.google.android.apps.fitness" -> "Google Fit"
        else -> packageName
    }

    private fun samsungHealthStatus(context: Context? = appContext): Pair<String, String> {
        if (context == null) {
            return PlatformStatus.SERVICE_UNAVAILABLE to "Samsung Health availability has not been checked"
        }
        return try {
            val application = context.packageManager.getApplicationInfo(SAMSUNG_HEALTH_PACKAGE, 0)
            if (application.enabled) {
                PlatformStatus.AVAILABLE to "Samsung Health is installed"
            } else {
                PlatformStatus.SERVICE_UNAVAILABLE to "Samsung Health is disabled"
            }
        } catch (_: PackageManager.NameNotFoundException) {
            PlatformStatus.SERVICE_UNAVAILABLE to "Samsung Health is not installed"
        } catch (exception: Exception) {
            PlatformStatus.ERROR to exception.safeMessage()
        }
    }
}
