using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        void PreparePlantMemoryPage()
        {
            if (!gameObject.activeInHierarchy) transform.root.GetComponent<MockupNavigation>()?.ShowActivities();
        }

        public void ShowPlantMemories(string plantId, int page = 0)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            PreparePlantMemoryPage();
            var state = Service.Snapshot;
            var plant = state.PlantCollection?.Plants.FirstOrDefault(p => p.Id == plantId);
            if (plant == null) { ShowPlantAlbum(); return; }
            int pages = Math.Max(1, (plant.Memories.Count + WeeklyPlants.PageSize - 1) / WeeklyPlants.PageSize);
            page = Mathf.Clamp(page, 0, pages - 1); int selectedPage = page;
            string key = "plant-memories:" + plantId;
            var root = BeginFigmaPage(key, () => ShowPlantMemories(plantId, selectedPage), FigmaMissionUi.Background, true, "기록");
            var frame = activityFrame;
            FigmaHeader(root, "기억 잎", GardenHomePresenter.CurrentPlantName(plant.Recipe));
            var body = FigmaScroll(root, 132, 628, 700); float y = 8;
            var visible = plant.Memories.Where(m => m.SlotIndex / WeeklyPlants.PageSize == page && m.Kind != MemoryRecordKind.None).ToArray();
            foreach (var memory in visible)
            {
                var memo = PlantAlbumHistory.MemoryRecord(state, plantId, memory.OrganId);
                if (memo == null) continue;
                string organ = memory.OrganId;
                var row = FigmaMissionUi.Button(body, "", 24, y, 342, 84, () => {
                    if (IsPlantPageCurrent(frame, key)) OpenPlantMemo(plantId, organ, plant.PhotoId, root);
                }, Color.white, FigmaMissionUi.Ink);
                row.name = "PlantMemory.Record." + organ;
                FigmaMissionUi.Label(row.transform, PlantAlbumHistory.Date(memo, Service.CurrentTimeZone), 16, 8, 310, 22, 14, FigmaMissionUi.Muted);
                string summary = string.IsNullOrWhiteSpace(memo.Mood) ? "남긴 메모" : memo.Mood;
                if (summary.Length > 40) summary = summary.Substring(0, 40) + "…";
                FigmaMissionUi.Label(row.transform, summary, 16, 34, 310, 40, 17, FigmaMissionUi.Green, true);
                y += 96;
            }
            if (visible.Length == 0)
            {
                FigmaMissionUi.Label(body, "이 가지에는 남긴 기록이 없어요.", 24, y, 342, 64, 17, FigmaMissionUi.Muted);
                y += 80;
            }
            if (pages > 1)
            {
                FigmaMissionUi.Label(body, "기억 잎 " + (page + 1) + " / " + pages, 24, y, 342, 28, 14,
                    FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter); y += 36;
                var previous = FigmaMissionUi.Button(body, "이전 잎", 24, y, 165, 48, () => {
                    if (IsPlantPageCurrent(frame, key)) ShowPlantMemories(plantId, selectedPage - 1);
                }, Color.white, FigmaMissionUi.Green);
                previous.name = "PlantMemory.Previous"; previous.interactable = page > 0;
                var next = FigmaMissionUi.Button(body, "다음 잎", 201, y, 165, 48, () => {
                    if (IsPlantPageCurrent(frame, key)) ShowPlantMemories(plantId, selectedPage + 1);
                }, Color.white, FigmaMissionUi.Green);
                next.name = "PlantMemory.Next"; next.interactable = page < pages - 1; y += 64;
            }
            bool current = WeeklyPlants.Active(state)?.Id == plantId;
            if (current || !string.IsNullOrEmpty(plant.PhotoId))
            {
                FigmaMissionUi.Button(body, current ? "정원에서 이 잎 보기" : "사진으로 보기", 24, y, 342, 48, () => {
                    if (!IsPlantPageCurrent(frame, key)) return;
                    if (!current) { ShowPlantPhoto(plant.PhotoId); return; }
                    var home = transform.root.GetComponent<GardenHomePresenter>();
                    home?.SetMemoryPage(selectedPage); GoGarden(); home?.SetOverview(false);
                }).name = "PlantMemory.ViewPlant"; y += 64;
            }
            FigmaMissionUi.Button(body, "사진첩으로 돌아가기", 24, y, 342, 48, () => {
                if (IsPlantPageCurrent(frame, key)) ShowPlantAlbum();
            }, Color.clear, FigmaMissionUi.Green).name = "PlantMemory.Back";
            body.sizeDelta = new Vector2(390, Math.Max(628, y + 72));
        }

        void ReturnToPlantMemory(string plantId, string photoId)
        {
            error = null;
            if (!string.IsNullOrEmpty(photoId) && Service.Snapshot.PlantPhotos.Any(p => p.Id == photoId)) ShowPlantPhoto(photoId);
            else ShowPlantMemories(plantId);
        }

        void ShowPlantMemoryEditor(string plantId, string organId, string photoId)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            PreparePlantMemoryPage();
            var state = Service.Snapshot; var memory = PlantAlbumHistory.Memory(state, plantId, organId);
            var memo = PlantAlbumHistory.MemoryRecord(state, plantId, organId);
            if (memory == null || memo == null) { ReturnToPlantMemory(plantId, photoId); return; }
            string key = "plant-memory-edit:" + organId;
            var root = BeginFigmaPage(key, () => ShowPlantMemoryEditor(plantId, organId, photoId), FigmaMissionUi.Background, true, "기록");
            var frame = activityFrame;
            FigmaHeader(root, "기억 수정", PlantAlbumHistory.Date(memo, Service.CurrentTimeZone));
            var body = FigmaScroll(root, 132, 628, 760); float y = 8;
            string[] headings, values;
            bool experience = memory.Kind == MemoryRecordKind.Experience;
            if (experience)
            {
                var record = state.Experiences.First(e => e.Id == memory.RecordId);
                headings = new[] { "사건", "몸의 느낌", "감정", "떠오른 생각" };
                values = new[] { record.Event, record.Body, record.Emotion, record.Thought };
            }
            else { headings = new[] { "감정", "메모" }; values = new[] { memo.Mood, memo.Note }; }
            var inputs = new InputField[headings.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                FigmaMissionUi.Label(body, headings[i], 24, y, 342, 26, 16, null, true); y += 34;
                int limit = !experience && i == 0 ? 80 : 500; float height = limit == 80 ? 96 : 160;
                inputs[i] = FigmaMissionUi.Input(body, headings[i] + "을 남겨요", 24, y, 342, height, limit);
                inputs[i].name = "PlantMemory.EditField." + i; inputs[i].text = values[i] ?? ""; y += height + 20;
            }
            var failure = FigmaMissionUi.Label(body, "", 24, y, 342, 56, 15, FigmaMissionUi.Brown);
            failure.name = "PlantMemory.Error"; y += 64;
            FigmaMissionUi.Button(body, "취소", 24, y, 165, 48, () => {
                if (IsPlantPageCurrent(frame, key)) ReturnToPlantMemory(plantId, photoId);
            }, Color.white, FigmaMissionUi.Green).name = "PlantMemory.Cancel";
            FigmaMissionUi.Button(body, "저장", 201, y, 165, 48, () => {
                if (!IsPlantPageCurrent(frame, key)) return;
                var live = PlantAlbumHistory.Memory(Service.Snapshot, plantId, organId);
                if (live == null || live.Kind != memory.Kind || live.RecordId != memory.RecordId)
                { failure.text = "기록이 바뀌었어요. 돌아가서 다시 확인해 주세요."; return; }
                bool saved = experience ? Service.UpdateExperience(memory.RecordId, inputs[0].text, inputs[1].text, inputs[2].text, inputs[3].text)
                    : memory.Kind == MemoryRecordKind.DailyEmotion ? Service.UpdateDailyEmotion(memory.RecordId, inputs[0].text, inputs[1].text)
                    : Service.UpdateReflection(memory.RecordId, inputs[0].text, inputs[1].text);
                if (!saved) { failure.text = "저장하지 못했어요. 입력은 그대로예요. 다시 시도해 주세요."; return; }
                UpdateGarden(); ReturnToPlantMemory(plantId, photoId);
            }).name = "PlantMemory.Save";
            body.sizeDelta = new Vector2(390, Math.Max(628, y + 72));
        }

        void ShowPlantMemoryDeletion(string plantId, string organId, string photoId)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            PreparePlantMemoryPage();
            var memory = PlantAlbumHistory.Memory(Service.Snapshot, plantId, organId);
            if (memory == null) { ReturnToPlantMemory(plantId, photoId); return; }
            string key = "plant-memory-delete:" + organId;
            var root = BeginFigmaPage(key, () => ShowPlantMemoryDeletion(plantId, organId, photoId), FigmaMissionUi.Background, true, "기록");
            var frame = activityFrame;
            FigmaHeader(root, "기억을 지울까요?", "식물의 모습과 성장 기록은 그대로예요.");
            var body = FigmaScroll(root, 132, 628, 420);
            FigmaMissionUi.Label(body, "감정과 메모 내용이 삭제돼요.\n이 잎에는 기록이 연결되지 않아요.", 24, 20, 342, 90, 18);
            var failure = FigmaMissionUi.Label(body, "", 24, 120, 342, 64, 15, FigmaMissionUi.Brown);
            failure.name = "PlantMemory.Error";
            FigmaMissionUi.Button(body, "돌아가기", 24, 204, 165, 48, () => {
                if (IsPlantPageCurrent(frame, key)) ReturnToPlantMemory(plantId, photoId);
            }, Color.white, FigmaMissionUi.Green).name = "PlantMemory.Cancel";
            FigmaMissionUi.Button(body, "삭제", 201, 204, 165, 48, () => {
                if (!IsPlantPageCurrent(frame, key)) return;
                var live = PlantAlbumHistory.Memory(Service.Snapshot, plantId, organId);
                if (live == null) { ReturnToPlantMemory(plantId, photoId); return; }
                if (live.Kind != memory.Kind || live.RecordId != memory.RecordId) return;
                bool saved = memory.Kind == MemoryRecordKind.DailyEmotion ? Service.DeleteDailyEmotion(memory.RecordId)
                    : memory.Kind == MemoryRecordKind.Experience ? Service.DeleteExperience(memory.RecordId)
                    : Service.UpdateReflection(memory.RecordId, "", "");
                if (!saved) { failure.text = "삭제하지 못했어요. 기록은 그대로예요. 다시 시도해 주세요."; return; }
                UpdateGarden(); ReturnToPlantMemory(plantId, photoId);
            }, FigmaMissionUi.Brown, Color.white).name = "PlantMemory.DeleteConfirm";
        }
    }
}
