package com.capstonedesign2026.platform

import android.util.AtomicFile
import android.util.Log
import org.robolectric.annotation.Implementation
import org.robolectric.annotation.Implements
import java.io.File
import java.nio.file.Files
import java.nio.file.StandardCopyOption

/** Host-only Android rename semantics. Windows File.renameTo cannot replace an existing
 * file, unlike Android's rename syscall. Keep real AtomicFile start/fail/read/backup logic.
 * AOSP: frameworks/base/core/java/android/util/AtomicFile.java, private rename(). */
@Implements(AtomicFile::class)
class AtomicFileRenameShadow {
    companion object {
        @JvmStatic @Implementation
        fun rename(source: File, target: File) {
            if(target.isDirectory && !target.delete())Log.e("AtomicFile","Could not remove target directory")
            try { Files.move(source.toPath(),target.toPath(),StandardCopyOption.ATOMIC_MOVE,StandardCopyOption.REPLACE_EXISTING) }
            catch (_: Exception) { Log.e("AtomicFile","Atomic rename failed") }
        }
    }
}
