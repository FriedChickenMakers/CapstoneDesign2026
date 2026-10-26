using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    [InitializeOnLoad]
    public static class MusicPlayValidation
    {
        const string Key="Capstone.MusicValidation", MutedKey="capstone.music.muted";
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] sealed class Report {public string status;public List<string> checks=new List<string>();public float outputPeak,nightOutputPeak;public string error;}
        [Serializable] sealed class PrefBackup {public bool hadMute;public int muted;}
        static Report report;
        static GardenMusicPlayer player;
        static GardenMusicProfile original,temporary;
        static AudioClip alternate,other;
        static double? originalClock;
        static bool clockCaptured;
        static MusicPlayValidation(){if(SessionState.GetBool(Key,false))EditorApplication.update+=WaitForPlay;}
        public static void Start()
        {
            if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT")))
                throw new Exception("An isolated CAPSTONE_SMOKE_SAVE_ROOT is required.");
            string root=Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT");Directory.CreateDirectory(root);
            string backup=Path.Combine(root,"music-pref-original.json");
            var saved=File.Exists(backup)?JsonUtility.FromJson<PrefBackup>(File.ReadAllText(backup)):
                new PrefBackup{hadMute=SessionState.GetBool(Key,false)?SessionState.GetBool(Key+".HadMute",false):PlayerPrefs.HasKey(MutedKey),
                    muted=SessionState.GetBool(Key,false)?SessionState.GetInt(Key+".Mute",0):PlayerPrefs.GetInt(MutedKey)};
            if(!File.Exists(backup))File.WriteAllText(backup,JsonUtility.ToJson(saved));
            SessionState.SetBool(Key+".HadMute",saved.hadMute);SessionState.SetInt(Key+".Mute",saved.muted);
            SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+240);
            EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
            EditorApplication.update-=WaitForPlay;EditorApplication.update+=WaitForPlay;EditorApplication.EnterPlaymode();
        }
        static void WaitForPlay()
        {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",0))
            {Finish(new Exception("Music Play startup timeout"));return;}
            if(!EditorApplication.isPlaying)return;
            var nav=UnityEngine.Object.FindFirstObjectByType<MockupNavigation>();
            if(nav==null)return;
            Application.runInBackground=true;
            EditorApplication.update-=WaitForPlay;EditorApplication.update+=WatchRun;
            Debug.Log("Music validation Play entered; starting audio checks");nav.StartCoroutine(Guarded());
        }
        static void WatchRun(){if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",0))Finish(new Exception("Music Play runtime timeout"));}
        static IEnumerator Guarded()
        {
            report=new Report();var steps=Steps();
            while(true)
            {
                object next=null;bool moved=false;Exception failure=null;
                try{moved=steps.MoveNext();if(moved)next=steps.Current;}catch(Exception ex){failure=ex;}
                if(failure!=null){Finish(failure);yield break;}
                if(!moved){Finish(null);yield break;}
                yield return next;
            }
        }
        static void Check(bool ok,string name){if(!ok)throw new Exception(name);report.checks.Add(name);Debug.Log("Music validation PASS: "+name);}
        static int Samples()=>Sources()[(int)typeof(GardenMusicPlayer).GetField("target",Hidden).GetValue(player)].timeSamples;
        static AudioSource[] Sources()=>(AudioSource[])typeof(GardenMusicPlayer).GetField("channels",Hidden).GetValue(player);
        static void Lifecycle(string method,bool value)=>typeof(GardenMusicPlayer).GetMethod(method,Hidden).Invoke(player,new object[]{value});
        static WaitForSecondsRealtime AudioQueueDelay()
        {
            AudioSettings.GetDSPBufferSize(out int length,out int count);
            // Pause changes the source state immediately, but timeSamples is read
            // across the mixer thread. Let the configured output queue drain before
            // taking the stability baseline, rather than widening its tolerance.
            return new WaitForSecondsRealtime(length*Mathf.Max(1,count)/(float)Mathf.Max(1,AudioSettings.outputSampleRate)+.04f);
        }
        static int[] SamplePositions()=>Sources().Select(source=>source.timeSamples).ToArray();
        static void CheckPausePosition(int[] requested,AudioClip[] clips,string label)
        {
            AudioSettings.GetDSPBufferSize(out int length,out int count);
            var sources=Sources();var settled=SamplePositions();
            double bufferedSeconds=length*(Math.Max(1,count)+1d)/Math.Max(1,AudioSettings.outputSampleRate);
            bool preserved=sources.Select((source,index)=>source.clip==clips[index] &&
                Math.Abs(settled[index]-requested[index])<=Math.Ceiling(bufferedSeconds*(clips[index]!=null?clips[index].frequency:1))+256).All(value=>value);
            string diagnostic="requested="+string.Join(",",requested)+" settled="+string.Join(",",settled)+
                " buffer="+length+" buffers="+count+" rate="+AudioSettings.outputSampleRate;
            Debug.Log("Music pause transition "+label+": "+diagnostic);
            if(!preserved)throw new Exception(label+"; "+diagnostic);
            Check(true,label);
        }
        static void CheckPausedSources(int[] before,AudioClip[] clips,string label)
        {
            var sources=Sources();var after=SamplePositions();
            AudioSettings.GetDSPBufferSize(out int length,out int count);
            string diagnostic="before="+string.Join(",",before)+" after="+string.Join(",",after)+
                " playing="+string.Join(",",sources.Select(source=>source.isPlaying))+
                " suspended="+player.IsSuspended+" buffer="+length+" buffers="+count+" rate="+AudioSettings.outputSampleRate;
            Debug.Log("Music pause diagnostic "+label+": "+diagnostic);
            bool stable=player.IsSuspended && sources.Select((source,index)=>
                !source.isPlaying && source.clip==clips[index] && Math.Abs(after[index]-before[index])<256).All(value=>value);
            if(!stable)throw new Exception(label+"; "+diagnostic);
            Check(true,label);
        }
        static void ClockWithoutEvent(int minute)
        {
            // Exercise ordinary per-frame clock polling without changing the operating-system clock.
            typeof(GardenVisualClock).GetField("<OverrideHour>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic)
                .SetValue(null,(double?)(minute/60d));
        }
        static AudioSource SourceFor(AudioClip clip)=>Sources().Single(s=>s.clip==clip);
        static bool OnlyPlaying(AudioClip clip)=>Sources().Count(s=>s.isPlaying)==1 && SourceFor(clip).isPlaying;
        static void CheckCrossfade(AudioClip outgoing,AudioClip incoming,string label)
        {
            float level=original.volume;
            Check(player.CurrentClip==incoming && Sources().Count(s=>s.isPlaying)==2,label+" selects the new track with two playing channels");
            Check(SourceFor(outgoing).volume>0 && SourceFor(outgoing).volume<level &&
                SourceFor(incoming).volume>0 && SourceFor(incoming).volume<level &&
                Mathf.Abs(Sources().Sum(s=>s.volume*s.volume)-level*level)<.0005f,label+" fades both tracks continuously while preserving total channel power");
        }
        static IEnumerator Steps()
        {
            Check(AndroidPlatformBridge.InputMode==PlatformInputMode.Mock,"isolated MOCK scene");
            var nav=UnityEngine.Object.FindFirstObjectByType<MockupNavigation>();
            player=GardenMusicPlayer.Ensure();original=Resources.Load<GardenMusicProfile>("Audio/GardenMusicProfile");
            originalClock=GardenVisualClock.OverrideHour;clockCaptured=true;
            Check(original!=null && original.homeGarden!=null && original.night!=null,"music profile and both supplied audio imports");
            Check(original.homeGarden.length>147 && original.homeGarden.length<148,"complete supplied track duration");
            var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(original.homeGarden));
            Check(importer.defaultSampleSettings.loadType==AudioClipLoadType.Streaming && importer.loadInBackground && !importer.forceToMono,"stereo streaming background import");
            var nightImporter=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(original.night));
            Check(nightImporter.defaultSampleSettings.loadType==AudioClipLoadType.Streaming && nightImporter.loadInBackground && !nightImporter.forceToMono,
                "night track uses stereo streaming background import");
            Check(original.night.length>154.2f && original.night.length<154.4f && original.night!=original.homeGarden,
                "complete supplied night track duration");
            Check(Mathf.Approximately(original.crossfadeSeconds,8),"profile uses an eight-second day/night crossfade");
            foreach(GardenMusicContext context in Enum.GetValues(typeof(GardenMusicContext)))
                Check(original.TrackFor(context,0)==original.night && original.TrackFor(context,5+59/60d)==original.night &&
                    original.TrackFor(context,6)==original.homeGarden && original.TrackFor(context,17+59/60d)==original.homeGarden &&
                    original.TrackFor(context,18)==original.night && original.TrackFor(context,23+59/60d)==original.night,
                    context+" follows exact 06:00 and 18:00 boundaries");
            GardenVisualClock.SetOverrideMinutes(0);
            UnityEngine.Object.DestroyImmediate(player.gameObject);player=GardenMusicPlayer.Ensure();
            GardenMusicPlayer.SetMuted(false);Lifecycle("OnApplicationFocus",true);Lifecycle("OnApplicationPause",false);
            nav.ShowIsland();
            yield return new WaitForSecondsRealtime(8.4f);
            Check(player.CurrentClip==original.night && OnlyPlaying(original.night),"launching at midnight starts the supplied night track");
            var nightPcm=new float[1024];float nightPeak=0;
            for(int frame=0;frame<20;frame++)
            {
                SourceFor(original.night).GetOutputData(nightPcm,0);
                foreach(float sample in nightPcm)nightPeak=Mathf.Max(nightPeak,Mathf.Abs(sample));
                yield return null;
            }
            report.nightOutputPeak=nightPeak;
            Check(nightPeak>0.00001f,"supplied night track produces nonzero PCM after its quiet introduction");
            GardenVisualClock.SetOverrideMinutes(12*60);
            yield return new WaitForSecondsRealtime(8.4f);
            Check(player.CurrentClip==original.homeGarden && OnlyPlaying(original.homeGarden),"daytime preview selects the existing daytime track");
            int before=Samples();yield return new WaitForSecondsRealtime(.5f);
            Check(Samples()>before+1000,"supplied streaming track timeSamples advance");
            var pcm=new float[1024];float peak=0;
            for(int frame=0;frame<20;frame++)
            {
                foreach(var source in Sources()){source.GetOutputData(pcm,0);foreach(float sample in pcm)peak=Mathf.Max(peak,Mathf.Abs(sample));}
                yield return null;
            }
            report.outputPeak=peak;Check(peak>0.00001f,"audio channel produces nonzero PCM output");
            before=Samples();nav.ShowActivities();yield return new WaitForSecondsRealtime(.2f);
            Check(player.CurrentContext==GardenMusicContext.Activity && Samples()>=before,"same-track activity navigation preserves position");
            before=Samples();nav.ShowSettings();yield return new WaitForSecondsRealtime(.2f);
            Check(player.CurrentContext==GardenMusicContext.Settings && Samples()>=before,"same-track settings navigation preserves position");
            Check(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l=>l.isActiveAndEnabled)==1,"one active listener after all tab cameras change");
            var pausedClips=Sources().Select(source=>source.clip).ToArray();var requestedSamples=SamplePositions();
            Lifecycle("OnApplicationFocus",false);
            Check(player.IsSuspended && Sources().All(source=>!source.isPlaying),"focus loss immediately pauses both source states");
            yield return AudioQueueDelay();
            CheckPausePosition(requestedSamples,pausedClips,"focus loss preserves the buffered playback position");
            var pausedSamples=SamplePositions();
            yield return new WaitForSecondsRealtime(.3f);
            CheckPausedSources(pausedSamples,pausedClips,"focus loss pauses sample position");
            before=Samples();
            Lifecycle("OnApplicationPause",true);Lifecycle("OnApplicationFocus",true);yield return new WaitForSecondsRealtime(.2f);
            CheckPausedSources(pausedSamples,pausedClips,"focus restoration alone cannot resume a paused app");
            Lifecycle("OnApplicationPause",false);yield return new WaitForSecondsRealtime(.3f);
            Check(!player.IsSuspended && Samples()>before+1000,"lifecycle restoration resumes rather than restarting");
            requestedSamples=SamplePositions();pausedClips=Sources().Select(source=>source.clip).ToArray();
            GardenMusicPlayer.SetMuted(true);
            Check(GardenMusicPlayer.IsMuted && player.IsSuspended && Sources().All(source=>!source.isPlaying),
                "mute immediately sets persisted and suspended source states");
            yield return AudioQueueDelay();
            CheckPausePosition(requestedSamples,pausedClips,"mute preserves the buffered playback position");
            pausedSamples=SamplePositions();
            before=Samples();yield return new WaitForSecondsRealtime(.3f);
            CheckPausedSources(pausedSamples,pausedClips,"muted sample position remains stable after the DSP queue drains");
            GardenMusicPlayer.SetMuted(false);yield return new WaitForSecondsRealtime(.3f);
            Check(!GardenMusicPlayer.IsMuted && Samples()>before+1000,"unmute resumes preserved position");
            typeof(GardenMusicPlayer).GetField("pauseForFocusLoss",Hidden).SetValue(player,false);
            Lifecycle("OnApplicationFocus",false);before=Samples();yield return new WaitForSecondsRealtime(.3f);
            Check(!player.IsSuspended && Samples()>before+1000,"Android policy keeps music during keyboard-only focus loss");
            requestedSamples=SamplePositions();pausedClips=Sources().Select(source=>source.clip).ToArray();
            Lifecycle("OnApplicationPause",true);
            Check(player.IsSuspended && Sources().All(source=>!source.isPlaying),"Android background immediately pauses both source states despite keyboard focus loss");
            yield return AudioQueueDelay();
            CheckPausePosition(requestedSamples,pausedClips,"Android background preserves the buffered playback position");
            pausedSamples=SamplePositions();
            before=Samples();yield return new WaitForSecondsRealtime(.3f);
            CheckPausedSources(pausedSamples,pausedClips,"Android policy pauses background even with keyboard focus absent");
            Lifecycle("OnApplicationPause",false);yield return new WaitForSecondsRealtime(.3f);
            Check(!player.IsSuspended && Samples()>before+1000,"Android policy resumes after returning from keyboard-background sequence");
            Lifecycle("OnApplicationFocus",true);
            typeof(GardenMusicPlayer).GetField("pauseForFocusLoss",Hidden).SetValue(player,true);

            nav.ShowIsland();GardenVisualClock.SetOverrideMinutes(17*60+59);
            yield return new WaitForSecondsRealtime(.2f);
            before=Samples();ClockWithoutEvent(18*60);
            yield return new WaitForSecondsRealtime(.1f);
            Check(player.CurrentClip==original.night && SourceFor(original.homeGarden).timeSamples>=before &&
                SourceFor(original.homeGarden).volume>original.volume*.9f && SourceFor(original.night).volume<original.volume*.1f,
                "17:59 to 18:00 polling starts the fade without cutting the outgoing track");
            yield return new WaitForSecondsRealtime(3.9f);
            CheckCrossfade(original.homeGarden,original.night,"sunset");
            yield return new WaitForSecondsRealtime(4.4f);
            Check(OnlyPlaying(original.night) && Sources().All(s=>s.clip==original.night || s.clip==null),
                "sunset completion stops and releases the daytime track");
            before=Samples();GardenVisualClock.SetOverrideMinutes(0);nav.ShowSettings();
            yield return new WaitForSecondsRealtime(.2f);
            Check(player.CurrentClip==original.night && Samples()>before+1000,"midnight and same-night navigation preserve track position");
            before=Samples();GardenVisualClock.SetOverrideMinutes(5*60+59);
            yield return new WaitForSecondsRealtime(.2f);
            Check(Samples()>before+1000,"05:59 remains the same uninterrupted night track");
            before=Samples();ClockWithoutEvent(6*60);
            yield return new WaitForSecondsRealtime(.1f);
            Check(player.CurrentClip==original.homeGarden && SourceFor(original.night).timeSamples>=before &&
                SourceFor(original.night).volume>original.volume*.9f && SourceFor(original.homeGarden).volume<original.volume*.1f,
                "05:59 to 06:00 polling starts the reverse fade without cutting the night track");
            yield return new WaitForSecondsRealtime(3.9f);
            CheckCrossfade(original.night,original.homeGarden,"sunrise");
            yield return new WaitForSecondsRealtime(4.4f);
            Check(OnlyPlaying(original.homeGarden) && Sources().All(s=>s.clip==original.homeGarden || s.clip==null),
                "sunrise completion stops and releases the night track");

            GardenVisualClock.SetOverrideMinutes(17*60+59);GardenMusicPlayer.SetMuted(true);
            ClockWithoutEvent(18*60);yield return new WaitForSecondsRealtime(.2f);
            Check(player.IsSuspended && player.CurrentClip==original.night && Sources().All(s=>!s.isPlaying),
                "muted sunset updates the requested track without audible playback");
            before=Samples();yield return new WaitForSecondsRealtime(.3f);
            Check(Math.Abs(Samples()-before)<256,"muted night track remains paused");
            GardenMusicPlayer.SetMuted(false);yield return new WaitForSecondsRealtime(8.4f);
            Check(!player.IsSuspended && OnlyPlaying(original.night),"unmuting after sunset fades to the correct night track");
            GardenVisualClock.SetOverrideMinutes(5*60+59);Lifecycle("OnApplicationPause",true);
            ClockWithoutEvent(6*60);yield return new WaitForSecondsRealtime(.2f);
            Check(player.IsSuspended && player.CurrentClip==original.homeGarden && Sources().All(s=>!s.isPlaying),
                "background sunrise updates the requested track while keeping both sources paused");
            Lifecycle("OnApplicationPause",false);yield return new WaitForSecondsRealtime(8.4f);
            Check(!player.IsSuspended && OnlyPlaying(original.homeGarden),"returning after sunrise fades to the correct daytime track");

            GardenVisualClock.SetOverrideMinutes(18*60);yield return new WaitForSecondsRealtime(.5f);
            before=Samples();GardenVisualClock.SetOverrideMinutes(6*60);yield return new WaitForSecondsRealtime(.1f);
            float[] gainsBefore=Sources().Select(s=>s.volume).ToArray();
            GardenVisualClock.SetOverrideMinutes(18*60);
            Check(player.CurrentClip==original.night && Samples()>=before &&
                Sources().Select((source,i)=>Mathf.Approximately(source.volume,gainsBefore[i])).All(s=>s),
                "rapid night-day-night reversal reuses playback position without an instantaneous volume jump");
            yield return new WaitForSecondsRealtime(8.4f);
            Check(OnlyPlaying(original.night) && player.GetComponents<AudioSource>().Length==2,
                "rapid time previews settle on the requested track using only two channels");
            GardenVisualClock.SetOverrideMinutes(12*60);yield return new WaitForSecondsRealtime(8.4f);

            temporary=UnityEngine.Object.Instantiate(original);alternate=Tone("Synthetic A",440);other=Tone("Synthetic B",660);
            temporary.activity=alternate;temporary.settings=other;temporary.crossfadeSeconds=.8f;
            temporary.night=null;
            Check(temporary.TrackFor(GardenMusicContext.HomeGarden,0)==original.homeGarden &&
                temporary.TrackFor(GardenMusicContext.Activity,0)==alternate && temporary.TrackFor(GardenMusicContext.Settings,18)==other,
                "a profile without a night clip retains all existing context fallbacks");
            typeof(GardenMusicPlayer).GetField("profile",Hidden).SetValue(player,temporary);
            nav.ShowActivities();yield return new WaitForSecondsRealtime(.1f);
            Check(Sources().Count(s=>s.isPlaying)==2 && player.CurrentClip==alternate,"different track crossfade uses two channels");
            nav.ShowSettings();yield return new WaitForSecondsRealtime(.05f);nav.ShowIsland();yield return new WaitForSecondsRealtime(1);
            Check(player.CurrentClip==original.homeGarden && Sources().Count(s=>s.isPlaying)==1,"rapid track changes settle to one requested channel");
            nav.ShowActivities();yield return new WaitForSecondsRealtime(.6f);before=Samples();
            nav.ShowIsland();yield return new WaitForSecondsRealtime(.1f);nav.ShowActivities();
            Check(player.CurrentClip==alternate && Samples()>=before,"reversing crossfade reuses existing clip position");
            Check(player.GetComponents<AudioSource>().Length==2,"rapid changes never allocate extra audio channels");
        }
        static AudioClip Tone(string name,float frequency)
        {
            const int rate=48000;var data=new float[rate*4];
            for(int i=0;i<data.Length;i++)data[i]=.08f*Mathf.Sin(2*Mathf.PI*frequency*i/rate);
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        static void Finish(Exception failure)
        {
            EditorApplication.update-=WaitForPlay;EditorApplication.update-=WatchRun;SessionState.SetBool(Key,false);
            if(player!=null && original!=null){typeof(GardenMusicPlayer).GetField("profile",Hidden).SetValue(player,original);player.SetContext(GardenMusicContext.HomeGarden);}
            if(clockCaptured)
            {
                if(originalClock.HasValue)GardenVisualClock.SetOverrideMinutes((int)Math.Round(originalClock.Value*60));
                else GardenVisualClock.UseSystemTime();
            }
            if(SessionState.GetBool(Key+".HadMute",false))PlayerPrefs.SetInt(MutedKey,SessionState.GetInt(Key+".Mute",0));else PlayerPrefs.DeleteKey(MutedKey);
            PlayerPrefs.Save();
            if(report==null)report=new Report();report.status=failure==null?"PASS":"FAIL";report.error=failure?.ToString();
            string root=Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT");Directory.CreateDirectory(root);
            File.Delete(Path.Combine(root,"music-pref-original.json"));
            File.WriteAllText(Path.Combine(root,"music-validation.json"),JsonUtility.ToJson(report,true));
            if(failure!=null)Debug.LogException(failure);else Debug.Log("Music Play validation: "+report.checks.Count+" PASS, PCM peak "+report.outputPeak);
            EditorApplication.Exit(failure==null?0:1);
        }
    }
}
