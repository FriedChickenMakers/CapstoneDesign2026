package com.capstonedesign2026.platform

import java.io.File
import java.io.FileOutputStream
import java.io.RandomAccessFile
import java.nio.file.Files
import java.nio.file.StandardCopyOption

/** A durable, process-shared fence. Kept outside the data allowlist being erased. */
internal class LocalResetEpoch(private val root: File) {
    companion object { private val monitor = Any() }
    data class State(val id: String = "", val pending: Boolean = false)
    private val journal get() = File(root, "app-local-reset.epoch")
    private fun <T> locked(action: () -> T): T = synchronized(monitor) {
        check(root.isDirectory || root.mkdirs()) { "Reset directory unavailable" }
        RandomAccessFile(File(root, "app-local-reset.lock"), "rw").use { file ->
            file.channel.lock().use { action() }
        }
    }
    private fun read(): State {
        if (!journal.exists()) return State()
        val parts = journal.readText().split('\n')
        check(parts.size == 2 && validId(parts[0]) && parts[1] in listOf("pending", "complete")) { "Reset journal is invalid" }
        return State(parts[0], parts[1] == "pending")
    }
    private fun save(value: State) {
        val temp = File(root, "app-local-reset.epoch.tmp")
        FileOutputStream(temp).use { it.write((value.id + "\n" + if (value.pending) "pending" else "complete").toByteArray()); it.fd.sync() }
        Files.move(temp.toPath(), journal.toPath(), StandardCopyOption.ATOMIC_MOVE, StandardCopyOption.REPLACE_EXISTING)
    }
    fun snapshot(): State = locked { read() }
    fun isCurrent(id: String): Boolean = locked { read().let { !it.pending && it.id == id } }
    fun write(id: String, action: () -> Unit): Boolean = locked {
        val value = read()
        if (value.pending || value.id != id) false else { action(); true }
    }
    /** false is an already completed receipt; a pending retry retains its epoch. */
    fun begin(id: String): Boolean = locked {
        require(validId(id)) { "Invalid reset identifier" }
        val value = read()
        check(!value.pending || value.id == id) { "Another reset is pending" }
        if (value.id == id && !value.pending) false else { if (value.id != id) save(State(id, true)); true }
    }
    fun finish(id: String, erase: (File) -> Unit) = locked {
        val value = read()
        check(value.id == id) { "Reset identifier changed" }
        if (value.pending) { erase(root); save(State(id, false)) }
    }
    /** Resolve before deleting; a symlink must never escape the explicitly owned directory. */
    fun eraseOwned(relative: String) {
        val base = root.canonicalFile
        val supplied = File(base, relative).absoluteFile.toPath().normalize().toFile()
        val target = supplied.canonicalFile
        require(relative.isNotEmpty() && target == supplied && target != base && target.toPath().startsWith(base.toPath())) { "Unsafe reset path" }
        fun erase(file: File) {
            val canonical = file.canonicalFile
            check(canonical == file.absoluteFile.toPath().normalize().toFile() && canonical.toPath().startsWith(target.toPath())) { "Reset child escapes owned path" }
            if (file.isDirectory) (file.listFiles() ?: error("Reset directory cannot be read")).forEach(::erase)
            check(!file.exists() || file.delete()) { "Local record could not be removed" }
        }
        erase(target)
    }
    private fun validId(id: String) = id.length in 1..128 && id.all { it in 'a'..'z' || it in 'A'..'Z' || it in '0'..'9' || it in "-_:" }
}
