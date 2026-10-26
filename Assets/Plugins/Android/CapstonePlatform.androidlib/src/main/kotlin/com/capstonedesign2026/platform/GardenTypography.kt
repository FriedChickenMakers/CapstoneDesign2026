package com.capstonedesign2026.platform

import android.content.Context
import android.graphics.Typeface

/** Matches Unity's bundled static faces instead of synthesizing bold glyphs. */
internal object GardenTypography {
    private val faces = mutableMapOf<String, Typeface>()

    @Synchronized
    fun face(context: Context, emphasized: Boolean = false, control: Boolean = false): Typeface {
        val weight = if (emphasized) "SemiBold" else if (control) "Medium" else "Regular"
        return faces.getOrPut(weight) {
            Typeface.createFromAsset(context.applicationContext.assets, "fonts/WantedSans-$weight.ttf")
        }
    }
}
