package com.capstonedesign2026.platform

import android.app.Activity
import android.os.Build
import android.view.View
import android.view.WindowInsets
import kotlin.math.max
import kotlin.math.min

/** Read-only UI-thread snapshot. Does not replace Unity/GameActivity's inset listener. */
object SystemBarInsets {
    @JvmStatic
    @Suppress("DEPRECATION")
    fun read(activity: Activity, surface: View?): IntArray? {
        if (activity.isFinishing || activity.isDestroyed || surface == null || !surface.isAttachedToWindow) return null
        val decor = activity.window.decorView
        val insets = decor.rootWindowInsets ?: return null
        var left: Int
        var top: Int
        var right: Int
        var bottom: Int
        if (Build.VERSION.SDK_INT >= 30) {
            // IME is deliberately absent: activity content handles it independently.
            val bars = insets.getInsets(WindowInsets.Type.systemBars() or WindowInsets.Type.displayCutout())
            left = bars.left; top = bars.top; right = bars.right; bottom = bars.bottom
        } else {
            // API 29's system-window bottom includes IME. Cap it by the stable
            // navigation size, as AndroidX WindowInsetsCompat does for navigationBars.
            left = insets.systemWindowInsetLeft
            top = insets.systemWindowInsetTop
            right = insets.systemWindowInsetRight
            bottom = min(insets.systemWindowInsetBottom, insets.stableInsetBottom)
            insets.displayCutout?.let {
                left = max(left, it.safeInsetLeft); top = max(top, it.safeInsetTop)
                right = max(right, it.safeInsetRight); bottom = max(bottom, it.safeInsetBottom)
            }
        }
        val origin = IntArray(2)
        val viewport = IntArray(2)
        decor.getLocationInWindow(origin)
        surface.getLocationInWindow(viewport)
        return intArrayOf(decor.width, decor.height, viewport[0] - origin[0], viewport[1] - origin[1],
            surface.width, surface.height, left, top, right, bottom)
    }
}
