using System;
using System.IO;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    // Explicit developer capture: output levels and playback positions, never audio
    // samples, user text, health data or device identifiers. Disabled in release.
    [DefaultExecutionOrder(10000)]
    public sealed class MusicDeviceProbe : MonoBehaviour
    {
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
        const int Limit = 1200;
        [Serializable] sealed class Channel
        {
            public bool playing;
            public int samples;
            public float volume, peak;
        }
        [Serializable] sealed class Sample
        {
            public string phase;
            public double seconds;
            public int context, outputChannels;
            public bool playerPresent, muted, suspended;
            public float outputPeak;
            public Channel[] channels;
        }
        readonly float[] pcm = new float[1024];
        readonly Channel[] channels = { new Channel(), new Channel() };
        readonly Sample sample = new Sample();
        GardenMusicPlayer player;
        AudioSource[] sources;
        string path;
        int written;
        double nextSample;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartRequestedCapture()
        {
            try
            {
                using var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                if (!intent.Call<bool>("getBooleanExtra", "capstoneAudioProbe", false)) return;
                new GameObject("Music device probe").AddComponent<MusicDeviceProbe>();
            }
            catch (Exception error) { Debug.LogWarning("MUSIC_PROBE_START_FAILED " + error.GetType().Name); }
        }
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            path = Path.Combine(Application.persistentDataPath, "capstone-music-probe.jsonl");
            sample.channels = channels;
            try { File.WriteAllText(path, ""); }
            catch (Exception error) { StopCapture(error); }
        }
        void Update()
        {
            if (Time.realtimeSinceStartupAsDouble < nextSample) return;
            nextSample = Time.realtimeSinceStartupAsDouble + .5;
            Capture("sample");
        }
        void OnApplicationPause(bool paused) => Capture(paused ? "pause" : "resume");
        void OnApplicationFocus(bool focused) => Capture(focused ? "focus" : "blur");
        void OnApplicationQuit() => Capture("quit");
        void Capture(string phase)
        {
            if (!enabled || string.IsNullOrEmpty(path) || written >= Limit) return;
            try
            {
                if (player == null)
                {
                    player = FindFirstObjectByType<GardenMusicPlayer>();
                    sources = player != null ? player.GetComponents<AudioSource>() : null;
                }
                sample.phase = phase;
                sample.seconds = Time.realtimeSinceStartupAsDouble;
                sample.playerPresent = player != null;
                sample.context = player != null ? (int)player.CurrentContext : -1;
                sample.muted = GardenMusicPlayer.IsMuted;
                sample.suspended = player != null && player.IsSuspended;
                sample.outputChannels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
                sample.outputPeak = 0;
                for (int channel = 0; channel < sample.outputChannels; channel++)
                {
                    AudioListener.GetOutputData(pcm, channel);
                    sample.outputPeak = Mathf.Max(sample.outputPeak, Peak());
                }
                for (int i = 0; i < channels.Length; i++)
                {
                    var source = sources != null && i < sources.Length ? sources[i] : null;
                    var channel = channels[i];
                    channel.playing = source != null && source.isPlaying;
                    channel.samples = source != null ? source.timeSamples : 0;
                    channel.volume = source != null ? source.volume : 0;
                    channel.peak = 0;
                    if (source != null)
                    {
                        int count = source.clip != null ? Mathf.Min(2, source.clip.channels) : 1;
                        for (int audioChannel = 0; audioChannel < count; audioChannel++)
                        {
                            source.GetOutputData(pcm, audioChannel);
                            channel.peak = Mathf.Max(channel.peak, Peak());
                        }
                    }
                }
                // Transition samples can still contain the previous DSP buffer;
                // steady samples and position deltas establish pause/mute behavior.
                File.AppendAllText(path, JsonUtility.ToJson(sample) + "\n");
                written++;
                if (written >= Limit) enabled = false;
            }
            catch (Exception error) { StopCapture(error); }
        }
        float Peak()
        {
            float peak = 0;
            foreach (float value in pcm) peak = Mathf.Max(peak, Mathf.Abs(value));
            return peak;
        }
        void StopCapture(Exception error)
        {
            enabled = false;
            Debug.LogWarning("MUSIC_PROBE_STOPPED " + error.GetType().Name);
        }
#endif
    }
}
