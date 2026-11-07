package com.capstonedesign2026.platform

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.result.contract.ActivityResultContracts
import androidx.core.content.FileProvider

/** System camera owns the camera UI; only this request's cache URI is shared. */
class MissionPhotoActivity : ComponentActivity() {
    private val id get() = intent.getStringExtra("photoId").orEmpty()
    private val target get() = intent.getStringExtra("target").orEmpty()
    private val capture = registerForActivityResult(ActivityResultContracts.TakePicture()) { success ->
        if (MissionPhotoController.valid(id, target)) {
            val uri = FileProvider.getUriForFile(this, "$packageName.missionphotos", MissionPhotoController.raw(this, id))
            revokeUriPermission(uri, Intent.FLAG_GRANT_WRITE_URI_PERMISSION or Intent.FLAG_GRANT_READ_URI_PERMISSION)
            if (success) MissionPhotoController.process(applicationContext, id, target)
            else MissionPhotoController.cancel(applicationContext, id)
        }
        finish()
    }
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        if (!MissionPhotoController.valid(id, target)) { finish(); return }
        MissionPhotoController.resumeCapture(id)
        if (savedInstanceState != null) return // ActivityResultRegistry restores the pending camera result.
        try {
            val uri = FileProvider.getUriForFile(this, "$packageName.missionphotos", MissionPhotoController.raw(this, id))
            capture.launch(uri)
        } catch (_: Exception) {
            MissionPhotoController.cancel(applicationContext, id)
            finish()
        }
    }
}
