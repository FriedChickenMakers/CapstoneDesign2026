package com.capstonedesign2026.platform

import java.io.File
import java.io.RandomAccessFile
import java.util.ArrayDeque

/** Single-writer bounded JSONL storage. Contains no Android or personal fixture data. */
internal class BoundedSensorLog(private val file: File, private val maxBytes: Long) {
    fun append(lines: List<String>) {
        if (lines.isEmpty()) return
        val separator = if (file.exists() && file.length() > 0) RandomAccessFile(file, "r").use {
            it.seek(it.length() - 1)
            if (it.read() == 10) "" else "\n"
        } else ""
        val bytes = (separator + lines.joinToString("\n", postfix = "\n")).toByteArray(Charsets.UTF_8)
        require(bytes.size <= maxBytes) { "Sensor batch exceeds file limit" }
        val directory = requireNotNull(file.parentFile)
        check(directory.exists() || directory.mkdirs()) { "Cannot create sensor log directory" }
        if (file.exists() && file.length() + bytes.size > maxBytes) {
            val previous = File(directory, "sensor_samples.previous.jsonl")
            check(!previous.exists() || previous.delete()) { "Cannot remove previous sensor log" }
            check(file.renameTo(previous)) { "Cannot rotate sensor log" }
        }
        file.appendBytes(bytes)
    }

    fun tail(limit: Int): List<String> {
        require(limit > 0)
        if (!file.exists()) return emptyList()
        val tail = ArrayDeque<String>()
        file.useLines { lines -> lines.forEach { line ->
            tail.addLast(line)
            if (tail.size > limit) tail.removeFirst()
        } }
        return tail.toList()
    }
}
