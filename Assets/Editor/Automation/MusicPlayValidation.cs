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
        [Serializable] sealed class Report {public string status;public List<string> checks=new List<string>();public float outputPeak;public string error;}
        [Serializable] sealed class PrefBackup {public bool hadMute;public int muted;}
        static Report report;
        static GardenMusicPlayer player;
        static GardenMusicProfile original,temporary;
        static AudioClip alternate,other;
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
            SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+90);
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
        static IEnumerator Steps()
        {
            Check(AndroidPlatformBridge.InputMode==PlatformInputMode.Mock,"isolated MOCK scene");
            var nav=UnityEngine.Object.FindFirstObjectByType<MockupNavigation>();
            player=GardenMusicPlayer.Ensure();original=Resources.Load<GardenMusicProfile>("Audio/GardenMusicProfile");
            Check(original!=null && original.homeGarden!=null,"music profile and supplied audio import");
            Check(original.homeGarden.length>147 && original.homeGarden.length<148,"complete supplied track duration");
            var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(original.homeGarden));
            Check(importer.defaultSampleSettings.loadType==AudioClipLoadType.Streaming && importer.loadInBackground && !importer.forceToMono,"stereo streaming background import");
            GardenMusicPlayer.SetMuted(false);Lifecycle("OnApplicationFocus",true);Lifecycle("OnApplicationPause",false);
            nav.ShowIsland();
            yield return new WaitForSecondsRealtime(2);
            Check(player.CurrentClip==original.homeGarden && Sources().Any(s=>s.isPlaying),"supplied track is playing");
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
            Lifecycle("OnApplicationFocus",false);before=Samples();yield return new WaitForSecondsRealtime(.3f);
            Check(player.IsSuspended && Math.Abs(Samples()-before)<256,"focus loss pauses sample position");
            Lifecycle("OnApplicationPause",true);Lifecycle("OnApplicationFocus",true);yield return new WaitForSecondsRealtime(.2f);
            Check(player.IsSuspended && Math.Abs(Samples()-before)<256,"focus restoration alone cannot resume a paused app");
            Lifecycle("OnApplicationPause",false);yield return new WaitForSecondsRealtime(.3f);
            Check(!player.IsSuspended && Samples()>before+1000,"lifecycle restoration resumes rather than restarting");
            GardenMusicPlayer.SetMuted(true);
            Check(GardenMusicPlayer.IsMuted && player.IsSuspended,"mute immediately sets persisted and suspended state");
            int muteRequestedAt=Samples();
            AudioSettings.GetDSPBufferSize(out int bufferLength,out int bufferCount);
            yield return new WaitForSecondsRealtime(bufferLength/(float)AudioSettings.outputSampleRate+.04f);
            before=Samples();yield return new WaitForSecondsRealtime(.3f);
            Debug.Log("Music mute diagnostic requested="+muteRequestedAt+" settled="+before+" after="+Samples()+
                " buffer="+bufferLength+" buffers="+bufferCount+" rate="+AudioSettings.outputSampleRate+
                " muted="+GardenMusicPlayer.IsMuted+" suspended="+player.IsSuspended);
            Check(Math.Abs(Samples()-before)<256,"muted sample position remains stable after one DSP buffer");
            GardenMusicPlayer.SetMuted(false);yield return new WaitForSecondsRealtime(.3f);
            Check(!GardenMusicPlayer.IsMuted && Samples()>before+1000,"unmute resumes preserved position");
            typeof(GardenMusicPlayer).GetField("pauseForFocusLoss",Hidden).SetValue(player,false);
            Lifecycle("OnApplicationFocus",false);before=Samples();yield return new WaitForSecondsRealtime(.3f);
            Check(!player.IsSuspended && Samples()>before+1000,"Android policy keeps music during keyboard-only focus loss");
            Lifecycle("OnApplicationPause",true);before=Samples();yield return new WaitForSecondsRealtime(.3f);
            Check(player.IsSuspended && Math.Abs(Samples()-before)<256,"Android policy pauses background even with keyboard focus absent");
            Lifecycle("OnApplicationPause",false);yield return new WaitForSecondsRealtime(.3f);
            Check(!player.IsSuspended && Samples()>before+1000,"Android policy resumes after returning from keyboard-background sequence");
            Lifecycle("OnApplicationFocus",true);
            typeof(GardenMusicPlayer).GetField("pauseForFocusLoss",Hidden).SetValue(player,true);
            temporary=UnityEngine.Object.Instantiate(original);alternate=Tone("Synthetic A",440);other=Tone("Synthetic B",660);
            temporary.activity=alternate;temporary.settings=other;
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
