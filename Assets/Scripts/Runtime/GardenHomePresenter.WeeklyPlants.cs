using System;
using System.Linq;
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
            var nextLeaves=PlantAlbumHistory.Leaves(state,current?.Id);
            int latest=nextLeaves.Where(m=>m.Linked).Select(m=>m.Slot).DefaultIfEmpty(-1).Max();
            int previous=VisibleMemoryInputs.Where(m=>m.Linked).Select(m=>m.Slot).DefaultIfEmpty(-1).Max();
            // A newly added record should have its leaf in the visible batch.
            // This is view state only; existing records remain in the full list.
            if(shownPlant?.Id!=current?.Id || latest>previous)
                MemoryPage=latest<0?0:latest/PlantMemoryGeometry.PageSize;
            shownPlant = current;
            MemoryPage = Mathf.Clamp(MemoryPage, 0, MemoryPageCount - 1);
            VisibleMemoryInputs = nextLeaves;
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
