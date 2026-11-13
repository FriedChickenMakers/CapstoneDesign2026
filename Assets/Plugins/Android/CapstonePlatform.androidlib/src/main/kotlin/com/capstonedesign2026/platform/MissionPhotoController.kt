package com.capstonedesign2026.platform

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Matrix
import android.os.SystemClock
import android.util.AtomicFile
import androidx.exifinterface.media.ExifInterface
import com.google.mediapipe.framework.image.BitmapImageBuilder
import com.google.mediapipe.tasks.core.BaseOptions
import com.google.mediapipe.tasks.vision.core.RunningMode
import com.google.mediapipe.tasks.vision.objectdetector.ObjectDetector
import org.json.JSONObject
import java.io.ByteArrayOutputStream
import java.io.File
import java.security.MessageDigest
import java.time.Instant
import java.util.concurrent.Executors

/** No photo leaves the app. Results contain only bounded, task-specific metadata. */
object MissionPhotoController {
    const val MODEL = "efficientdet-lite0-int8-v1"
    private const val ASSET = "models/efficientdet_lite0_int8_v1.tflite"
    private const val SHA256 = "0720bf247bd76e6594ea28fa9c6f7c5242be774818997dbbeffc4da460c723bb"
    private val worker = Executors.newSingleThreadExecutor()
    private val lock = Any()
    private val active = java.util.concurrent.ConcurrentHashMap.newKeySet<String>()
    private val capturing = java.util.concurrent.ConcurrentHashMap.newKeySet<String>()
    private var verifiedModel = false // Access only on worker.
    private val idPattern = Regex("^[a-f0-9]{32}$")
    fun valid(id: String, target: String) = idPattern.matches(id) && target in setOf("container", "book")
    // Reads never recreate directories. All writes use the application's durable epoch fence.
    fun root(context: Context) = File(context.noBackupFilesDir, "mission_photos")
    fun raw(context: Context, id: String): File {
        require(idPattern.matches(id))
        return File(File(context.cacheDir, "mission_capture"), "$id.jpg")
    }
    private fun file(context: Context, id: String, suffix: String): File {
        require(idPattern.matches(id)); return File(root(context), id + suffix)
    }
    fun resumeCapture(context: Context, id: String, target: String, epoch: String): Boolean = synchronized(lock) {
        if (!valid(id,target) || !NativeLocalReset.current(context,epoch) || file(context,id,".cancelled").exists()) false
        else { capturing.add(id); true }
    }
    fun prepareCapture(context: Context, id: String, epoch: String): Boolean = synchronized(lock) {
        if (!idPattern.matches(id)) return@synchronized false
        var prepared=false
        NativeLocalReset.write(context,epoch) {
            if (!file(context,id,".cancelled").exists()) {
                check(raw(context,id).parentFile!!.isDirectory || raw(context,id).parentFile!!.mkdirs())
                prepared=true
            }
        }
        prepared
    }
    private fun result(id: String, target: String, status: String) = JSONObject()
        .put("id", id).put("target", target).put("status", status).put("modelVersion", MODEL)
        .put("capturedUtc", Instant.now().toString()).put("label", "").put("score", 0.0)
    private fun writeUnlocked(context: Context, id: String, value: JSONObject) {
        val output = AtomicFile(file(context, id, ".json")); val stream = output.startWrite()
        try { stream.write(value.toString().toByteArray(Charsets.UTF_8)); output.finishWrite(stream) }
        catch (e: Exception) { output.failWrite(stream); throw e }
        check(output.readFully().toString(Charsets.UTF_8)==value.toString()) { "Photo result write was not confirmed" }
    }
    /** Also used for worker completion: cancellation and reset cannot race a result commit. */
    internal fun commitResult(context: Context, id: String, epoch: String, value: JSONObject): Boolean = synchronized(lock) {
        if(!idPattern.matches(id))return@synchronized false
        var committed=false
        NativeLocalReset.write(context,epoch) {
            if(!file(context,id,".cancelled").exists()) { writeUnlocked(context,id,value); committed=true }
        }
        committed
    }
    @JvmStatic fun start(activity: Activity, id: String, target: String): String = synchronized(lock) {
        if (!valid(id, target)) return@synchronized "{\"status\":\"unavailable\"}"
        val epoch=NativeLocalReset.epoch(activity)
        try {
            if(!NativeLocalReset.current(activity,epoch))return@synchronized result(id,target,"unavailable").toString()
            if(file(activity,id,".json").exists())return@synchronized poll(activity,id)
            if(!commitResult(activity,id,epoch,result(id,target,"capturing")))return@synchronized result(id,target,"unavailable").toString()
            capturing.add(id)
            activity.runOnUiThread {
                if(!NativeLocalReset.current(activity,epoch) || file(activity,id,".cancelled").exists())return@runOnUiThread
                try { activity.startActivity(Intent(activity, MissionPhotoActivity::class.java)
                    .putExtra("photoId", id).putExtra("target", target).putExtra("localDataEpoch",epoch)) }
                catch (_: Exception) { cancel(activity, id, epoch) }
            }
            result(id, target, "capturing").toString()
        } catch (_: Exception) { result(id, target, "unavailable").toString() }
    }
    @JvmStatic fun poll(context: Context, id: String): String = synchronized(lock) {
        if (!idPattern.matches(id) || NativeLocalReset.pending(context)) return@synchronized "{\"status\":\"unavailable\"}"
        try {
            if(file(context,id,".cancelled").exists())return@synchronized result(id,"","cancelled").toString()
            val data = JSONObject(AtomicFile(file(context, id, ".json")).readFully().toString(Charsets.UTF_8))
            // A killed process cannot finish an old inference. Retain its photo for direct confirmation.
            if (data.optString("status") == "processing" && id !in active) data.put("status", "unavailable")
            if (data.optString("status") == "capturing" && id !in capturing) data.put("status", "unavailable")
            data.toString()
        } catch (_: Exception) { result(id, "", "unavailable").toString() }
    }
    @JvmStatic fun cancel(context: Context, id: String) = cancel(context,id,NativeLocalReset.epoch(context))
    internal fun cancel(context: Context, id: String, epoch: String): Unit = synchronized(lock) {
        if (!idPattern.matches(id)) return@synchronized
        capturing.remove(id)
        try {
            NativeLocalReset.write(context,epoch) {
                check(root(context).isDirectory || root(context).mkdirs())
                file(context, id, ".cancelled").writeText("")
                writeUnlocked(context, id, result(id, "", "cancelled"))
                raw(context, id).delete()
                AtomicFile(file(context,id,".jpg")).delete()
            }
        } catch (_: Exception) { /* Cleanup is retried by bounded orphan collection. */ }
    }
    @JvmStatic fun prune(context: Context, keepCsv: String) = synchronized(lock) {
        if(NativeLocalReset.pending(context))return@synchronized
        val keep = keepCsv.split(',').filter { idPattern.matches(it) }.toSet()
        val cutoff = System.currentTimeMillis() - 24 * 60 * 60 * 1000L
        for (dir in listOf(root(context), File(context.cacheDir, "mission_capture")))
            dir.listFiles()?.forEach { f ->
                val id = f.name.substringBefore('.')
                if (idPattern.matches(id) && id !in keep && id !in active && id !in capturing && f.lastModified() < cutoff && f.isFile) f.delete()
            }
    }
    /** The fence is already pending. Old workers may finish inference but can no longer write. */
    internal fun resetLocalData(context: Context) = synchronized(lock) {
        (capturing + active).forEach { id ->
            try {
                val uri=androidx.core.content.FileProvider.getUriForFile(context,"${context.packageName}.missionphotos",raw(context,id))
                context.revokeUriPermission(uri,Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION)
            } catch (_: IllegalArgumentException) { /* Missing provider has no live URI grant; still erase owned files. */ }
        }
        capturing.clear(); active.clear()
        LocalResetEpoch(context.noBackupFilesDir).eraseOwned("mission_photos")
        LocalResetEpoch(context.cacheDir).eraseOwned("mission_capture")
    }
    fun process(context: Context, id: String, target: String, epoch: String) {
        synchronized(lock) {
            if (!valid(id, target) || !NativeLocalReset.current(context,epoch) || !active.add(id)) return
            capturing.remove(id)
            try { if(!commitResult(context,id,epoch,result(id,target,"processing"))) { active.remove(id); return } }
            catch (_: Exception) { active.remove(id); raw(context, id).delete(); return }
        }
        worker.execute {
            var bitmap: Bitmap? = null
            try {
                if (!NativeLocalReset.current(context,epoch) || file(context, id, ".cancelled").exists()) return@execute
                bitmap = normalize(raw(context, id))
                val bytes=jpeg(bitmap)
                var committed=false
                synchronized(lock) {
                    NativeLocalReset.write(context,epoch) {
                        if(!file(context,id,".cancelled").exists()) {
                            val output = AtomicFile(file(context,id,".jpg")); val stream = output.startWrite()
                            try { stream.write(bytes); output.finishWrite(stream); committed=true }
                            catch (e: Exception) { output.failWrite(stream); throw e }
                        }
                    }
                }
                if(!committed || !commitResult(context,id,epoch,result(id,target,"processing")))return@execute
                val started = SystemClock.elapsedRealtime()
                val data = detect(context, id, target, bitmap)
                if (SystemClock.elapsedRealtime() - started > 5000) data.put("status", "unavailable").put("label", "").put("score", 0.0)
                commitResult(context,id,epoch,data)
            } catch (failure: Throwable) {
                if (failure !is Exception && failure !is LinkageError && failure !is OutOfMemoryError) throw failure
                try { commitResult(context,id,epoch,result(id,target,"unavailable")) } catch (_: Exception) { }
            } finally { bitmap?.recycle(); raw(context, id).delete(); active.remove(id) }
        }
    }
    private fun normalize(source: File): Bitmap {
        require(source.length() in 1..(40L * 1024 * 1024))
        val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
        BitmapFactory.decodeFile(source.path, bounds)
        require(bounds.outWidth > 0 && bounds.outHeight > 0 && bounds.outWidth.toLong() * bounds.outHeight <= 100_000_000)
        var sample = 1
        while (maxOf(bounds.outWidth, bounds.outHeight) / sample > 1600) sample *= 2
        val decoded = BitmapFactory.decodeFile(source.path, BitmapFactory.Options().apply { inSampleSize = sample; inPreferredConfig = Bitmap.Config.ARGB_8888 })
            ?: error("Cannot decode photo")
        try {
            val exif = ExifInterface(source.path)
            val matrix = Matrix().apply {
                when (exif.getAttributeInt(ExifInterface.TAG_ORIENTATION, ExifInterface.ORIENTATION_NORMAL)) {
                    2 -> setScale(-1f, 1f)
                    3 -> setRotate(180f)
                    4 -> { setRotate(180f); postScale(-1f, 1f) }
                    5 -> { setRotate(90f); postScale(-1f, 1f) }
                    6 -> setRotate(90f)
                    7 -> { setRotate(-90f); postScale(-1f, 1f) }
                    8 -> setRotate(-90f)
                }
                val scale = minOf(1f, 1280f / maxOf(decoded.width, decoded.height)); postScale(scale, scale)
            }
            val rotated = Bitmap.createBitmap(decoded, 0, 0, decoded.width, decoded.height, matrix, true)
            if (rotated !== decoded) decoded.recycle()
            return rotated
        } catch (e: Exception) { decoded.recycle(); throw e }
    }
    private fun jpeg(bitmap: Bitmap): ByteArray {
        for (quality in listOf(85, 75, 65, 55, 40)) {
            val bytes = ByteArrayOutputStream().use { out -> check(bitmap.compress(Bitmap.CompressFormat.JPEG, quality, out)); out.toByteArray() }
            if (bytes.size <= 1024 * 1024) return bytes
        }
        error("Photo size budget exceeded")
    }
    private fun detect(context: Context, id: String, target: String, bitmap: Bitmap): JSONObject {
        if (!verifiedModel) {
            val digest = MessageDigest.getInstance("SHA-256")
            context.assets.open(ASSET).use { stream -> val buffer = ByteArray(32768); while (true) { val n=stream.read(buffer); if(n<0)break; digest.update(buffer,0,n) } }
            check(digest.digest().joinToString("") { "%02x".format(it) } == SHA256)
            verifiedModel = true
        }
        val labels = if (target == "book") listOf("book") else listOf("cup", "bottle")
        val options = ObjectDetector.ObjectDetectorOptions.builder()
            .setBaseOptions(BaseOptions.builder().setModelAssetPath(ASSET).build())
            .setRunningMode(RunningMode.IMAGE).setScoreThreshold(.60f).setCategoryAllowlist(labels).setMaxResults(5).build()
        val data = result(id, target, "not_found")
        ObjectDetector.createFromOptions(context, options).use { detector ->
            BitmapImageBuilder(bitmap).build().use { image ->
                val category = detector.detect(image).detections().flatMap { it.categories() }
                    .filter { it.categoryName() in labels }.maxByOrNull { it.score() }
                if (category != null) data.put("status", "matched").put("label", category.categoryName()).put("score", category.score().toDouble())
            }
        }
        return data
    }
}
