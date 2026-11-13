using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public enum GardenSfxCue
    {
        UiClick, UiBack, PracticeStart, PracticeEnd, GrowthSwirl, GrowthBloom,
        MissionComplete, Purchase, RecordSaved, Error, PhotoShutter
    }

    // Independent one-shots: the music player owns the app's listener and is
    // deliberately never consulted or changed by this service.
    [DisallowMultipleComponent]
    public sealed class GardenSfxPlayer : MonoBehaviour
    {
        const string VolumeKey="capstone.sfx.volume";
        const int PoolSize=5, CueCount=11;
        const float UiInterval=.075f, SemanticUiQuietTime=.18f, SemanticInterval=.14f;
        static GardenSfxPlayer instance;
        readonly AudioSource[] channels=new AudioSource[PoolSize];
        readonly GardenSfxCue[] channelCues=new GardenSfxCue[PoolSize];
        readonly float[] gains=new float[PoolSize], startedAt=new float[PoolSize];
        readonly int[] priorities=new int[PoolSize];
        readonly AudioClip[] clips=new AudioClip[CueCount];
        readonly bool[] clipRequested=new bool[CueCount];
        readonly float[] lastCueAt=new float[CueCount];
        float userVolume=1, preferenceSaveAt, lastUiAt=float.NegativeInfinity, lastSemanticAt=float.NegativeInfinity;
        bool preferencesPending, focused=true, appPaused, pauseForFocusLoss, hasPendingUi;
        GardenSfxCue pendingUi;

        // Reading settings must neither add objects nor create preference keys.
        // 100% uses the quiet authored cue gains below, not full device gain.
        public static float Volume=>instance!=null?instance.userVolume:SavedVolume();
        static float BoundedVolume(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp01(value);
        static float SavedVolume()=>BoundedVolume(PlayerPrefs.GetFloat(VolumeKey,1));
        bool Suspended=>userVolume<=0 || appPaused || (pauseForFocusLoss && !focused);

        static GardenSfxPlayer Ensure()
        {
            if(instance==null)new GameObject("Garden sound effects").AddComponent<GardenSfxPlayer>();
            return instance;
        }

        public static void Play(GardenSfxCue cue)
        {
            if(!Application.isPlaying || (int)cue<0 || (int)cue>=CueCount || Volume<=0)return;
            // Do not create a desktop player for an event received off-screen.
            if(instance==null && Application.platform!=RuntimePlatform.Android && !Application.isFocused)return;
            var player=Ensure();
            if(player.Suspended)return;
            float now=Time.unscaledTime;
            if(IsUi(cue))
            {
                if(now-player.lastUiAt<UiInterval || now-player.lastSemanticAt<SemanticUiQuietTime)return;
                player.lastUiAt=now;player.pendingUi=cue;player.hasPendingUi=true;
                // Dispatch after all button listeners. A semantic result from
                // the same activation can replace this generic acknowledgement.
                return;
            }
            if(now-player.lastCueAt[(int)cue]<SemanticInterval)return;
            player.lastCueAt[(int)cue]=now;player.lastSemanticAt=now;player.hasPendingUi=false;
            for(int i=0;i<PoolSize;i++)
                if(IsUi(player.channelCues[i]) && player.channels[i].isPlaying)player.StopChannel(i);
            if(cue==GardenSfxCue.GrowthSwirl || cue==GardenSfxCue.GrowthBloom)StopGrowth();
            player.PlayNow(cue,now);
        }

        public static void SetVolume(float value)
        {
            if(!Application.isPlaying)return;
            float next=BoundedVolume(value);
            if(Mathf.Approximately(Volume,next))return;
            var player=Ensure();player.userVolume=next;
            if(next<=0)player.StopAllChannels();
            player.ApplyChannelVolumes();
            PlayerPrefs.SetFloat(VolumeKey,next);
            player.preferencesPending=true;player.preferenceSaveAt=Time.unscaledTime+.25f;
        }

        public static void SaveVolume()
        {
            if(instance==null || !instance.preferencesPending)return;
            PlayerPrefs.Save();instance.preferencesPending=false;
        }

        public static void RestoreInitialSettings()
        {
            // Full reset has already removed preferences. Restoring a default
            // must not recreate them, even when an old debounce was pending.
            if(instance==null)return;
            instance.preferencesPending=false;instance.userVolume=1;
            instance.StopAllChannels();instance.ApplyChannelVolumes();
        }

        public static void StopAll(){if(instance!=null)instance.StopAllChannels();}
        public static void StopGrowth()
        {
            if(instance==null)return;
            for(int i=0;i<PoolSize;i++)
                if(instance.channelCues[i]==GardenSfxCue.GrowthSwirl || instance.channelCues[i]==GardenSfxCue.GrowthBloom)
                    instance.StopChannel(i);
        }

        void Awake()
        {
            if(instance!=null && instance!=this){Destroy(gameObject);return;}
            instance=this;
            if(Application.isPlaying)DontDestroyOnLoad(gameObject);
            userVolume=SavedVolume();focused=Application.isFocused;
            pauseForFocusLoss=Application.platform!=RuntimePlatform.Android;
            for(int i=0;i<CueCount;i++)lastCueAt[i]=float.NegativeInfinity;
            for(int i=0;i<PoolSize;i++)
            {
                var source=gameObject.AddComponent<AudioSource>();channels[i]=source;
                source.playOnAwake=false;source.loop=false;source.spatialBlend=0;
                source.dopplerLevel=0;source.volume=0;source.priority=160;
            }
        }

        void Update()
        {
            if(preferencesPending && Time.unscaledTime>=preferenceSaveAt)SaveVolume();
            for(int i=0;i<PoolSize;i++)
                if(channels[i]!=null && channels[i].clip!=null && !channels[i].isPlaying)StopChannel(i);
        }
        void LateUpdate()
        {
            if(!hasPendingUi)return;
            var cue=pendingUi;hasPendingUi=false;
            if(!Suspended && Time.unscaledTime-lastSemanticAt>=SemanticUiQuietTime)PlayNow(cue,Time.unscaledTime);
        }

        void PlayNow(GardenSfxCue cue,float now)
        {
            var clip=ClipFor(cue);if(clip==null)return;
            int requestedPriority=PriorityFor(cue),slot=-1;
            for(int i=0;i<PoolSize;i++)if(!channels[i].isPlaying){slot=i;break;}
            if(slot<0)
            {
                // A click cannot evict a reward. At capacity, replace the oldest
                // cue of the lowest eligible priority; never allocate a source.
                for(int i=0;i<PoolSize;i++)
                    if(priorities[i]<=requestedPriority && (slot<0 || priorities[i]<priorities[slot] ||
                        (priorities[i]==priorities[slot] && startedAt[i]<startedAt[slot])))slot=i;
            }
            if(slot<0)return;
            StopChannel(slot);channelCues[slot]=cue;priorities[slot]=requestedPriority;startedAt[slot]=now;gains[slot]=GainFor(cue);
            var source=channels[slot];source.clip=clip;source.volume=gains[slot]*userVolume;
            source.priority=160-requestedPriority*24;source.Play();
        }

        AudioClip ClipFor(GardenSfxCue cue)
        {
            int index=(int)cue;
            if(!clipRequested[index])
            {
                clipRequested[index]=true;
                var resource=cue==GardenSfxCue.PracticeEnd?GardenSfxCue.MissionComplete:
                    cue==GardenSfxCue.PhotoShutter?GardenSfxCue.UiClick:cue;
                clips[index]=Resources.Load<AudioClip>("Audio/Sfx/"+resource);
                if(clips[index]==null && cue==GardenSfxCue.UiBack)clips[index]=Resources.Load<AudioClip>("Audio/Sfx/UiClick");
            }
            return clips[index];
        }

        static bool IsUi(GardenSfxCue cue)=>cue==GardenSfxCue.UiClick || cue==GardenSfxCue.UiBack;
        static int PriorityFor(GardenSfxCue cue)
        {
            switch(cue)
            {
                case GardenSfxCue.UiClick:case GardenSfxCue.UiBack:return 0;
                case GardenSfxCue.PracticeStart:case GardenSfxCue.PhotoShutter:case GardenSfxCue.RecordSaved:return 1;
                case GardenSfxCue.GrowthSwirl:case GardenSfxCue.GrowthBloom:return 2;
                default:return 3;
            }
        }
        static float GainFor(GardenSfxCue cue)
        {
            switch(cue)
            {
                case GardenSfxCue.UiClick:return .42f;
                case GardenSfxCue.UiBack:return .46f;
                case GardenSfxCue.PhotoShutter:return .50f;
                case GardenSfxCue.GrowthSwirl:return .60f;
                case GardenSfxCue.GrowthBloom:return .72f;
                case GardenSfxCue.Error:return .55f;
                case GardenSfxCue.MissionComplete:case GardenSfxCue.PracticeEnd:return .75f;
                default:return .65f;
            }
        }

        void ApplyChannelVolumes(){for(int i=0;i<PoolSize;i++)if(channels[i]!=null)channels[i].volume=gains[i]*userVolume;}
        void StopChannel(int index)
        {
            if(channels[index]==null)return;
            channels[index].Stop();channels[index].clip=null;channels[index].volume=0;gains[index]=0;
        }
        void StopAllChannels()
        {
            hasPendingUi=false;
            for(int i=0;i<PoolSize;i++)StopChannel(i);
        }
        void OnApplicationFocus(bool value)
        {
            focused=value;
            if(!value){SaveVolume();if(pauseForFocusLoss)StopAllChannels();}
        }
        void OnApplicationPause(bool value)
        {
            appPaused=value;
            if(value){SaveVolume();StopAllChannels();}
        }
        void OnApplicationQuit(){SaveVolume();}
        void OnDestroy()
        {
            if(instance!=this)return;
            SaveVolume();StopAllChannels();instance=null;
        }
    }
}
