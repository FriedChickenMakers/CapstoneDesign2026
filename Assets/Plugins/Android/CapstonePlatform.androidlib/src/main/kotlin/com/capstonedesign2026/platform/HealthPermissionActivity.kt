package com.capstonedesign2026.platform

import android.app.Activity
import android.app.AlertDialog
import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.health.connect.HealthConnectManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import androidx.activity.ComponentActivity
import androidx.activity.result.contract.ActivityResultContracts
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.HealthConnectFeatures
import androidx.health.connect.client.PermissionController
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.StepsRecord
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class HealthPermissionActivity : ComponentActivity() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private var settingsLaunched = false
    private var healthSettingsLaunched = false
    private val runtimeLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions(),
    ) {
        requestHealthPermissions()
    }

    private val permissionLauncher = registerForActivityResult(
        PermissionController.createRequestPermissionResultContract(),
    ) {
        PermissionState.markHealthRequestCompleted(applicationContext)
        HealthRepository.refresh()
        finish()
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        HealthRepository.initialize(applicationContext)
        if (intent.getBooleanExtra("automatic", false) || intent.getBooleanExtra("rewardReminder", false)) {
            val missing = buildList {
                if (intent.getBooleanExtra("automatic", false) && Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q &&
                    checkSelfPermission(Manifest.permission.ACTIVITY_RECOGNITION) != PackageManager.PERMISSION_GRANTED)
                    add(Manifest.permission.ACTIVITY_RECOGNITION)
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
                    checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED)
                    add(Manifest.permission.POST_NOTIFICATIONS)
            }
            val requestable = missing.filter { !PermissionState.wasRuntimeRequested(this, it) }
            if (requestable.isNotEmpty()) {
                requestable.forEach { PermissionState.markRuntimeRequested(this, it) }
                runtimeLauncher.launch(requestable.toTypedArray())
                return
            }
        }
        requestHealthPermissions()
    }

    private fun requestHealthPermissions() {
        if ((intent.getBooleanExtra("automatic", false) || intent.getBooleanExtra("rewardReminder", false)) && hasDeniedRuntimePermission()) {
            if (settingsLaunched) finish() else showSettingsPrompt(health = false)
            return
        }
        if (HealthConnectClient.getSdkStatus(this) != HealthConnectClient.SDK_AVAILABLE) {
            finish()
            return
        }
        scope.launch {
            try {
                val client = HealthConnectClient.getOrCreate(this@HealthPermissionActivity)
                val granted = withContext(Dispatchers.IO) { client.permissionController.getGrantedPermissions() }
                val required = if (intent.getBooleanExtra("rewardReminder", false)) {
                    buildSet {
                        add(HealthPermission.getReadPermission(StepsRecord::class))
                        if (client.features.getFeatureStatus(HealthConnectFeatures.FEATURE_READ_HEALTH_DATA_IN_BACKGROUND)
                            == HealthConnectFeatures.FEATURE_STATUS_AVAILABLE)
                            add(HealthPermission.PERMISSION_READ_HEALTH_DATA_IN_BACKGROUND)
                    }
                } else HealthRepository.requiredPermissions()
                val missing = required - granted
                if (missing.isEmpty() ||
                    (intent.getBooleanExtra("automatic", false) &&
                        PermissionState.wasHealthRequestCompleted(this@HealthPermissionActivity))) {
                    if (missing.isNotEmpty()) showSettingsPrompt(health = true)
                    else {
                        HealthRepository.refresh()
                        finish()
                    }
                } else {
                    permissionLauncher.launch(missing)
                }
            } catch (_: Exception) {
                HealthRepository.refresh()
                finish()
            }
        }
    }

    private fun hasDeniedRuntimePermission(): Boolean =
        (!intent.getBooleanExtra("rewardReminder", false) && Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q &&
            checkSelfPermission(Manifest.permission.ACTIVITY_RECOGNITION) != PackageManager.PERMISSION_GRANTED &&
            PermissionState.wasRuntimeRequested(this, Manifest.permission.ACTIVITY_RECOGNITION)) ||
        (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
            checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED &&
            PermissionState.wasRuntimeRequested(this, Manifest.permission.POST_NOTIFICATIONS))

    private fun showSettingsPrompt(health: Boolean) {
        AlertDialog.Builder(this)
            .setTitle(if (health) "건강 기록 연결" else "앱 권한 설정")
            .setMessage(if (health)
                "헬스 커넥트에서 연결할 건강 기록을 선택해 주세요."
                else "신체 활동 또는 알림 권한이 꺼져 있어요. 앱 설정에서 허용할 수 있어요.")
            .setPositiveButton("설정 열기") { _, _ ->
                val destination = if (health && Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE)
                    Intent(HealthConnectManager.ACTION_MANAGE_HEALTH_PERMISSIONS)
                        .putExtra(Intent.EXTRA_PACKAGE_NAME, packageName)
                else if (health) Intent(HealthConnectClient.ACTION_HEALTH_CONNECT_SETTINGS)
                else appSettingsIntent()
                settingsLaunched = true
                healthSettingsLaunched = health
                try { startActivity(destination) }
                catch (_: Exception) {
                    try { startActivity(appSettingsIntent()) }
                    catch (_: Exception) { finish() }
                }
            }
            .setNegativeButton("나중에") { _, _ -> finish() }
            .setOnCancelListener { finish() }
            .show()
    }

    override fun onResume() {
        super.onResume()
        if (!settingsLaunched) return
        if (healthSettingsLaunched || hasDeniedRuntimePermission()) {
            HealthRepository.refresh()
            finish()
        } else {
            requestHealthPermissions()
        }
    }

    private fun appSettingsIntent(): Intent = Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
        Uri.parse("package:$packageName"))

    override fun onDestroy() {
        scope.cancel()
        super.onDestroy()
    }
}

class HealthPermissionsRationaleActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val padding = (24 * resources.displayMetrics.density).toInt()
        val content = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(padding, padding, padding, padding)
            addView(TextView(context).apply {
                text = "건강 기록과 개인정보"
                textSize = 24f
            })
            addView(TextView(context).apply {
                text = "허용한 걸음 수, 수면, 심박수, 운동 기록만 헬스 커넥트에서 읽어요. " +
                    "기록은 기기 안에서 건강 정보 표시와 활동 목표 조절에 사용해요. " +
                    "진단·치료·순위 평가나 불이익에는 사용하지 않아요."
                textSize = 17f
                setPadding(0, padding, 0, 0)
            })
        }
        setContentView(ScrollView(this).apply { addView(content) })
    }
}
