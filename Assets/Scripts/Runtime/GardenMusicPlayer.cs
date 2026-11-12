using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CapstoneDesign.Runtime
{
    // One app-level player survives tab camera/panel changes. No sensor/service is required.
    public sealed class GardenMusicPlayer : MonoBehaviour
    {
        const string MutedKey="capstone.music.muted", VolumeKey="capstone.music.volume";
        static GardenMusicPlayer instance;
        [SerializeField] GardenMusicProfile profile;
        readonly AudioSource[] channels=new AudioSource[2];
        readonly float[] gains=new float[2], fadeFrom=new float[2];
        readonly bool[] started=new bool[2];
        readonly List<AudioListener> displacedListeners=new List<AudioListener>();
        AudioListener ownedListener;
        int target=-1;
        float fadeElapsed, userVolume=1, preferenceSaveAt;
        bool preferencesPending;
        bool fading, focused=true, appPaused, suspended, muted, pauseForFocusLoss;
        bool nightPeriod;
        AudioClip desiredClip;

        public GardenMusicContext CurrentContext {get;private set;}
        public AudioClip CurrentClip=>desiredClip;
        public float PlaybackSeconds=>target>=0 && channels[target]!=null?channels[target].time:0;
        public bool IsSuspended=>suspended;
        public static bool IsMuted=>instance!=null?instance.muted:PlayerPrefs.GetInt(MutedKey,0)!=0 || SavedVolume()<=0;
        // 100% preserves the profile's authored level; it is not full device gain.
        // Old installations without this key retain their original on/off state.
        public static float Volume=>IsMuted?0:instance!=null?instance.userVolume:SavedVolume();

        static float BoundedVolume(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp01(value);
        static float SavedVolume()=>BoundedVolume(PlayerPrefs.GetFloat(VolumeKey,1));

        public static GardenMusicPlayer Ensure()
        {
            if(instance==null)new GameObject("Garden music").AddComponent<GardenMusicPlayer>();
            return instance;
        }
        public static void SetMuted(bool value)
        {
            var player=Ensure();
            if(!value && player.userVolume<=0)player.userVolume=1;
            player.muted=value;player.ApplyChannelVolume();player.ApplySuspension();
            player.QueuePreferences();SaveVolume();
        }
        public static void SetVolume(float value)
        {
            var player=Ensure();float next=BoundedVolume(value);
            if(Mathf.Approximately(player.userVolume,next) && player.muted==(next<=0))return;
            player.userVolume=next;player.muted=next<=0;
            player.ApplyChannelVolume();player.ApplySuspension();player.QueuePreferences();
        }
        void QueuePreferences()
        {
            PlayerPrefs.SetFloat(VolumeKey,userVolume);PlayerPrefs.SetInt(MutedKey,muted?1:0);
            preferencesPending=true;preferenceSaveAt=Time.unscaledTime+.25f;
            if(!Application.isPlaying)SaveVolume();
        }
        public static void SaveVolume()
        {
            if(instance==null || !instance.preferencesPending)return;
            PlayerPrefs.Save();instance.preferencesPending=false;
        }

        public static void RestoreInitialSettings()
        {
            // PlayerPrefs has already been cleared. Do not create a new player
            // or write a replacement preference merely to restore its default.
            if(instance==null)return;
            instance.preferencesPending=false;instance.userVolume=1;instance.muted=false;
            instance.ApplyChannelVolume();
            instance.ApplySuspension();
            instance.SetContext(GardenMusicContext.HomeGarden);
        }

        void Awake()
        {
            if(instance!=null && instance!=this){Destroy(gameObject);return;}
            instance=this;
            if(Application.isPlaying)DontDestroyOnLoad(gameObject);
            if(profile==null)profile=Resources.Load<GardenMusicProfile>("Audio/GardenMusicProfile");
            userVolume=SavedVolume();muted=PlayerPrefs.GetInt(MutedKey,0)!=0 || userVolume<=0;
            pauseForFocusLoss=Application.platform!=RuntimePlatform.Android;
            focused=Application.isFocused;
            for(int i=0;i<2;i++)
            {
                var source=gameObject.AddComponent<AudioSource>();channels[i]=source;
                source.playOnAwake=false;source.loop=true;source.spatialBlend=0;
                source.volume=0;source.priority=128;
            }
            ownedListener=gameObject.AddComponent<AudioListener>();
            EnsureSingleListener();SceneManager.sceneLoaded+=SceneLoaded;
            GardenVisualClock.Changed+=RefreshTrack;
            ApplySuspension();SetContext(GardenMusicContext.HomeGarden);
        }

        public void SetContext(GardenMusicContext context)
        {
            CurrentContext=context;EnsureSingleListener();
            RefreshTrack();
        }

        void RefreshTrack()
        {
            double hour=GardenVisualClock.CurrentHour;
            nightPeriod=GardenStreetLighting.IsNightHour(hour);
            var clip=profile!=null?profile.TrackFor(CurrentContext,hour):null;
            // Identity equality preserves both position and any in-flight fade.
            if(clip==desiredClip)return;
            desiredClip=clip;
            int next=-1;
            if(clip!=null)
            {
                for(int i=0;i<2;i++)if(started[i] && channels[i].clip==clip)next=i;
                if(next<0)
                {
                    next=target<0?0:(gains[0]<gains[1]?0:1);
                    channels[next].Stop();channels[next].clip=clip;
                    gains[next]=0;channels[next].volume=0;
                    channels[next].Play();started[next]=true;
                    if(suspended)channels[next].Pause();
                }
            }
            target=next;fadeElapsed=0;fading=true;
            for(int i=0;i<2;i++)fadeFrom[i]=gains[i];
        }

        void Update()
        {
            if(preferencesPending && Time.unscaledTime>=preferenceSaveAt)SaveVolume();
            // The real clock advances without Changed events. Check even while
            // muted/paused so a returning app selects the current period's song.
            if(nightPeriod!=GardenStreetLighting.IsNightHour(GardenVisualClock.CurrentHour))RefreshTrack();
            if(suspended || !fading)return;
            fadeElapsed+=Time.unscaledDeltaTime;
            float t=Mathf.Clamp01(fadeElapsed/Mathf.Max(.1f,profile!=null?profile.crossfadeSeconds:.8f));
            t=t*t*(3-2*t);
            for(int i=0;i<2;i++)
            {
                // Interpolate power to avoid the middle-of-crossfade volume dip.
                // Starting from the current gains also keeps reversals continuous.
                gains[i]=Mathf.Sqrt(Mathf.Lerp(fadeFrom[i]*fadeFrom[i],i==target?1:0,t));
            }
            ApplyChannelVolume();
            if(t<1)return;
            fading=false;
            for(int i=0;i<2;i++)if(i!=target){channels[i].Stop();channels[i].clip=null;started[i]=false;}
        }

        void OnApplicationFocus(bool value)
        {
            // Android's IME also emits focus=false. Activity pause/resume is the app
            // background signal there; desktop focus loss should still pause music.
            focused=value;
            if(!value)SaveVolume();
            ApplySuspension();
        }
        void OnApplicationPause(bool value){if(value)SaveVolume();appPaused=value;ApplySuspension();}
        void ApplyChannelVolume()
        {
            float level=(muted?0:userVolume)*Mathf.Clamp01(profile!=null?profile.volume:.25f);
            for(int i=0;i<2;i++)if(channels[i]!=null)channels[i].volume=gains[i]*level;
        }
        void ApplySuspension()
        {
            bool next=muted || appPaused || (pauseForFocusLoss && !focused);
            if(next==suspended)return;
            suspended=next;
            for(int i=0;i<2;i++)if(started[i] && channels[i]!=null)
            {
                if(suspended)channels[i].Pause();else channels[i].UnPause();
            }
        }

        void SceneLoaded(Scene scene,LoadSceneMode mode){EnsureSingleListener();}
        void EnsureSingleListener()
        {
            if(ownedListener==null)return;
            foreach(var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(listener!=ownedListener && listener.enabled)
                {listener.enabled=false;if(!displacedListeners.Contains(listener))displacedListeners.Add(listener);}
            ownedListener.enabled=true;
        }
        void OnDestroy()
        {
            if(instance!=this)return;
            SaveVolume();
            SceneManager.sceneLoaded-=SceneLoaded;
            GardenVisualClock.Changed-=RefreshTrack;
            foreach(var listener in displacedListeners)if(listener!=null)listener.enabled=true;
            instance=null;
        }
    }
}
