using System;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public sealed partial class GardenHomePresenter
    {
        PlantInstanceState shownPlant;
        public string ShownPlantId => shownPlant?.Id;
        public int MemoryPage { get; private set; }
        public int MemoryPageCount => Math.Max(1, ((shownPlant?.Memories.Count ?? 0) + PlantMemoryGeometry.PageSize - 1) / PlantMemoryGeometry.PageSize);
        PlantMemoryLeaf[] VisibleMemoryInputs = Array.Empty<PlantMemoryLeaf>();

        void BindCurrentPlant(GardenState state)
        {
            var current = WeeklyPlants.Active(state);
            if (shownPlant?.Id != current?.Id) MemoryPage = 0;
            shownPlant = current;
            MemoryPage = Mathf.Clamp(MemoryPage, 0, MemoryPageCount - 1);
            VisibleMemoryInputs = PlantAlbumHistory.Leaves(state, current?.Id);
        }

        public void SetMemoryPage(int page)
        {
            MemoryPage = Mathf.Clamp(page, 0, MemoryPageCount - 1);
            RefreshFromState();
        }

        // Cinematics may display the new individual's seed before its first committed
        // growth. This never changes the active individual, records, or saved recipe.
        public void RenderGrowthAppearance(PlantRecipe recipe, float scale)
        {
            if (recipe == null || float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0) return;
            var memories = recipe.InstanceId == shownPlant?.Id ? VisibleMemoryInputs : null;
            RenderPlant(recipe, 0, scale, memories, memories == null ? 0 : MemoryPage);
            if (Application.isPlaying && gardenCamera != null && IsGardenVisible) gardenCamera.Render();
        }
    }
}
