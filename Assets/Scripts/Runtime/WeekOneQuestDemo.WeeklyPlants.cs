using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Plants;
using CapstoneDesign.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        int plantCollectionPage;
        string experiencePlantId;
        GardenHomePresenter PlantHome => transform.root.GetComponent<GardenHomePresenter>();
        public void ShowPlantCollection()
        {
            EnterPage("plants",ShowPlantCollection);
            var p=FocusPage("나의 식물함","내 속도로 자라는 정원");var state=Service.Snapshot;
            Guidance(p,"활동을 마치면 하루 최대 3회 자라요. 21회를 채울 때까지 같은 식물과 함께해요. 감정 기록은 선택이에요.");
            GardenUi.FlowLabel(p,"오늘 성장 "+WeeklyPlants.Today(state)+" / 3 · 매일 새벽 4시에 다시 자랄 수 있어요.",GardenUi.BodySize,GardenUi.Muted);
            var plants=state.PlantCollection.Plants.AsEnumerable().Reverse().ToArray();
            int pages=Math.Max(1,(plants.Length+3)/4);plantCollectionPage=Mathf.Clamp(plantCollectionPage,0,pages-1);
            var list=ActivityList(p,"Plant collection");
            foreach(var plant in plants.Skip(plantCollectionPage*4).Take(4))
            {
                string id=plant.Id;
                ActivityListItem(list,WeeklyPlants.Title(plant),PlantDescription(plant),()=>ShowPlantMemories(id,0),PrototypeUiIcon.Symbol.Leaf);
            }
            if(pages>1)
            {
                DockPair("이전 식물",()=>{plantCollectionPage=Math.Max(0,plantCollectionPage-1);ShowPlantCollection();},"다음 식물",()=>{plantCollectionPage=Math.Min(pages-1,plantCollectionPage+1);ShowPlantCollection();});
            }
            DockAction("현재 식물 보기",()=>ViewSavedPlant(state.PlantCollection.ActiveId,0),true);
            DockAction("이전 성장 구매 기록",ShowGrowthHistory);
        }
        static string PlantDescription(PlantInstanceState plant)
        {
            string species=plant.Recipe.SpeciesId==null?(plant.Recipe.GrowthStep==0?"작은 씨앗":"자라는 새싹"):PlantCatalog.Get(plant.Recipe.SpeciesId).Name;
            return species+" · "+(plant.Legacy?"이전 모습 보관":plant.Recipe.Mature?"성장 완료":plant.Recipe.GrowthStep+" / 21회")+" · 기억 "+plant.Memories.Count(m=>m.Kind!=MemoryRecordKind.None)+"개";
        }
        public void ShowPlantMemories(string id,int page)
        {
            var plant=Service.Snapshot.PlantCollection.Plants.FirstOrDefault(x=>x.Id==id);
            if(plant==null){ShowPlantCollection();return;}
            int pages=Math.Max(1,(plant.Memories.Count+20)/21);page=Mathf.Clamp(page,0,pages-1);
            int selectedPage=page;
            EnterPage("plant-memories",()=>ShowPlantMemories(id,selectedPage),id+":"+page);
            var p=FocusPage(WeeklyPlants.Title(plant),PlantDescription(plant));
            GardenUi.FlowLabel(p,(plant.Legacy?"이 모습으로 보관한 날 · ":"함께 시작한 날 · ")+LocalTime(plant.CreatedUtc),GardenUi.BodySize,GardenUi.Muted);
            if(!string.IsNullOrEmpty(plant.CompletedUtc))GardenUi.FlowLabel(p,"성장 완료 · "+LocalTime(plant.CompletedUtc),GardenUi.BodySize,GardenUi.Muted);
            if(plant.Legacy)Guidance(p,"이전 식물의 모습과 구매 기록을 보관했어요.");
            else if(plant.Memories.Count==0)Guidance(p,"감정을 남기면 기억 잎이 생겨요. 기록 없이도 식물은 완성할 수 있어요.");
            var visible=plant.Memories.Where(m=>m.SlotIndex/21==page && m.Kind!=MemoryRecordKind.None).ToArray();
            if(visible.Length>0)
            {
                var list=ActivityList(p,"Memory leaves");
                foreach(var memory in visible)
                {
                    string organ=memory.OrganId;
                    ActivityListItem(list,MemoryDate(memory),memory.Kind==MemoryRecordKind.Experience?"경험 기록":"활동 후 감정",()=>ShowMemory(id,organ),PrototypeUiIcon.Symbol.Leaf);
                }
            }
            if(pages>1)
            {
                GardenUi.FlowLabel(p,"기록 가지 "+(page+1)+" / "+pages);
                DockPair("이전 가지",()=>ShowPlantMemories(id,Math.Max(0,selectedPage-1)),"다음 가지",()=>ShowPlantMemories(id,Math.Min(pages-1,selectedPage+1)));
            }
            DockAction("정원에서 이 가지 보기",()=>ViewSavedPlant(id,selectedPage),true);
            if(!plant.Legacy)DockAction("이 식물에 경험 남기기",()=>{experiencePlantId=id;ShowExperienceTypes();});
            DockAction("식물함",ShowPlantCollection);
        }
        static string MemoryDate(MemoryOrganState memory) => DateTimeOffset.Parse(memory.CreatedUtc).ToOffset(TimeSpan.FromMinutes(memory.UtcOffsetMinutes)).ToString("yyyy년 M월 d일 HH:mm");
        void ViewSavedPlant(string id,int page)
        {
            PlantHome?.SelectPlant(id,page);GoGarden();PlantHome?.SetOverview(false);
        }
        public void ShowMemoryChoices(string plantId,string[] ids)
        {
            if(ids.Length==1){ShowMemory(plantId,ids[0]);return;}
            EnterPage("memory-choices",()=>ShowMemoryChoices(plantId,ids));
            var p=FocusPage("어느 기억을 볼까요?","겹쳐 있는 기억 잎");
            var plant=Service.Snapshot.PlantCollection.Plants.First(x=>x.Id==plantId);var list=ActivityList(p,"Overlapping memory leaves");
            foreach(var memory in plant.Memories.Where(m=>ids.Contains(m.OrganId)))
            {string id=memory.OrganId;ActivityListItem(list,MemoryDate(memory),null,()=>ShowMemory(plantId,id),PrototypeUiIcon.Symbol.Leaf);}
            DockAction("식물로 돌아가기",GoGarden);
        }
        void ShowMemory(string plantId,string organId)
        {
            var state=Service.Snapshot;var plant=state.PlantCollection.Plants.First(x=>x.Id==plantId);
            var memory=plant.Memories.FirstOrDefault(m=>m.OrganId==organId);
            if(memory==null || memory.Kind==MemoryRecordKind.None){ShowPlantMemories(plantId,0);return;}
            // Reading from the list must return to this plant as well as taps
            // originating in the garden, including completed archived plants.
            PlantHome?.SelectPlant(plantId,memory.SlotIndex/WeeklyPlants.PageSize);
            EnterPage("memory",()=>ShowMemory(plantId,organId),organId);
            var p=FocusPage("잎에 남긴 기억",MemoryDate(memory));
            if(memory.Kind==MemoryRecordKind.SessionReflection)
            {
                var session=state.Sessions.First(s=>s.SessionId==memory.RecordId);
                GardenUi.FlowLabel(p,FocusSessionTitle(session),GardenUi.HeadingSize);
                GardenUi.FlowLabel(p,session.Mood??"");GardenUi.FlowLabel(p,session.Note??"");
                DockAction("원래 활동 기록",()=>ShowRecord(session.SessionId),true);
                DockAction("감정 기록 수정",()=>EditPlantReflection(session.SessionId));
            }
            else
            {
                var record=state.Experiences.First(e=>e.Id==memory.RecordId);
                GardenUi.FlowLabel(p,record.Type+" · "+record.Emotion);GardenUi.FlowLabel(p,record.Event);
                DockAction("원래 경험 기록",()=>ShowExperienceRecord(record.Id),true);
                DockAction("경험 기록 수정",()=>EditPlantExperience(record.Id));
            }
            DockAction("이 기록 삭제",()=>ConfirmMemoryDelete(plantId,organId));
            DockAction("식물로 돌아가기",GoGarden);
            DockAction("이 식물의 기억 목록",()=>ShowPlantMemories(plantId,memory.SlotIndex/21));
        }
        void ConfirmMemoryDelete(string plantId,string organId)
        {
            EnterPage("memory-delete",()=>ConfirmMemoryDelete(plantId,organId),organId);
            var memory=Service.Snapshot.PlantCollection.Plants.First(p=>p.Id==plantId).Memories.First(m=>m.OrganId==organId);
            var p=FocusPage("기록을 삭제할까요?","기억 잎");
            Guidance(p,memory.Kind==MemoryRecordKind.SessionReflection?"이 활동의 감정과 한 줄 기록을 삭제해요. 활동 완료와 식물 성장은 유지돼요.":"이 경험 기록을 삭제해요. 식물 성장은 유지되고 잎은 장식으로 남아요.");
            DockPair("취소",()=>ShowMemory(plantId,organId),"기록 삭제",()=>
            {
                bool ok=memory.Kind==MemoryRecordKind.SessionReflection?Service.UpdateReflection(memory.RecordId,null,null):Service.DeleteExperience(memory.RecordId);
                if(Apply(ok))ShowPlantMemories(plantId,memory.SlotIndex/21);
            });
        }
        void EditPlantReflection(string id)
        {
            var record=Service.Snapshot.Sessions.First(s=>s.SessionId==id);
            EnterPage("edit-reflection",()=>EditPlantReflection(id),id);var p=FocusPage("감정 기록 수정","선택 기록");
            GardenUi.FlowLabel(p,"감정");var mood=GardenUi.FlowInput(p,"감정 · 최대 80자");mood.characterLimit=80;mood.text=record.Mood??"";
            GardenUi.FlowLabel(p,"한 줄 기록");var note=GardenUi.FlowInput(p,"최대 500자");note.text=record.Note??"";note.characterLimit=500;
            DockPair("취소",()=>ShowRecord(id),"저장",()=>{if(Apply(Service.UpdateReflection(id,mood.text,note.text)))ShowRecord(id);},true);
        }
        void EditPlantExperience(string id)
        {
            var record=Service.Snapshot.Experiences.First(e=>e.Id==id);
            EnterPage("edit-experience",()=>EditPlantExperience(id),id);var p=FocusPage("경험 기록 수정",record.Type);
            var titles=new[]{"사건","몸 감각","감정","생각"};var values=new[]{record.Event,record.Body,record.Emotion,record.Thought};var inputs=new InputField[4];
            for(int i=0;i<4;i++){GardenUi.FlowLabel(p,titles[i]);inputs[i]=GardenUi.FlowInput(p,"선택 기록");inputs[i].text=values[i];inputs[i].characterLimit=500;}
            DockPair("취소",()=>ShowExperienceRecord(id),"저장",()=>{if(Apply(Service.UpdateExperience(id,inputs[0].text,inputs[1].text,inputs[2].text,inputs[3].text)))ShowExperienceRecord(id);},true);
        }
        void ShowExperiencePlantConfirmation()
        {
            var state=Service.Snapshot;
            var plant=state.PlantCollection.Plants.FirstOrDefault(p=>p.Id==experiencePlantId && !p.Legacy)??WeeklyPlants.Active(state);
            experiencePlantId=plant.Id;
            EnterPage("experience-plant",ShowExperiencePlantConfirmation);
            var p=FocusPage("어느 식물에 남길까요?","경험 기록 저장");
            Guidance(p,WeeklyPlants.Title(plant)+"에 기억 잎으로 남겨요. 성장 횟수와 영양제는 바뀌지 않아요.");
            var list=ActivityList(p,"Experience plant choice");
            // All plants remain reachable through the collection, including older ones.
            ActivityListItem(list,"식물함에서 다른 식물 선택",null,ShowExperiencePlantPicker,PrototypeUiIcon.Symbol.Leaf);
            DockAction("이 식물에 저장",()=>
            {
                if(Apply(Service.SaveExperience(experienceId,experienceType,experienceValues[0],experienceValues[1],experienceValues[2],experienceValues[3],experiencePlantId)))
                {experiencePlantId=null;FinishExperienceNavigation();}
            },true);
            DockAction("기록으로 돌아가기",ShowExperienceStep);
        }
        int experiencePlantPage;
        void ShowExperiencePlantPicker()
        {
            EnterPage("experience-plant-picker",ShowExperiencePlantPicker);var p=FocusPage("함께 기억할 식물","경험 기록");
            var plants=Service.Snapshot.PlantCollection.Plants.Where(x=>!x.Legacy).Reverse().ToArray();
            int pages=Math.Max(1,(plants.Length+3)/4);experiencePlantPage=Mathf.Clamp(experiencePlantPage,0,pages-1);
            var list=ActivityList(p,"Experience plants");
            foreach(var plant in plants.Skip(experiencePlantPage*4).Take(4))
            {string id=plant.Id;ActivityListItem(list,WeeklyPlants.Title(plant),PlantDescription(plant),()=>{experiencePlantId=id;ShowExperiencePlantConfirmation();},PrototypeUiIcon.Symbol.Leaf);}
            if(pages>1)DockPair("이전",()=>{experiencePlantPage=Math.Max(0,experiencePlantPage-1);ShowExperiencePlantPicker();},"다음",()=>{experiencePlantPage=Math.Min(pages-1,experiencePlantPage+1);ShowExperiencePlantPicker();});
            DockAction("돌아가기",ShowExperiencePlantConfirmation);
        }
    }
}
