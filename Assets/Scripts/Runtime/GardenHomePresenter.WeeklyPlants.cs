using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Plants;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public sealed partial class GardenHomePresenter
    {
        public string SelectedPlantId { get; private set; }
        public int MemoryPage { get; private set; }
        Vector2? rememberedAngles;
        PlantInstanceState shownPlant;
        bool pendingGrowthPulse;
        float growthPulseStart=-1,renderedScale=1;
        void TickGrowthPulse()
        {
            if(!IsGardenVisible || ProceduralPlant==null)return;
            if(pendingGrowthPulse){growthPulseStart=Time.unscaledTime;pendingGrowthPulse=false;}
            if(growthPulseStart<0)return;
            float t=(Time.unscaledTime-growthPulseStart)/.4f;
            float pulse=FigmaMissionUi.ReducedMotion||t>=1?1:1+.035f*Mathf.Sin(Mathf.PI*t);
            ProceduralPlant.transform.localScale=Vector3.one*(renderedScale*pulse);
            if(gardenCamera!=null&&gardenVisuals.activeInHierarchy)gardenCamera.Render();
            if(t>=1||FigmaMissionUi.ReducedMotion)growthPulseStart=-1;
        }
        public void SelectPlant(string id,int page=0)
        {
            SelectedPlantId=id;MemoryPage=Mathf.Max(0,page);RefreshFromState();
        }
        public void OpenMemories(string[] ids)
        {
            if(activity==null || shownPlant==null)return;
            string plantId=shownPlant.Id;
            navigation?.ShowActivities();activity.ShowMemoryChoices(plantId,ids);
        }
        string MemorySignature => shownPlant==null?"":MemoryPage+":"+string.Join(",",shownPlant.Memories.Select(m=>m.OrganId+":"+m.Kind));
        PlantMemoryLeaf[] VisibleMemoryInputs => shownPlant?.Memories.Select(m=>new PlantMemoryLeaf {
            OrganId=m.OrganId,Slot=m.SlotIndex,Linked=m.Kind!=MemoryRecordKind.None }).ToArray();
        void SelectSavedPlant(GardenState saved)
        {
            shownPlant=saved.PlantCollection?.Plants.FirstOrDefault(p=>p.Id==SelectedPlantId)??WeeklyPlants.Active(saved);
            if(shownPlant!=null)MemoryPage=Mathf.Clamp(MemoryPage,0,Mathf.Max(0,(shownPlant.Memories.Count-1)/WeeklyPlants.PageSize));
        }
    }
}
