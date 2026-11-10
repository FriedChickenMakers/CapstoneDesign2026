using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CapstoneDesign.Runtime.Personalization
{
    [Serializable]
    public sealed class AutomaticEnrollmentRecord
    {
        public string endpoint, token, automaticEnrollmentKey;
    }

    public enum AutomaticEnrollmentResult { Existing, Created, Recovered, Blocked }

    // Only prepares an app-private identity. No network request or collection is
    // authorized until the caller successfully reads the committed final file.
    public static class AutomaticEnrollment
    {
        public const string FileName = "personalization-enrollment.json";
        public const string ResetMarker = "personalization-local-reset";
        const int MaximumBytes = 4096;
        static readonly object Gate = new object();

        public static bool ValidConfiguration(string endpoint, string admissionKey)
        {
            Uri uri;
            return endpoint != null && endpoint.Length <= 2048
                && Uri.TryCreate(endpoint, UriKind.Absolute, out uri)
                && uri.Scheme == "https" && !string.IsNullOrEmpty(uri.Host)
                && uri.Query.Length == 0 && uri.Fragment.Length == 0
                && uri.UserInfo.Length == 0 && uri.AbsolutePath == "/"
                && IsLowerHex64(admissionKey);
        }

        public static bool IsLowerHex64(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char character in value)
                if (!(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f')) return false;
            return true;
        }

        public static AutomaticEnrollmentResult EnsureCreated(string directory, string endpoint, string admissionKey,
            Func<AutomaticEnrollmentRecord, string> serialize, Func<string, AutomaticEnrollmentRecord> deserialize,
            bool creationBlocked = false)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("An app-private directory is required.");
            lock (Gate)
            {
                string reset = Path.Combine(directory, ResetMarker);
                if (creationBlocked || File.Exists(reset)) return AutomaticEnrollmentResult.Blocked;
                string destination = Path.Combine(directory, FileName);
                // Existing manual registrations and their credentials always win,
                // even if a subsequent APK has a different registration key.
                if (File.Exists(destination)) return AutomaticEnrollmentResult.Existing;
                if (!ValidConfiguration(endpoint, admissionKey) || serialize == null || deserialize == null)
                    throw new ArgumentException("Automatic enrollment configuration is invalid.");
                endpoint = endpoint.TrimEnd('/');
                Directory.CreateDirectory(directory);
                string pending = destination + ".new";
                bool recovering = File.Exists(pending);
                if (recovering)
                {
                    var info = new FileInfo(pending);
                    AutomaticEnrollmentRecord pendingRecord = null;
                    if (info.Length > 0 && info.Length <= MaximumBytes)
                    {
                        // An I/O read failure is not evidence of corruption:
                        // preserve the file and retry instead of replacing it.
                        string pendingJson = File.ReadAllText(pending, Encoding.UTF8);
                        try { pendingRecord = deserialize(pendingJson); }
                        catch (Exception) { /* An incomplete, never-committed JSON write is recoverable. */ }
                    }
                    if (pendingRecord == null || !ValidConfiguration(pendingRecord.endpoint, pendingRecord.automaticEnrollmentKey)
                        || !IsLowerHex64(pendingRecord.token))
                    {
                        // The caller has ruled out any bound queue/withdrawal.
                        // No client can have used this incomplete .new identity;
                        // only a successfully renamed final file is activated.
                        if (File.Exists(reset)) return AutomaticEnrollmentResult.Blocked;
                        File.Delete(pending);
                        if (File.Exists(pending)) throw new IOException("Incomplete pending enrollment could not be removed.");
                        recovering = false;
                    }
                    else ValidatePending(pendingRecord, endpoint, admissionKey);
                }
                if (!recovering)
                {
                    var random = new byte[32];
                    using (var generator = RandomNumberGenerator.Create()) generator.GetBytes(random);
                    var candidate = new AutomaticEnrollmentRecord
                    {
                        endpoint = endpoint,
                        token = BitConverter.ToString(random).Replace("-", "").ToLowerInvariant(),
                        automaticEnrollmentKey = admissionKey
                    };
                    Array.Clear(random, 0, random.Length);
                    string json = serialize(candidate);
                    if (string.IsNullOrEmpty(json)) throw new IOException("Enrollment could not be serialized.");
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    if (bytes.Length > MaximumBytes) throw new IOException("Enrollment configuration is too large.");
                    var roundTrip = ReadRecord(json, deserialize);
                    ValidatePending(roundTrip, endpoint, admissionKey);
                    if (roundTrip.token != candidate.token) throw new IOException("Enrollment serialization changed the identity.");
                    // CreateNew prevents truncating a token left by an interrupted
                    // attempt. Flush before the atomic, non-overwriting rename.
                    using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                }
                // Reset may have been committed while the durable write ran.
                if (File.Exists(reset)) return AutomaticEnrollmentResult.Blocked;
                try { File.Move(pending, destination); }
                catch (IOException)
                {
                    if (File.Exists(destination)) return AutomaticEnrollmentResult.Existing;
                    throw;
                }
                return recovering ? AutomaticEnrollmentResult.Recovered : AutomaticEnrollmentResult.Created;
            }
        }

        static AutomaticEnrollmentRecord ReadRecord(string json, Func<string, AutomaticEnrollmentRecord> deserialize)
        {
            try { return deserialize(json); }
            catch (Exception) { throw new IOException("Enrollment data is invalid; existing state was preserved."); }
        }

        static void ValidatePending(AutomaticEnrollmentRecord record, string endpoint, string admissionKey)
        {
            // A pending identity belongs to the original server/key. Never send
            // its future records to a replacement endpoint without review.
            if (record == null || record.endpoint != endpoint || record.automaticEnrollmentKey != admissionKey
                || !IsLowerHex64(record.token))
                throw new IOException("Pending enrollment does not match this build; existing state was preserved.");
        }
    }
}
