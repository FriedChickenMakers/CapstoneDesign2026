using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CapstoneDesign.Runtime
{
    // One app-level player survives tab camera/panel changes. No sensor/service is required.
    public sealed class GardenMusicPlayer : MonoBehaviour
    {
        const string MutedKey="capstone.music.muted";
        static GardenMusicPlayer instance;
        [SerializeField] GardenMusicProfile profile;
        readonly AudioSource[] channels=new AudioSource[2];
        readonly float[] gains=new float[2], fadeFrom=new float[2];
        readonly bool[] started=new bool[2];
        readonly List<AudioListener> displacedListeners=new List<AudioListener>();
        AudioListener ownedListener;
        int target=-1;
        float fadeElapsed;
        bool fading, focused=true, appPaused, suspended, muted, pauseForFocusLoss;
        bool nightPeriod;
        AudioClip desiredClip;

        public GardenMusicContext CurrentContext {get;private set;}
        public AudioClip CurrentClip=>desiredClip;
        public float PlaybackSeconds=>target>=0 && channels[target]!=null?channels[target].time:0;
        public bool IsSuspended=>suspended;
        public static bool IsMuted=>instance!=null?instance.muted:PlayerPrefs.GetInt(MutedKey,0)!=0;

        public static GardenMusicPlayer Ensure()
        {
            if(instance==null)new GameObject("Garden music").AddComponent<GardenMusicPlayer>();
            return instance;
        }
        public static void SetMuted(bool value)
        {
            PlayerPrefs.SetInt(MutedKey,value?1:0);PlayerPrefs.Save();
            var player=Ensure();player.muted=value;player.ApplySuspension();
        }

        void Awake()
        {
            if(instance!=null && instance!=this){Destroy(gameObject);return;}
            instance=this;
            if(Application.isPlaying)DontDestroyOnLoad(gameObject);
            if(profile==null)profile=Resources.Load<GardenMusicProfile>("Audio/GardenMusicProfile");
            muted=PlayerPrefs.GetInt(MutedKey,0)!=0;
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
                channels[i].volume=gains[i]*Mathf.Clamp01(profile!=null?profile.volume:.25f);
            }
            if(t<1)return;
            fading=false;
            for(int i=0;i<2;i++)if(i!=target){channels[i].Stop();channels[i].clip=null;started[i]=false;}
        }

        void OnApplicationFocus(bool value)
        {
            // Android's IME also emits focus=false. Activity pause/resume is the app
            // background signal there; desktop focus loss should still pause music.
            focused=value;
            ApplySuspension();
        }
        void OnApplicationPause(bool value){appPaused=value;ApplySuspension();}
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
            SceneManager.sceneLoaded-=SceneLoaded;
            GardenVisualClock.Changed-=RefreshTrack;
            foreach(var listener in displacedListeners)if(listener!=null)listener.enabled=true;
            instance=null;
        }
    }
}
