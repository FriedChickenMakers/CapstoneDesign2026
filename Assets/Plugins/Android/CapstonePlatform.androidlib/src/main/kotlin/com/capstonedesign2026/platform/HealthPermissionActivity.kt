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
            .setTitle(if (health) "Health Connect access" else "App permissions")
            .setMessage(if (health)
                "Health Connect access is off. Open its permissions page to allow the health data you want to share."
                else "Activity or notification permission is off. Open this app's settings to allow it.")
            .setPositiveButton("Open settings") { _, _ ->
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
            .setNegativeButton("Later") { _, _ -> finish() }
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
                text = "Health data privacy"
                textSize = 24f
            })
            addView(TextView(context).apply {
                text = "This prototype reads only the Health Connect categories you approve: " +
                    "steps, sleep, heart rate, and exercise. Data is used locally to show the " +
                    "debug wellness view and is not used for diagnosis, treatment, ranking, or punishment."
                textSize = 17f
                setPadding(0, padding, 0, 0)
            })
        }
        setContentView(ScrollView(this).apply { addView(content) })
    }
}
