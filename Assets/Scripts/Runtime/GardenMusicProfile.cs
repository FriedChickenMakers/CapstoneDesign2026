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
        public AudioClip night;
        [Range(0,1)] public float volume=.25f;
        [Range(.1f,10)] public float crossfadeSeconds=.8f;

        public AudioClip TrackFor(GardenMusicContext context) => TrackFor(context, GardenVisualClock.CurrentHour);

        public AudioClip TrackFor(GardenMusicContext context, double hour)
        {
            // Music follows the same 18:00–06:00 period as the garden lamps.
            if(night!=null && GardenStreetLighting.IsNightHour(hour))return night;
            if(context==GardenMusicContext.Activity)return activity!=null?activity:homeGarden;
            if(context==GardenMusicContext.Settings)return settings!=null?settings:homeGarden;
            return homeGarden;
        }
    }
}
