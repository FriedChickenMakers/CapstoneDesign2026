package com.capstonedesign2026.platform

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.result.contract.ActivityResultContracts
import androidx.core.content.FileProvider

/** System camera owns the camera UI; only this request's cache URI is shared. */
class MissionPhotoActivity : ComponentActivity() {
    private val id get() = intent.getStringExtra("photoId").orEmpty()
    private val epoch get() = intent.getStringExtra("localDataEpoch")
    private val target get() = intent.getStringExtra("target").orEmpty()
    private val capture = registerForActivityResult(ActivityResultContracts.TakePicture()) { success ->
        if (MissionPhotoController.valid(id, target)) {
            val uri = FileProvider.getUriForFile(this, "$packageName.missionphotos", MissionPhotoController.raw(this, id))
            revokeUriPermission(uri, Intent.FLAG_GRANT_WRITE_URI_PERMISSION or Intent.FLAG_GRANT_READ_URI_PERMISSION)
            val capturedEpoch=epoch
            if(capturedEpoch != null) {
                if (success) MissionPhotoController.process(applicationContext, id, target, capturedEpoch)
                else MissionPhotoController.cancel(applicationContext, id, capturedEpoch)
            }
        }
        finish()
    }
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        if (!MissionPhotoController.valid(id, target)) { finish(); return }
        val capturedEpoch=epoch
        if(capturedEpoch == null || !MissionPhotoController.resumeCapture(this,id,target,capturedEpoch)) { finish(); return }
        if (savedInstanceState != null) return // ActivityResultRegistry restores the pending camera result.
        try {
            if(!MissionPhotoController.prepareCapture(this,id,capturedEpoch)) { finish(); return }
            val uri = FileProvider.getUriForFile(this, "$packageName.missionphotos", MissionPhotoController.raw(this, id))
            capture.launch(uri)
        } catch (_: Exception) {
            MissionPhotoController.cancel(applicationContext, id, capturedEpoch)
            finish()
        }
    }
}
