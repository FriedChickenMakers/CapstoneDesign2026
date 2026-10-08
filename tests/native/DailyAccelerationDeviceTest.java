package com.capstonedesign2026.platform;

import android.content.ContextWrapper;
import java.io.File;
import java.nio.file.Files;
import java.util.List;
import org.json.JSONObject;

/** Run with app_process and the installed APK, against an isolated files directory, never user records. */
public final class DailyAccelerationDeviceTest {
    private static int checks;
    private static void verify(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
        checks++;
    }
    public static void main(String[] args) throws Exception {
        final File root = new File(args[0]);
        if (root.exists()) throw new IllegalArgumentException("Use a new isolated test directory");
        root.mkdirs();
        ContextWrapper context = new ContextWrapper(null) {
            @Override public File getFilesDir() { return root; }
        };
        DailyAccelerationStore store = DailyAccelerationStore.INSTANCE;
        SensorWindowAccumulator a = new SensorWindowAccumulator(60000, 0, 0);
        a.add("acceleration", 1000, 0, 0, 8);
        a.add("acceleration", 2000, 0, 0, 12);
        store.append(context, a.drainAcceleration(), true);
        File file = new File(root, "platform/daily_acceleration/daily-19700101.jsonl");
        JSONObject first = new JSONObject(Files.readAllLines(file.toPath()).get(0));
        verify(first.getLong("accelerationSamples") == 2, "burst must be saved before minute ends");
        verify(first.getBoolean("partial"), "burst is a partial minute");
        // Stop/restart within the same minute: merge the independent new sample, not duplicate the bucket.
        SensorWindowAccumulator restarted = new SensorWindowAccumulator(60000, 0, 0);
        restarted.add("acceleration", 10000, 0, 0, 16);
        store.append(context, restarted.drainAcceleration(), true);
        List<String> rows = Files.readAllLines(file.toPath());
        JSONObject merged = new JSONObject(rows.get(0));
        verify(rows.size() == 1, "one row per minute after restart");
        verify(merged.getLong("accelerationSamples") == 3, "all checkpoint samples preserved");
        verify(Math.abs(merged.getDouble("meanAccelerationMagnitudeMps2") - 12.0) < 1e-9, "sample weighted merge");
        store.append(context, restarted.closeThrough(60000).get(0), false);
        merged = new JSONObject(Files.readAllLines(file.toPath()).get(0));
        verify(!merged.getBoolean("partial") && merged.getLong("accelerationSamples") == 3, "minute close must retain prior checkpoint");
        store.append(context, restarted.closeThrough(120000).get(0), false);
        JSONObject empty = new JSONObject(Files.readAllLines(file.toPath()).get(1));
        verify(empty.getString("status").equals("NO_SAMPLES") && empty.isNull("meanAccelerationMagnitudeMps2"), "gap cannot become zero");
        // A legacy incomplete tail must not swallow the next valid append.
        Files.write(file.toPath(), "{broken".getBytes(), java.nio.file.StandardOpenOption.APPEND);
        restarted.add("acceleration", 121000, 0, 0, 9);
        store.append(context, restarted.drainAcceleration(), true);
        rows = Files.readAllLines(file.toPath());
        verify(rows.size() == 3, "valid earlier rows survive malformed tail recovery");
        for (String row : rows) new JSONObject(row);
        verify(store.readRecent(context, 180000).size() == 2, "chart excludes empty minutes");
        DailyAccelerationStatus status = DailyAccelerationStatus.INSTANCE;
        status.write(context, "STOPPED", "USER_STOP", 1, 2, 3);
        verify(status.read(context).getString("reason").equals("USER_STOP"), "stop reason is durable");
        File statusFile = new File(root, "platform/daily_acceleration/status.json");
        File pending = new File(statusFile.getPath() + ".new");
        Files.write(pending.toPath(), "pending writer".getBytes());
        verify(status.read(context).getString("reason").equals("USER_STOP"), "reader sees committed status during write");
        verify(pending.exists(), "UI status read must not delete another process's in-flight atomic write");
        pending.delete();
        Files.write(statusFile.toPath(), new JSONObject().put("status", "RUNNING").put("updatedEpochMs", 1).toString().getBytes());
        verify(status.read(context).getString("status").equals("INTERRUPTED"), "stale status cannot claim active collection");
        // This operation must fail rather than silently succeeding with an unwritable daily target.
        final File badRoot = new File(root, "bad");
        Files.write(badRoot.toPath(), "not a directory".getBytes());
        ContextWrapper bad = new ContextWrapper(null) { @Override public File getFilesDir() { return badRoot; } };
        boolean failed = false;
        try { store.append(bad, firstSummary(), true); } catch (Exception expected) { failed = true; }
        verify(failed, "write failure must propagate");
        System.out.println("PASS " + checks + " daily acceleration device assertions");
    }
    private static SensorWindowAccumulator.Summary firstSummary() {
        SensorWindowAccumulator a = new SensorWindowAccumulator(60000, 0, 0);
        a.add("acceleration", 1000, 0, 0, 9);
        return a.drainAcceleration();
    }
}
