using UnityEngine;

namespace CapstoneDesign.Runtime
{
    // Presentation only. A future animated prefab can replace this component
    // without owning mission completion, plant recipes, saving or camera flow.
    public abstract class GardenGrowthEffect : MonoBehaviour
    {
        public abstract void Begin(Transform plant);
        public abstract void Sample(float progress);
        public abstract void BeforePlantChanges();
        public abstract void Reveal(Transform plant);
        public abstract void Finish();
        public virtual int ParticleCount => 0;
    }
}
