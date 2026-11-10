using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using CapstoneDesign.Runtime.Personalization;

static class AutomaticEnrollmentTests
{
    const string Endpoint = "https://telemetry.example.test";
    // Deliberately synthetic fixture; never a deployed admission key.
    static readonly string Key = new string('a', 64);
    static int assertions;
    static void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
    static void Throws(Action action, string message)
    { bool threw = false; try { action(); } catch (Exception) { threw = true; } Check(threw, message); }
    static string Serialize(AutomaticEnrollmentRecord record)
    {
        using (var stream = new MemoryStream())
        {
            new DataContractJsonSerializer(typeof(AutomaticEnrollmentRecord)).WriteObject(stream, record);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
    static AutomaticEnrollmentRecord Deserialize(string value)
    {
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(value)))
            return (AutomaticEnrollmentRecord)new DataContractJsonSerializer(typeof(AutomaticEnrollmentRecord)).ReadObject(stream);
    }
    static string Final(string directory) => Path.Combine(directory, AutomaticEnrollment.FileName);
    static string Pending(string directory) => Final(directory) + ".new";
    static AutomaticEnrollmentResult Ensure(string directory, bool blocked = false) =>
        AutomaticEnrollment.EnsureCreated(directory, Endpoint, Key, Serialize, Deserialize, blocked);
    static string Create(string root, string name)
    { string directory = Path.Combine(root, name); Directory.CreateDirectory(directory); return directory; }
    static AutomaticEnrollmentRecord Synthetic() => new AutomaticEnrollmentRecord
        { endpoint = Endpoint, token = new string('b', 64), automaticEnrollmentKey = Key };

    static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "capstone-auto-enrollment-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var first = Create(root, "first");
            Check(Ensure(first) == AutomaticEnrollmentResult.Created, "Offline first use creates a committed identity without a network dependency");
            string firstBytes = File.ReadAllText(Final(first));
            var record = Deserialize(firstBytes);
            Check(record.endpoint == Endpoint && record.automaticEnrollmentKey == Key, "Admission settings round-trip into private storage");
            Check(AutomaticEnrollment.IsLowerHex64(record.token), "Identity has the native/server 256-bit lowercase token format");
            Check(!File.Exists(Pending(first)), "Successful rename leaves no pending copy");
            Check(Ensure(first) == AutomaticEnrollmentResult.Existing && File.ReadAllText(Final(first)) == firstBytes,
                "Repeated initialization keeps exact committed identity and bytes");
            var other = Create(root, "other-install"); Ensure(other);
            Check(Deserialize(File.ReadAllText(Final(other))).token != record.token, "Different installations obtain independent identities");

            var manual = Create(root, "manual");
            const string manualBytes = "{\"endpoint\":\"https://manual.example.test\",\"token\":\"synthetic-manual-participant-token\"}";
            File.WriteAllText(Final(manual), manualBytes);
            File.WriteAllText(Pending(manual), "stale incomplete copy");
            Check(AutomaticEnrollment.EnsureCreated(manual, "bad config", null, null, null) == AutomaticEnrollmentResult.Existing,
                "Existing manual enrollment does not depend on a later build's automatic config");
            Check(File.ReadAllText(Final(manual)) == manualBytes, "Manual registration remains byte-for-byte untouched");

            var recovery = Create(root, "recover-after-rename-failure");
            Directory.CreateDirectory(Final(recovery)); // Filesystem rejects rename after the durable pending write.
            Throws(() => Ensure(recovery), "Commit failure is visible to the caller");
            Check(!File.Exists(Final(recovery)) && File.Exists(Pending(recovery)), "Failed commit cannot advertise a usable enrollment");
            string recoveryBytes = File.ReadAllText(Pending(recovery));
            Directory.Delete(Final(recovery));
            Check(Ensure(recovery) == AutomaticEnrollmentResult.Recovered, "Next process can finish the existing durable pending identity");
            Check(File.ReadAllText(Final(recovery)) == recoveryBytes, "Retry commits the original token without generating another");

            var corrupt = Create(root, "partial-pending-write");
            File.WriteAllText(Pending(corrupt), "{\"token\":");
            Check(Ensure(corrupt) == AutomaticEnrollmentResult.Created, "An uncommitted partial JSON write is replaced on retry");
            Check(AutomaticEnrollment.IsLowerHex64(Deserialize(File.ReadAllText(Final(corrupt))).token)
                && !File.Exists(Pending(corrupt)), "Only a valid replacement reaches final storage and can be activated");
            foreach (string incomplete in new[] { "", "{}", "{\"token\":\"synthetic-invalid\"}", new string('x', 4097) })
            {
                var partial = Create(root, "incomplete-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(Pending(partial), incomplete);
                Check(Ensure(partial) == AutomaticEnrollmentResult.Created
                    && AutomaticEnrollment.IsLowerHex64(Deserialize(File.ReadAllText(Final(partial))).token),
                    "Empty, missing-field, invalid-token and oversized uncommitted files recover without operator enrollment");
            }
            var mismatched = Create(root, "pending-key-mismatch");
            var old = Synthetic(); old.automaticEnrollmentKey = new string('c', 64);
            string oldBytes = Serialize(old); File.WriteAllText(Pending(mismatched), oldBytes);
            Throws(() => Ensure(mismatched), "An interrupted identity is not silently repointed to another deployment key");
            Check(File.ReadAllText(Pending(mismatched)) == oldBytes && !File.Exists(Final(mismatched)), "Mismatched pending identity is preserved");
            var wrongEndpoint = Create(root, "pending-endpoint-mismatch");
            var moved = Synthetic(); moved.endpoint = "https://different.example.test";
            string movedBytes = Serialize(moved); File.WriteAllText(Pending(wrongEndpoint), movedBytes);
            Throws(() => Ensure(wrongEndpoint), "A valid identity for a different endpoint is not treated as a partial write");
            Check(File.ReadAllText(Pending(wrongEndpoint)) == movedBytes && !File.Exists(Final(wrongEndpoint)),
                "Valid cross-endpoint pending credentials remain untouched");

            foreach (string name in new[] { "pending-deletion", "revoked", "existing-bound-queue" })
            {
                var blocked = Create(root, name);
                Check(Ensure(blocked, true) == AutomaticEnrollmentResult.Blocked, "Caller durable state can prevent automatic identity recreation");
                Check(!File.Exists(Final(blocked)) && !File.Exists(Pending(blocked)), "Blocked state creates no replacement enrollment");
            }
            var reset = Create(root, "reset-marker");
            File.WriteAllText(Path.Combine(reset, AutomaticEnrollment.ResetMarker), "synthetic-reset");
            File.WriteAllText(Pending(reset), Serialize(Synthetic()));
            Check(Ensure(reset) == AutomaticEnrollmentResult.Blocked && !File.Exists(Final(reset)), "Reset marker blocks recovery as well as new creation");
            File.WriteAllText(Path.Combine(manual, AutomaticEnrollment.ResetMarker), "synthetic-reset");
            Check(Ensure(manual) == AutomaticEnrollmentResult.Blocked && File.ReadAllText(Final(manual)) == manualBytes,
                "A reset marker wins over an existing registration without destroying its evidence");

            var duringWrite = Create(root, "reset-during-write");
            var resetResult = AutomaticEnrollment.EnsureCreated(duringWrite, Endpoint, Key, value =>
            {
                File.WriteAllText(Path.Combine(duringWrite, AutomaticEnrollment.ResetMarker), "synthetic-reset");
                return Serialize(value);
            }, Deserialize);
            Check(resetResult == AutomaticEnrollmentResult.Blocked && !File.Exists(Final(duringWrite)),
                "Reset committed during preparation prevents final identity activation");

            var serializationFailure = Create(root, "serialization-failure");
            Throws(() => AutomaticEnrollment.EnsureCreated(serializationFailure, Endpoint, Key,
                value => { throw new IOException("Synthetic serialization failure"); }, Deserialize), "Preparation error reaches caller");
            Check(!File.Exists(Pending(serializationFailure)) && !File.Exists(Final(serializationFailure)), "Failed preparation leaves no fake enrollment");
            Check(Ensure(serializationFailure) == AutomaticEnrollmentResult.Created, "Retry after transient preparation failure succeeds");
            var changed = Create(root, "identity-changing-serializer");
            Throws(() => AutomaticEnrollment.EnsureCreated(changed, Endpoint, Key,
                value => Serialize(Synthetic()), Deserialize), "Serializer may not substitute a different identity");
            Check(!File.Exists(Pending(changed)) && !File.Exists(Final(changed)), "Changed identity never reaches disk");

            var concurrent = Create(root, "concurrent-initialization");
            var attempts = Enumerable.Range(0, 12).Select(i => Task.Run(() => Ensure(concurrent))).ToArray();
            Task.WaitAll(attempts);
            Check(attempts.Count(task => task.Result == AutomaticEnrollmentResult.Created) == 1
                && attempts.Count(task => task.Result == AutomaticEnrollmentResult.Existing) == 11,
                "Concurrent callers commit exactly one identity");
            Check(AutomaticEnrollment.IsLowerHex64(Deserialize(File.ReadAllText(Final(concurrent))).token)
                && !File.Exists(Pending(concurrent)), "Concurrent initialization leaves one valid durable file");

            Check(AutomaticEnrollment.ValidConfiguration(Endpoint + "/", Key), "Root trailing slash is allowed and normalized");
            foreach (string endpoint in new[] { null, "", "http://telemetry.example.test", "file:///tmp/synthetic", Endpoint + "/nested",
                Endpoint + "?secret=synthetic", Endpoint + "#fragment", "https://synthetic@telemetry.example.test" })
                Check(!AutomaticEnrollment.ValidConfiguration(endpoint, Key), "Unsafe/nonroot enrollment endpoints are rejected");
            foreach (string key in new[] { null, "", new string('a', 63), new string('a', 65), new string('A', 64), new string('g', 64) })
                Check(!AutomaticEnrollment.ValidConfiguration(Endpoint, key), "Noncanonical admission keys are rejected");
            var invalid = Create(root, "invalid-build-config");
            Throws(() => AutomaticEnrollment.EnsureCreated(invalid, "http://telemetry.example.test", Key, Serialize, Deserialize),
                "Missing/invalid build configuration cannot create enrollment");
            Check(!File.Exists(Final(invalid)) && !File.Exists(Pending(invalid)), "Invalid config leaves storage untouched");
            Console.WriteLine("AUTOMATIC_ENROLLMENT_PASS assertions=" + assertions);
        }
        finally { Directory.Delete(root, true); }
    }
}
