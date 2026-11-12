using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Run only through a disposable copied project. Neither a real save root
    // nor the real company's PlayerPrefs is an acceptable validation fixture.
    [InitializeOnLoad]
    public static class SfxPlayValidation
    {
        const string Key="Capstone.SfxValidation", VolumeKey="capstone.sfx.volume";
        const string MusicVolumeKey="capstone.music.volume", MusicMutedKey="capstone.music.muted";
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] sealed class Report
        {public string status;public List<string> checks=new List<string>();public float outputPeak;public string error;}
        [Serializable] sealed class PrefBackup
        {public bool hadSfx,hadMusicVolume,hadMusicMuted;public float sfx=1,musicVolume=1;public int musicMuted;}
        static Report report;
        static GardenSfxPlayer player;
        static GardenMusicPlayer music;
        static GameObject testButton;
        static GameObject testSlider;
        static AudioClip poolClip;
        static bool originalReducedMotion,reducedMotionCaptured;
        static double? originalClock;
        static bool clockCaptured;
        static AudioSource[] musicChannels;
        static AudioClip[] musicClips;
        static float[] musicGains,musicLevels;
        static GardenMusicContext musicContext;
        static AudioClip musicClip;
        static float musicVolume,listenerVolume;
        static bool hadMusicVolume,hadMusicMuted;
        static int musicMuted;
        static int[] listeners;
        static bool[] listenerStates;

        static SfxPlayValidation(){if(SessionState.GetBool(Key,false))EditorApplication.update+=WaitForPlay;}
        static bool IsIsolatedProject()=>Application.companyName=="CapstoneSyntheticValidation" &&
            Path.GetFullPath(Directory.GetCurrentDirectory()).IndexOf(Path.DirectorySeparatorChar+"artifacts"+Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)>=0 && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT"));
        static string Root=>Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT");
        static string BackupPath=>Path.Combine(Root,"sfx-pref-original.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ConfigureSyntheticInput()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(!IsIsolatedProject())throw new Exception("SFX Play bootstrap requires the isolated synthetic project.");
            AndroidPlatformBridge.UseMockProvider();
        }
        public static void Start()
        {
            if(!IsIsolatedProject())throw new Exception("A separately copied CapstoneSyntheticValidation project and isolated CAPSTONE_SMOKE_SAVE_ROOT are required.");
            Directory.CreateDirectory(Root);
            if(!File.Exists(BackupPath))File.WriteAllText(BackupPath,JsonUtility.ToJson(new PrefBackup
            {
                hadSfx=PlayerPrefs.HasKey(VolumeKey),sfx=PlayerPrefs.GetFloat(VolumeKey,1),
                hadMusicVolume=PlayerPrefs.HasKey(MusicVolumeKey),musicVolume=PlayerPrefs.GetFloat(MusicVolumeKey,1),
                hadMusicMuted=PlayerPrefs.HasKey(MusicMutedKey),musicMuted=PlayerPrefs.GetInt(MusicMutedKey,0)
            }));
            PlayerPrefs.DeleteKey(VolumeKey);PlayerPrefs.Save();
            int before=Players().Length;
            float initial=GardenSfxPlayer.Volume;GardenSfxPlayer.SetVolume(.25f);GardenSfxPlayer.SaveVolume();
            GardenSfxPlayer.Play(GardenSfxCue.UiClick);GardenSfxPlayer.RestoreInitialSettings();
            SessionState.SetBool(Key+".EditorReadOnly",initial==1 && !PlayerPrefs.HasKey(VolumeKey) && Players().Length==before);
            SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+240);
            EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
            EditorApplication.update-=WaitForPlay;EditorApplication.update+=WaitForPlay;EditorApplication.EnterPlaymode();
        }
        static void WaitForPlay()
        {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",0)){Finish(new Exception("SFX Play startup timeout"));return;}
            if(!EditorApplication.isPlaying)return;
            var nav=UnityEngine.Object.FindFirstObjectByType<MockupNavigation>();if(nav==null)return;
            Application.runInBackground=true;
            EditorApplication.update-=WaitForPlay;EditorApplication.update+=WatchRun;nav.StartCoroutine(Guarded());
        }
        static void WatchRun(){if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",0))Finish(new Exception("SFX Play runtime timeout"));}
        static IEnumerator Guarded()
        {
            report=new Report();var steps=Steps();
            while(true)
            {
                object next=null;bool moved=false;Exception failure=null;
                try{moved=steps.MoveNext();if(moved)next=steps.Current;}catch(Exception ex){failure=ex;}
                if(failure!=null){Finish(failure);yield break;}
                if(!moved){Finish(null);yield break;}yield return next;
            }
        }
        static void Check(bool ok,string label)
        {if(!ok)throw new Exception(label);report.checks.Add(label);Debug.Log("SFX validation PASS: "+label);}
        static GardenSfxPlayer[] Players()=>UnityEngine.Object.FindObjectsByType<GardenSfxPlayer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        static AudioSource[] Sources()=>(AudioSource[])typeof(GardenSfxPlayer).GetField("channels",Hidden).GetValue(player);
        static float[] Gains()=>(float[])typeof(GardenSfxPlayer).GetField("gains",Hidden).GetValue(player);
        static GardenSfxCue[] Cues()=>(GardenSfxCue[])typeof(GardenSfxPlayer).GetField("channelCues",Hidden).GetValue(player);
        static bool Pending()=>(bool)typeof(GardenSfxPlayer).GetField("preferencesPending",Hidden).GetValue(player);
        static int Active()=>Sources().Count(source=>source.isPlaying);
        static int RuntimeListenerCount(Button button)
        {
            var calls=typeof(UnityEngine.Events.UnityEventBase).GetField("m_Calls",Hidden).GetValue(button.onClick);
            return ((ICollection)calls.GetType().GetField("m_RuntimeCalls",Hidden).GetValue(calls)).Count;
        }
        static bool Playing(GardenSfxCue cue)=>Sources().Select((source,index)=>source.isPlaying && Cues()[index]==cue).Any(value=>value);
        static void Lifecycle(string method,bool value)=>typeof(GardenSfxPlayer).GetMethod(method,Hidden).Invoke(player,new object[]{value});
        static void FocusPolicy(bool desktop)=>typeof(GardenSfxPlayer).GetField("pauseForFocusLoss",Hidden).SetValue(player,desktop);
        static void AwakePlayer()
        {
            // Explicit user adjustment may initialize the runtime service. Tests
            // do not depend on OS focus of the disposable editor window.
            GardenSfxPlayer.SetVolume(GardenSfxPlayer.Volume==.99f?1:.99f);
            player=Players().Single();Lifecycle("OnApplicationFocus",true);Lifecycle("OnApplicationPause",false);
        }
        static void DestroyPlayer()
        {
            foreach(var item in Players())UnityEngine.Object.DestroyImmediate(item.gameObject);
            player=null;
        }
        static void CheckLevels(float requested,string label)
        {
            Check(Mathf.Approximately(GardenSfxPlayer.Volume,requested) && Sources().Select((source,index)=>
                Mathf.Abs(source.volume-Gains()[index]*requested)<.00001f).All(value=>value),label);
        }
        static void CaptureMusic()
        {
            musicChannels=(AudioSource[])typeof(GardenMusicPlayer).GetField("channels",Hidden).GetValue(music);
            musicClips=musicChannels.Select(source=>source.clip).ToArray();
            musicGains=((float[])typeof(GardenMusicPlayer).GetField("gains",Hidden).GetValue(music)).ToArray();
            musicLevels=musicChannels.Select(source=>source.volume).ToArray();musicContext=music.CurrentContext;musicClip=music.CurrentClip;
            hadMusicVolume=PlayerPrefs.HasKey(MusicVolumeKey);musicVolume=PlayerPrefs.GetFloat(MusicVolumeKey,1);
            hadMusicMuted=PlayerPrefs.HasKey(MusicMutedKey);musicMuted=PlayerPrefs.GetInt(MusicMutedKey,0);
            var activeListeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include,FindObjectsSortMode.InstanceID);
            listeners=activeListeners.Select(item=>item.GetInstanceID()).ToArray();listenerStates=activeListeners.Select(item=>item.enabled).ToArray();
            listenerVolume=AudioListener.volume;
        }
        static void CheckMusic(string label)
        {
            var activeListeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include,FindObjectsSortMode.InstanceID);
            Check(music.CurrentClip==musicClip && music.CurrentContext==musicContext &&
                musicChannels.Select(source=>source.clip).SequenceEqual(musicClips) &&
                ((float[])typeof(GardenMusicPlayer).GetField("gains",Hidden).GetValue(music)).SequenceEqual(musicGains) &&
                musicChannels.Select(source=>source.volume).SequenceEqual(musicLevels) &&
                PlayerPrefs.HasKey(MusicVolumeKey)==hadMusicVolume && PlayerPrefs.GetFloat(MusicVolumeKey,1)==musicVolume &&
                PlayerPrefs.HasKey(MusicMutedKey)==hadMusicMuted && PlayerPrefs.GetInt(MusicMutedKey,0)==musicMuted &&
                activeListeners.Select(item=>item.GetInstanceID()).SequenceEqual(listeners) &&
                activeListeners.Select(item=>item.enabled).SequenceEqual(listenerStates) && AudioListener.volume==listenerVolume,label);
        }
        static IEnumerator Steps()
        {
            Check(AndroidPlatformBridge.InputMode==PlatformInputMode.Mock,"isolated MOCK scene");
            Check(SessionState.GetBool(Key+".EditorReadOnly",false),"editor reads, renders, and rejected playback do not create a player or preferences");
            DestroyPlayer();PlayerPrefs.DeleteKey(VolumeKey);PlayerPrefs.Save();
            Check(GardenSfxPlayer.Volume==1 && Players().Length==0 && !PlayerPrefs.HasKey(VolumeKey),"runtime default-volume getter is side-effect free");
            GardenSfxPlayer.RestoreInitialSettings();GardenSfxPlayer.SaveVolume();
            Check(Players().Length==0 && !PlayerPrefs.HasKey(VolumeKey),"reset without an SFX player remains side-effect free");

            foreach(string name in new[]{"UiClick","UiBack","PracticeStart","GrowthSwirl","GrowthBloom","MissionComplete","Purchase","RecordSaved","Error"})
            {
                var clip=Resources.Load<AudioClip>("Audio/Sfx/"+name);
                Check(clip!=null && clip.length>.03f && clip.length<=4 && clip.channels==1,name+" is a bounded mono audio resource");
                var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                Check(importer!=null && importer.defaultSampleSettings.loadType==AudioClipLoadType.DecompressOnLoad &&
                    importer.defaultSampleSettings.preloadAudioData && !importer.ambisonic,name+" imports as preloaded non-streaming short audio");
                clip.LoadAudioData();var pcm=new float[clip.samples*clip.channels];
                Check(clip.GetData(pcm,0) && pcm.Any(value=>Mathf.Abs(value)>.0001f) && pcm.All(value=>!float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Abs(value)<=1),
                    name+" contains finite, nonzero PCM without digital clipping");
            }
            AwakePlayer();GardenSfxPlayer.SetVolume(1);GardenSfxPlayer.SaveVolume();
            Check(Sources().Length==5 && player.GetComponents<AudioSource>().Length==5 && player.GetComponent<AudioListener>()==null,
                "one SFX player owns exactly five reusable sources and no listener");
            new GameObject("Synthetic duplicate SFX player").AddComponent<GardenSfxPlayer>();yield return null;
            Check(Players().Length==1 && player.GetComponents<AudioSource>().Length==5,"duplicate initialization retains one player and five sources");
            foreach(float value in new[]{0,.5f,1})
            {
                GardenSfxPlayer.SetVolume(value);GardenSfxPlayer.SaveVolume();DestroyPlayer();
                Check(GardenSfxPlayer.Volume==value && Players().Length==0,"saved "+value+" volume survives player destruction without getter allocation");
                // Instantiate explicitly only inside the isolated validation to
                // test startup loading without changing the persisted value.
                player=new GameObject("Synthetic SFX reload").AddComponent<GardenSfxPlayer>();Lifecycle("OnApplicationFocus",true);
                CheckLevels(value,"startup restores "+value+" volume");
            }
            GardenSfxPlayer.Play(GardenSfxCue.GrowthSwirl);GardenSfxPlayer.SetVolume(.5f);
            Check(Active()==1,"growth cue starts on one pooled source");CheckLevels(.5f,"volume scales an already playing cue immediately");
            Check(Pending(),"dragged volume is queued for a debounced save");yield return new WaitForSecondsRealtime(.35f);
            Check(!Pending() && PlayerPrefs.GetFloat(VolumeKey)==.5f,"volume flushes after 250ms idle");
            GardenSfxPlayer.SetVolume(-1);CheckLevels(0,"negative volume clamps to silence");
            Check(Active()==0,"mute stops all currently playing cues");
            GardenSfxPlayer.SetVolume(2);CheckLevels(1,"excess volume clamps to one");
            GardenSfxPlayer.SetVolume(float.NaN);CheckLevels(0,"NaN volume clamps to silence");
            GardenSfxPlayer.SetVolume(1);GardenSfxPlayer.SetVolume(float.PositiveInfinity);CheckLevels(0,"infinite volume clamps to silence");
            GardenSfxPlayer.Play(GardenSfxCue.MissionComplete);Check(Active()==0,"muted requests are discarded");
            GardenSfxPlayer.SetVolume(.4f);PlayerPrefs.DeleteKey(VolumeKey);PlayerPrefs.Save();
            GardenSfxPlayer.RestoreInitialSettings();GardenSfxPlayer.SaveVolume();
            Check(GardenSfxPlayer.Volume==1 && !Pending() && !PlayerPrefs.HasKey(VolumeKey),"full reset restores the default without recreating deleted keys");

            music=GardenMusicPlayer.Ensure();originalClock=GardenVisualClock.OverrideHour;clockCaptured=true;
            GardenVisualClock.SetOverrideMinutes(12*60);GardenMusicPlayer.SetVolume(1);GardenMusicPlayer.SaveVolume();
            typeof(GardenMusicPlayer).GetMethod("OnApplicationFocus",Hidden).Invoke(music,new object[]{true});
            typeof(GardenMusicPlayer).GetMethod("OnApplicationPause",Hidden).Invoke(music,new object[]{false});
            yield return new WaitForSecondsRealtime(8.4f);CaptureMusic();
            var playingMusic=musicChannels.Single(source=>source.isPlaying);int musicSamples=playingMusic.timeSamples;
            foreach(GardenSfxCue cue in Enum.GetValues(typeof(GardenSfxCue)))
            {
                GardenSfxPlayer.StopAll();yield return new WaitForSecondsRealtime(.22f);GardenSfxPlayer.Play(cue);
                // LateUpdate resolves generic clicks after button callbacks.
                yield return null;
                Check(Playing(cue),cue+" resolves to its supplied clip and plays");
                var pcm=new float[512];float peak=0;
                for(int frame=0;frame<5;frame++)
                {
                    foreach(var source in Sources())if(source.isPlaying){source.GetOutputData(pcm,0);foreach(float sample in pcm)peak=Mathf.Max(peak,Mathf.Abs(sample));}
                    yield return null;
                }
                report.outputPeak=Mathf.Max(report.outputPeak,peak);
            }
            Check(report.outputPeak>.00001f,"SFX sources produce real nonzero mixer PCM");
            Check(playingMusic.isPlaying && playingMusic.timeSamples>musicSamples+1000,"background music advances throughout all SFX cues without restarting");
            CheckMusic("SFX playback leaves the music clip, context, gains, levels, preferences, and listener unchanged");

            GardenSfxPlayer.StopAll();GardenSfxPlayer.Play(GardenSfxCue.GrowthSwirl);yield return new WaitForSecondsRealtime(.22f);
            GardenSfxPlayer.Play(GardenSfxCue.UiClick);yield return null;
            Check(Playing(GardenSfxCue.GrowthSwirl) && Playing(GardenSfxCue.UiClick),"generic clicks may overlap growth without interrupting it");
            GardenSfxPlayer.StopGrowth();Check(!Playing(GardenSfxCue.GrowthSwirl) && Playing(GardenSfxCue.UiClick),"StopGrowth stops only growth sources");
            GardenSfxPlayer.StopAll();yield return new WaitForSecondsRealtime(.22f);
            GardenSfxPlayer.Play(GardenSfxCue.UiClick);GardenSfxPlayer.Play(GardenSfxCue.MissionComplete);yield return null;
            Check(Playing(GardenSfxCue.MissionComplete) && !Playing(GardenSfxCue.UiClick),"semantic result replaces a generic click from the same activation");
            GardenSfxPlayer.Play(GardenSfxCue.UiClick);yield return null;
            Check(Active()==1 && Playing(GardenSfxCue.MissionComplete),"a later generic listener cannot overshadow the semantic result");
            GardenSfxPlayer.StopAll();yield return new WaitForSecondsRealtime(.22f);
            for(int i=0;i<50;i++)GardenSfxPlayer.Play(GardenSfxCue.MissionComplete);
            Check(Active()==1,"rapid repeated semantic callbacks cannot layer duplicate result sounds");
            GardenSfxPlayer.StopAll();yield return new WaitForSecondsRealtime(.22f);
            // Fixed-duration synthetic clips isolate pool policy from the very
            // short shipped click/error tails. Restore the resource cache after
            // this check; normal-cue and mixer tests above use the real assets.
            var cached=(AudioClip[])typeof(GardenSfxPlayer).GetField("clips",Hidden).GetValue(player);
            var savedClips=cached.ToArray();poolClip=AudioClip.Create("Synthetic pool duration",44100*2,1,44100,false);
            var poolPcm=new float[44100*2];for(int i=0;i<poolPcm.Length;i++)poolPcm[i]=.02f*Mathf.Sin(2*Mathf.PI*330*i/44100);
            poolClip.SetData(poolPcm,0);for(int i=0;i<cached.Length;i++)cached[i]=poolClip;
            foreach(var cue in new[]{GardenSfxCue.MissionComplete,GardenSfxCue.PracticeEnd,GardenSfxCue.Purchase,GardenSfxCue.RecordSaved,GardenSfxCue.Error})
                GardenSfxPlayer.Play(cue);
            Check(Active()==5 && player.GetComponents<AudioSource>().Length==5,"distinct concurrent events remain bounded to the five-source pool");
            yield return new WaitForSecondsRealtime(.22f);GardenSfxPlayer.Play(GardenSfxCue.UiClick);yield return null;
            Check(Active()==5 && !Playing(GardenSfxCue.UiClick),"low-priority UI cannot evict a full pool of result cues");
            GardenSfxPlayer.Play(GardenSfxCue.GrowthBloom);
            Check(Active()==5 && Playing(GardenSfxCue.GrowthBloom) && !Playing(GardenSfxCue.RecordSaved),
                "a higher-priority event replaces the oldest lower-priority cue without allocating a source");
            GardenSfxPlayer.StopAll();Array.Copy(savedClips,cached,cached.Length);UnityEngine.Object.DestroyImmediate(poolClip);poolClip=null;

            GardenSfxPlayer.StopAll();yield return new WaitForSecondsRealtime(.22f);
            testButton=new GameObject("Synthetic SFX button",typeof(RectTransform),typeof(Button));var button=testButton.GetComponent<Button>();
            for(int i=0;i<8;i++)GardenSfxButton.Attach(button,GardenSfxCue.RecordSaved);
            Check(RuntimeListenerCount(button)==1,"repeated binding retains exactly one actual onClick listener");
            Check(GardenSfxButton.CueForLabel(" ‹ 이전 단계 ")==GardenSfxCue.UiBack && GardenSfxButton.CueForLabel("닫기")==GardenSfxCue.UiBack &&
                GardenSfxButton.CueForLabel("시작")==GardenSfxCue.UiClick,"standard back labels select the soft back cue");
            int accepted=0;button.onClick.AddListener(()=>accepted++);
            var eventSystem=EventSystem.current;
            Check(eventSystem!=null,"synthetic button uses the existing EventSystem");
            button.OnSubmit(new BaseEventData(eventSystem));
            Check(accepted==1 && Active()==1 && testButton.GetComponents<GardenSfxButton>().Length==1,
                "keyboard submit plays exactly once after repeated helper binding");
            GardenSfxPlayer.StopAll();button.interactable=false;
            button.OnSubmit(new BaseEventData(eventSystem));button.OnPointerClick(new PointerEventData(eventSystem){button=PointerEventData.InputButton.Left});
            Check(accepted==1 && Active()==0,"disabled keyboard and pointer activations play no sound");
            button.interactable=true;GardenSfxButton.Attach(button);yield return new WaitForSecondsRealtime(.22f);
            var pointer=new PointerEventData(eventSystem){button=PointerEventData.InputButton.Left};
            button.OnPointerDown(pointer);button.OnPointerExit(pointer);button.OnPointerUp(pointer);yield return null;
            Check(accepted==1 && Active()==0,"pointer-down followed by cancellation plays no sound");
            for(int i=0;i<10;i++)button.OnPointerClick(pointer);yield return null;
            Check(accepted==11 && Active()==1,"accepted rapid UI clicks are throttled to one audible cue");
            GardenSfxPlayer.StopAll();GardenSfxButton.Suppress(button);button.OnSubmit(new BaseEventData(eventSystem));yield return null;
            Check(accepted==12 && Active()==0,"semantic buttons suppress generic sound without removing their action listener");
            GardenSfxPlayer.Play(GardenSfxCue.Error);
            Check(Playing(GardenSfxCue.Error) && !Playing(GardenSfxCue.MissionComplete) && !Playing(GardenSfxCue.Purchase),
                "explicit failure cue does not produce a success cue");

            GardenSfxPlayer.StopAll();yield return new WaitForSecondsRealtime(.22f);
            testSlider=new GameObject("Synthetic SFX slider",typeof(RectTransform),typeof(Slider));var slider=testSlider.GetComponent<Slider>();
            slider.onValueChanged.AddListener(GardenSfxPlayer.SetVolume);GardenSfxSliderFeedback.Attach(slider);GardenSfxSliderFeedback.Attach(slider);
            var feedback=slider.GetComponent<GardenSfxSliderFeedback>();feedback.OnPointerDown(pointer);slider.value=.5f;slider.value=.6f;
            Check(Active()==0 && Pending() && testSlider.GetComponents<GardenSfxSliderFeedback>().Length==1,
                "slider drag changes volume silently and attaches only one release helper");
            feedback.OnPointerUp(pointer);yield return null;
            Check(Active()==1 && Playing(GardenSfxCue.UiClick) && !Pending(),"slider release flushes the final value and previews one quiet click");
            GardenSfxPlayer.StopAll();feedback.OnPointerUp(pointer);yield return null;
            Check(Active()==0,"an unmatched repeated pointer-up does not replay the slider preview");
            slider.interactable=false;feedback.OnPointerDown(pointer);feedback.OnPointerUp(pointer);yield return null;
            Check(Active()==0,"disabled slider release plays no preview");
            slider.interactable=true;yield return new WaitForSecondsRealtime(.22f);
            feedback.OnPointerDown(pointer);
            typeof(GardenSfxSliderFeedback).GetMethod("OnApplicationPause",Hidden).Invoke(feedback,new object[]{true});
            Lifecycle("OnApplicationPause",true);Lifecycle("OnApplicationPause",false);
            typeof(GardenSfxSliderFeedback).GetMethod("OnApplicationPause",Hidden).Invoke(feedback,new object[]{false});
            feedback.OnPointerUp(pointer);yield return null;
            Check(Active()==0,"a slider gesture interrupted by app background cannot preview after returning");
            yield return new WaitForSecondsRealtime(.22f);feedback.OnPointerDown(pointer);
            typeof(GardenSfxSliderFeedback).GetField("cancelForFocusLoss",Hidden).SetValue(feedback,true);
            typeof(GardenSfxSliderFeedback).GetMethod("OnApplicationFocus",Hidden).Invoke(feedback,new object[]{false});
            typeof(GardenSfxSliderFeedback).GetMethod("OnApplicationFocus",Hidden).Invoke(feedback,new object[]{true});
            feedback.OnPointerUp(pointer);yield return null;
            Check(Active()==0,"desktop focus loss cancels an armed slider release");
            yield return new WaitForSecondsRealtime(.22f);feedback.OnPointerDown(pointer);
            typeof(GardenSfxSliderFeedback).GetField("cancelForFocusLoss",Hidden).SetValue(feedback,false);
            typeof(GardenSfxSliderFeedback).GetMethod("OnApplicationFocus",Hidden).Invoke(feedback,new object[]{false});
            typeof(GardenSfxSliderFeedback).GetMethod("OnApplicationFocus",Hidden).Invoke(feedback,new object[]{true});
            feedback.OnPointerUp(pointer);yield return null;
            Check(Active()==1 && Playing(GardenSfxCue.UiClick),"Android keyboard-only focus loss does not cancel an accepted slider release");
            typeof(GardenSfxSliderFeedback).GetField("cancelForFocusLoss",Hidden).SetValue(feedback,true);

            GardenSfxPlayer.Play(GardenSfxCue.GrowthSwirl);GardenSfxPlayer.SetVolume(0);
            Check(Active()==0,"muting overlapping cues stops all sources immediately");
            GardenSfxPlayer.SetVolume(1);yield return new WaitForSecondsRealtime(.22f);
            Check(Active()==0,"unmute never replays stale cues");
            GardenSfxPlayer.Play(GardenSfxCue.GrowthSwirl);GardenSfxPlayer.SetVolume(.4f);FocusPolicy(true);Lifecycle("OnApplicationFocus",false);
            Check(Active()==0 && !Pending() && PlayerPrefs.GetFloat(VolumeKey)==.4f,"desktop focus loss stops one-shots and flushes pending volume");
            GardenSfxPlayer.Play(GardenSfxCue.MissionComplete);Lifecycle("OnApplicationFocus",true);yield return new WaitForSecondsRealtime(.22f);
            Check(Active()==0,"desktop return discards background requests without replay");
            FocusPolicy(false);GardenSfxPlayer.Play(GardenSfxCue.GrowthSwirl);Lifecycle("OnApplicationFocus",false);
            Check(Playing(GardenSfxCue.GrowthSwirl),"Android keyboard-only focus loss retains the current cue");
            GardenSfxPlayer.SetVolume(.6f);Lifecycle("OnApplicationPause",true);
            Check(Active()==0 && !Pending() && PlayerPrefs.GetFloat(VolumeKey)==.6f,"Android activity pause stops cues and flushes the latest volume");
            GardenSfxPlayer.Play(GardenSfxCue.MissionComplete);Lifecycle("OnApplicationFocus",true);
            Check(Active()==0,"focus return alone cannot play while the activity is paused");
            Lifecycle("OnApplicationPause",false);yield return new WaitForSecondsRealtime(.1f);
            Check(Active()==0,"activity resume starts no stale one-shots");
            GardenSfxPlayer.SetVolume(.35f);DestroyPlayer();
            Check(PlayerPrefs.GetFloat(VolumeKey)==.35f && GardenSfxPlayer.Volume==.35f,"destruction flushes pending volume and preserves it for restart");
            CheckMusic("SFX volume, mute, burst, button, and lifecycle tests preserve all music and listener state");

            AwakePlayer();GardenSfxPlayer.SetVolume(1);GardenSfxPlayer.StopAll();
            var nav=UnityEngine.Object.FindFirstObjectByType<MockupNavigation>();nav.ShowIsland();yield return null;
            var home=nav.home;
            Check(home!=null && home.activity!=null && home.activity.HasOpenState && home.gardenCamera.targetTexture!=null,
                "real scene garden is ready for the growth sound integration check");
            var before=home.activity.Service.Snapshot;string savedState=StateCodec.Encode(before);var after=StateCodec.Clone(before);
            Check(!before.PlantAppearance.Recipe.Mature,"synthetic garden has an unfinished plant");
            after.PlantAppearance.Recipe.Counts[(int)GrowthCategory.Breath]++;
            after.PlantAppearance.Recipe=PlantGenerator.Advance(after.PlantAppearance.Recipe);
            originalReducedMotion=FigmaMissionUi.ReducedMotion;reducedMotionCaptured=true;FigmaMissionUi.ReducedMotion=false;
            int completions=0;
            Check(GardenMissionGrowthSequence.TryPlay(home,before,after,()=>completions++),"actual garden growth presenter accepts the synthetic next stage");
            var sequence=home.GetComponent<GardenMissionGrowthSequence>();
            Check(sequence.IsPlaying && Playing(GardenSfxCue.GrowthSwirl),"actual growth begin starts the swirl cue");
            var bloomStarts=new HashSet<float>();float growthDeadline=Time.unscaledTime+8;
            while(sequence.IsPlaying && Time.unscaledTime<growthDeadline)
            {
                var starts=(float[])typeof(GardenSfxPlayer).GetField("startedAt",Hidden).GetValue(player);
                for(int i=0;i<Sources().Length;i++)if(Sources()[i].isPlaying && Cues()[i]==GardenSfxCue.GrowthBloom)bloomStarts.Add(starts[i]);
                yield return null;
            }
            Check(!sequence.IsPlaying && completions==1 && bloomStarts.Count==1 && !Playing(GardenSfxCue.GrowthSwirl) && !Playing(GardenSfxCue.GrowthBloom),
                "actual reveal plays bloom exactly once and completed growth leaves no residual sound");
            Check(StateCodec.Encode(home.activity.Service.Snapshot)==savedState,"growth presentation and sound do not mutate the committed synthetic garden");
            Check(!GardenMissionGrowthSequence.TryPlay(home,before,before,()=>completions++) && Active()==0,
                "reentering an unchanged plant starts neither growth nor sound");
            Check(GardenMissionGrowthSequence.TryPlay(home,before,after,()=>completions++),"a second synthetic presentation can start after the first finishes");
            Check(Playing(GardenSfxCue.GrowthSwirl),"second growth starts a fresh swirl");
            sequence.Interrupt();sequence.Interrupt();yield return null;
            Check(!sequence.IsPlaying && completions==2 && Active()==0,"interrupt stops growth audio and releases its callback exactly once");
            FigmaMissionUi.ReducedMotion=true;yield return new WaitForSecondsRealtime(.22f);
            Check(GardenMissionGrowthSequence.TryPlay(home,before,after,()=>completions++),"reduced-motion growth uses the actual quiet presenter");
            bool quiet=true;growthDeadline=Time.unscaledTime+3;
            while(sequence.IsPlaying && Time.unscaledTime<growthDeadline){quiet&=Active()==0;yield return null;}
            Check(quiet && !sequence.IsPlaying && completions==3 && Active()==0,
                "reduced-motion growth plays no truncated swirl or bloom and completes once");
            FigmaMissionUi.ReducedMotion=originalReducedMotion;

            // Exercise the real result controller and its actual retry button.
            // Only this disposable MOCK store receives the synthetic practice;
            // failure injection uses the controller's existing commit delegate.
            var ui=home.activity;var service=ui.Service;const string practiceId="synthetic-sfx-completion";
            Check(service.BeginFocusSession(practiceId,"M03") && service.StartSession(practiceId),
                "synthetic untimed M03 starts in the actual scene service");
            nav.ShowActivities();ui.SelectFocusSession(practiceId);
            var definition=FocusMissionCatalog.FindForSession(service.Snapshot.Sessions.Single(item=>item.SessionId==practiceId));
            for(int step=0;step<definition.Steps.Length;step++)
            {
                var instruction=definition.Steps[step];string answer=instruction.Options.FirstOrDefault() ?? "";
                Check(instruction.RecommendedSeconds==0 && service.ConfirmFocusStep(practiceId,step,answer,answer.Length==0),
                    "synthetic M03 confirms untimed step "+step);
            }
            string pendingState=StateCodec.Encode(service.Snapshot);int coinsBefore=service.CoinBalance;
            int receiptsBefore=service.Snapshot.PracticeGrowthRecords.Count,continued=0,commitAttempts=0;bool rejectCommit=true;
            Func<bool> commit=()=>{commitAttempts++;return !rejectCommit && service.CompletePracticeSession(practiceId);};
            Action continuation=()=>{continued++;ui.ShowFocusPractice();};
            var handleCompletion=typeof(WeekOneQuestDemo).GetMethod("HandlePracticeCompletion",Hidden);
            Check(handleCompletion!=null,"actual practice completion handler is available");
            GardenSfxPlayer.StopAll();yield return new WaitForSecondsRealtime(.22f);
            handleCompletion.Invoke(ui,new object[]{practiceId,continuation,commit});
            Check(commitAttempts==1 && continued==0 && ui.CurrentScreen=="practice-complete-error" &&
                StateCodec.Encode(service.Snapshot)==pendingState && service.GetPracticeGrowth(practiceId)==null,
                "failed actual completion retains its retry page and leaves the practice and reward unchanged");
            Check(Sources().Select((source,index)=>source.clip!=null && Cues()[index]==GardenSfxCue.Error).Count(value=>value)==1 &&
                !Sources().Select((source,index)=>source.clip!=null && Cues()[index]!=GardenSfxCue.Error).Any(value=>value),
                "failed actual completion issues exactly the error cue without a success or growth cue");
            var retry=ui.GetComponentsInChildren<Button>(false).Single(item=>item.name=="PracticeCompletion.Retry");
            GardenSfxPlayer.StopAll();yield return new WaitForSecondsRealtime(.22f);
            rejectCommit=false;FigmaMissionUi.ReducedMotion=true;
            retry.OnSubmit(new BaseEventData(EventSystem.current));
            // A duplicate result callback arriving during presentation must not
            // commit a second result or schedule another completion sound.
            if(home.GetComponent<GardenMissionGrowthSequence>().IsPlaying)
            {
                handleCompletion.Invoke(ui,new object[]{practiceId,continuation,commit});
                Check(commitAttempts==2,"an in-flight completion ignores a duplicate callback");
            }
            float completionDeadline=Time.unscaledTime+6;
            while(continued==0 && Time.unscaledTime<completionDeadline)yield return null;
            var committedSession=service.Snapshot.Sessions.Single(item=>item.SessionId==practiceId);
            Check(continued==1 && commitAttempts==2 && committedSession.Status==ParticipationState.RewardCommitted &&
                committedSession.RewardAmount==30 && service.CoinBalance==coinsBefore+30 &&
                service.Snapshot.PracticeGrowthRecords.Count==receiptsBefore+1 && service.GetPracticeGrowth(practiceId)!=null,
                "actual retry commits one additional practice result, one growth receipt, and exactly thirty coins");
            Check(Active()==1 && Playing(GardenSfxCue.MissionComplete) && ui.CurrentScreen=="focus-complete",
                "successful actual retry reaches completion and plays one semantic completion chime");
            string committedState=StateCodec.Encode(service.Snapshot);
            GardenSfxPlayer.StopAll();yield return new WaitForSecondsRealtime(.22f);
            handleCompletion.Invoke(ui,new object[]{practiceId,continuation,commit});yield return null;
            Check(continued==2 && StateCodec.Encode(service.Snapshot)==committedState && Active()==0,
                "already committed completion callback neither repeats its reward nor plays another chime");
            ui.SelectFocusSession(practiceId);yield return null;
            Check(ui.CurrentScreen=="focus-complete" && StateCodec.Encode(service.Snapshot)==committedState && Active()==0,
                "reopening the completed practice page remains silent and preserves the committed result");
            FigmaMissionUi.ReducedMotion=originalReducedMotion;
        }

        static void Finish(Exception failure)
        {
            EditorApplication.update-=WaitForPlay;EditorApplication.update-=WatchRun;SessionState.SetBool(Key,false);
            DestroyPlayer();if(testButton!=null)UnityEngine.Object.DestroyImmediate(testButton);
            if(testSlider!=null)UnityEngine.Object.DestroyImmediate(testSlider);
            if(poolClip!=null)UnityEngine.Object.DestroyImmediate(poolClip);
            if(reducedMotionCaptured)FigmaMissionUi.ReducedMotion=originalReducedMotion;
            if(clockCaptured)
            {
                if(originalClock.HasValue)GardenVisualClock.SetOverrideMinutes((int)Math.Round(originalClock.Value*60));else GardenVisualClock.UseSystemTime();
            }
            // Flush/destroy disposable managers before restoring the durable
            // backup, so no pending preference write can defeat restoration.
            if(music!=null)UnityEngine.Object.DestroyImmediate(music.gameObject);
            if(File.Exists(BackupPath))
            {
                var backup=JsonUtility.FromJson<PrefBackup>(File.ReadAllText(BackupPath));
                if(backup.hadSfx)PlayerPrefs.SetFloat(VolumeKey,backup.sfx);else PlayerPrefs.DeleteKey(VolumeKey);
                if(backup.hadMusicVolume)PlayerPrefs.SetFloat(MusicVolumeKey,backup.musicVolume);else PlayerPrefs.DeleteKey(MusicVolumeKey);
                if(backup.hadMusicMuted)PlayerPrefs.SetInt(MusicMutedKey,backup.musicMuted);else PlayerPrefs.DeleteKey(MusicMutedKey);
                PlayerPrefs.Save();File.Delete(BackupPath);
            }
            if(report==null)report=new Report();report.status=failure==null?"PASS":"FAIL";report.error=failure?.ToString();
            Directory.CreateDirectory(Root);File.WriteAllText(Path.Combine(Root,"sfx-validation.json"),JsonUtility.ToJson(report,true));
            if(failure!=null)Debug.LogException(failure);else Debug.Log("SFX Play validation: "+report.checks.Count+" PASS, PCM peak "+report.outputPeak);
            EditorApplication.Exit(failure==null?0:1);
        }
    }
}
