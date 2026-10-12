using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public enum GardenMusicContext { HomeGarden, Activity, Settings }

    [CreateAssetMenu(menuName="Mind Garden/Music Profile")]
    public sealed class GardenMusicProfile : ScriptableObject
    {
        public AudioClip homeGarden;
        public AudioClip activity;
        public AudioClip settings;
        [Range(0,1)] public float volume=.25f;
        [Range(.1f,3)] public float crossfadeSeconds=.8f;

        public AudioClip TrackFor(GardenMusicContext context)
        {
            if(context==GardenMusicContext.Activity)return activity!=null?activity:homeGarden;
            if(context==GardenMusicContext.Settings)return settings!=null?settings:homeGarden;
            return homeGarden;
        }
    }
}
