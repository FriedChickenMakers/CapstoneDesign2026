package com.capstonedesign2026.platform

import java.io.File
import java.nio.file.Files
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlin.concurrent.thread

/** Synthetic temporary directories only; no Android context or real application paths. */
fun main(args:Array<String>) {
    if(args.firstOrNull()=="writer") {
        val root=File(args[1]);val epoch=args[2]
        check(LocalResetEpoch(root).write(epoch) {
            File(root,"child-ready").writeText("ready")
            val deadline=System.nanoTime()+TimeUnit.SECONDS.toNanos(15)
            while(!File(root,"child-release").exists()) {check(System.nanoTime()<deadline);Thread.sleep(10)}
            File(root,"platform/child.jsonl").apply {parentFile.mkdirs();writeText("last child write")}
        })
        return
    }
    val root=Files.createTempDirectory("capstone-reset-epoch-").toFile()
    val outside=Files.createTempDirectory("capstone-reset-negative-").toFile()
    var assertions=0
    fun verify(value:Boolean,message:String) {assertions++;check(value){message}}
    fun rejects(message:String,action:()->Unit) {var failed=false;try{action()}catch(_:Exception){failed=true};verify(failed,message)}
    try {
        val fence=LocalResetEpoch(root)
        verify(fence.snapshot()==LocalResetEpoch.State(),"new installation has empty epoch")
        val file=File(root,"platform/sensor_samples.jsonl")
        verify(fence.write(""){file.parentFile.mkdirs();file.writeText("synthetic-old")},"current writer is accepted")
        verify(fence.begin("reset-1"),"first reset begins")
        verify(fence.snapshot().pending,"pending is durable")
        verify(!fence.write(""){file.writeText("stale")},"old writer rejected during reset")
        verify(!fence.write("reset-1"){file.writeText("premature")},"new writer blocked until completion")
        rejects("different reset cannot replace pending transaction"){fence.begin("reset-2")}
        rejects("partial cleanup is not acknowledged"){fence.finish("reset-1"){fence.eraseOwned("platform");error("synthetic IO failure")}}
        verify(!file.exists() && fence.snapshot().pending,"failed cleanup remains pending")
        val reopened=LocalResetEpoch(root)
        verify(reopened.begin("reset-1"),"same operation resumes after restart")
        reopened.finish("reset-1"){reopened.eraseOwned("platform")}
        verify(!reopened.snapshot().pending && reopened.snapshot().id=="reset-1","completed generation retained")
        verify(!reopened.begin("reset-1"),"completed duplicate is acknowledgment")
        verify(!reopened.write(""){file.parentFile.mkdirs();file.writeText("resurrected")},"late old task cannot recreate files")
        verify(!file.exists(),"old file stays absent")
        verify(reopened.write("reset-1"){file.parentFile.mkdirs();file.writeText("new")},"new collector may write fresh data")
        reopened.finish("reset-1"){error("duplicate must not erase new records")}
        verify(file.readText()=="new","duplicate completion preserves later records")
        listOf("", "../"+outside.name, outside.absolutePath, ".").forEach { bad ->
            rejects("outside/root path must be rejected"){reopened.eraseOwned(bad)}
        }
        File(outside,"keep").writeText("synthetic-owned-by-other")
        verify(File(outside,"keep").exists(),"outside data retained")
        // A live write finishes before the cross-thread reset obtains the same file lock.
        val entered=CountDownLatch(1);val release=CountDownLatch(1);val begun=CountDownLatch(1)
        val writer=thread {reopened.write("reset-1"){entered.countDown();check(release.await(10,TimeUnit.SECONDS));file.writeText("last old row")}}
        verify(entered.await(10,TimeUnit.SECONDS),"writer acquired lease")
        val reset=thread {LocalResetEpoch(root).begin("reset-2");begun.countDown()}
        verify(!begun.await(100,TimeUnit.MILLISECONDS),"reset waits for outstanding writer")
        release.countDown();writer.join();reset.join()
        verify(begun.count==0L,"reset acquires fence after writer")
        reopened.finish("reset-2"){reopened.eraseOwned("platform")}
        verify(!file.exists(),"cleanup occurs after last old write")
        verify(!reopened.write("reset-1"){file.parentFile.mkdirs();file.writeText("late")},"previous generation is still fenced")
        rejects("invalid id cannot be stored"){reopened.begin("../../bad")}
        verify(reopened.snapshot().id=="reset-2","invalid request preserves receipt")
        // A second JVM has a different monitor and emulates Android's :sensor process.
        val java=File(System.getProperty("java.home"),"bin/"+if(System.getProperty("os.name").startsWith("Windows"))"javaw.exe" else "java")
        val child=ProcessBuilder(java.path,"-cp",System.getProperty("java.class.path"),
            "com.capstonedesign2026.platform.LocalResetEpochTestsKt","writer",root.path,"reset-2").redirectErrorStream(true).start()
        val deadline=System.nanoTime()+TimeUnit.SECONDS.toNanos(15)
        while(!File(root,"child-ready").exists() && child.isAlive && System.nanoTime()<deadline)Thread.sleep(10)
        verify(File(root,"child-ready").exists(),"separate JVM acquired file lease")
        val crossBegun=CountDownLatch(1)
        val cross=thread {LocalResetEpoch(root).begin("reset-3");crossBegun.countDown()}
        verify(!crossBegun.await(100,TimeUnit.MILLISECONDS),"reset waits on another process's OS file lock")
        File(root,"child-release").writeText("release")
        verify(child.waitFor(15,TimeUnit.SECONDS) && child.exitValue()==0,"child writer completed without error")
        cross.join(15000)
        verify(crossBegun.count==0L,"cross-process reset acquired the released lock")
        reopened.finish("reset-3"){reopened.eraseOwned("platform")}
        verify(!File(root,"platform/child.jsonl").exists(),"cross-process final write removed before completion")
        verify(!reopened.write("reset-2"){error("previous process epoch entered")},"old process epoch cannot resume")
        rejects("non-ASCII id rejected consistently"){reopened.begin("한글")}
        // Corrupt journal is never interpreted as a clean installation.
        File(root,"app-local-reset.epoch").writeText("broken")
        rejects("corrupt journal blocks writes"){reopened.write(""){file.writeText("bad")}}
        println("LocalResetEpoch: $assertions assertions PASS")
    } finally {
        check(root.name.startsWith("capstone-reset-epoch-") && outside.name.startsWith("capstone-reset-negative-"))
        root.deleteRecursively();outside.deleteRecursively()
    }
}
