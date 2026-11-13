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
            // Keep the optional page argument for existing callers. Only the 3D
            // representation is paged; this reading list includes every linked leaf.
            var records = plant.Memories.Where(memory => memory.Kind != MemoryRecordKind.None)
                .Select(memory => new { Memory = memory, Record = PlantAlbumHistory.MemoryRecord(state, plantId, memory.OrganId) })
                .Where(entry => entry.Record != null)
                .OrderByDescending(entry => entry.Record.CreatedUtc, StringComparer.Ordinal)
                .ThenByDescending(entry => entry.Memory.SlotIndex).ToArray();
            bool current = WeeklyPlants.Active(state)?.Id == plantId;
            string photoId = state.PlantPhotos.FirstOrDefault(photo => photo.Id == plant.PhotoId
                && photo.SourcePlantInstanceId == plantId)?.Id;
            string key = "plant-memories:" + plantId;
            var root = BeginFigmaPage(key, () => ShowPlantMemories(plantId, page), FigmaMissionUi.Background, true, "기록");
            var frame = activityFrame;
            FigmaHeader(root, "기억 잎", GardenHomePresenter.CurrentPlantName(plant.Recipe) + " · " + records.Length + "개");
            var back = FigmaMissionUi.Button(root, current ? "‹ 정원" : photoId != null ? "‹ 식물 사진" : "‹ 사진첩",
                16, 0, 100, 40, () => {
                    if (!IsPlantPageCurrent(frame, key) || GardenMemoPopup.IsOpen) return;
                    if (current) GoGarden();
                    else if (photoId != null) ShowPlantPhoto(photoId);
                    else ShowPlantAlbum();
                }, Color.clear, FigmaMissionUi.Green);
            back.name = "PlantMemory.ViewPlant";
            var body = FigmaScroll(root, 126, 500, 1); float y = 8;
            pageViewport.name = "PlantMemory.Scroll";
            foreach (var entry in records)
            {
                string organId = entry.Memory.OrganId;
                y += CreatePlantMemoryRecordRow(body, "PlantMemory", organId, entry.Record, 24, y, 342, () => {
                    if (IsPlantPageCurrent(frame, key) && !GardenMemoPopup.IsOpen)
                        OpenPlantMemo(plantId, organId, photoId, root);
                }) + 8;
            }
            if (records.Length == 0)
            {
                FigmaMissionUi.Label(body, "아직 기록이 연결된 기억 잎이 없어요.\n감정과 메모를 남기면 여기서 다시 볼 수 있어요.",
                    24, y, 342, 88, 17, FigmaMissionUi.Muted).name = "PlantMemory.Empty";
                y += 104;
            }
            var footer = FigmaMissionUi.Box(root, "PlantMemory.Actions", 0, 684, 390, 64, FigmaMissionUi.Background);
            FigmaMissionUi.Button(footer, "사진첩 보기", 24, 8, 342, 48, () => {
                if (IsPlantPageCurrent(frame, key) && !GardenMemoPopup.IsOpen) ShowPlantAlbum();
            }, Color.clear, FigmaMissionUi.Green).name = "PlantMemory.Back";
            ConfigureGardenPage(root, body, y + 8, footer, 64, back, 126);
        }

        void ReturnToPlantMemory(string plantId, string photoId, Action returnToOrigin = null)
        {
            error = null;
            if (returnToOrigin != null) { returnToOrigin(); return; }
            if (!string.IsNullOrEmpty(photoId) && Service.Snapshot.PlantPhotos.Any(p => p.Id == photoId)) ShowPlantPhoto(photoId);
            else ShowPlantMemories(plantId);
        }

        void ShowPlantMemoryEditor(string plantId, string organId, string photoId, Action returnToOrigin = null)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            PreparePlantMemoryPage();
            var state = Service.Snapshot; var memory = PlantAlbumHistory.Memory(state, plantId, organId);
            var memo = PlantAlbumHistory.MemoryRecord(state, plantId, organId);
            if (memory == null || memo == null) { ReturnToPlantMemory(plantId, photoId, returnToOrigin); return; }
            string key = "plant-memory-edit:" + organId;
            var root = BeginFigmaPage(key, () => ShowPlantMemoryEditor(plantId, organId, photoId, returnToOrigin), FigmaMissionUi.Background, true, "기록");
            var frame = activityFrame;
            FigmaHeader(root, "기억 잎 기록 수정", PlantAlbumHistory.Date(memo, Service.CurrentTimeZone));
            var body = FigmaScroll(root, 126, 500, 1); float y = 8;
            pageViewport.name = "PlantMemory.EditScroll";
            var footer = FigmaMissionUi.Box(root, "PlantMemory.Actions", 0, 670, 390, 78, FigmaMissionUi.Background);
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
                int limit = !experience && i == 0 ? 80 : 500; float height = limit == 80 ? 64 : 128;
                inputs[i] = FigmaMissionUi.Input(body, headings[i] + "을 남겨요", 24, y, 342, height, limit);
                inputs[i].name = "PlantMemory.EditField." + i; inputs[i].text = values[i] ?? ""; y += height + 20;
                var keyboard = root.gameObject.AddComponent<FocusMissionKeyboardAvoidance>();
                keyboard.Input = inputs[i]; keyboard.Title = headings[i];
                keyboard.Background = FigmaMissionUi.Background; keyboard.Accent = FigmaMissionUi.Green;
            }
            var failure = notice; failure.name = "PlantMemory.Error";
            failure.text = ""; failure.gameObject.SetActive(false);
            Action<string> fail = message => { failure.text = message; failure.gameObject.SetActive(true); root.GetComponent<GardenPageLayout>()?.Refresh(); };
            FigmaMissionUi.Button(footer, "취소", 24, 10, 165, 56, () => {
                if (IsPlantPageCurrent(frame, key)) ReturnToPlantMemory(plantId, photoId, returnToOrigin);
            }, Color.white, FigmaMissionUi.Green).name = "PlantMemory.Cancel";
            FigmaMissionUi.Button(footer, "저장", 201, 10, 165, 56, () => {
                if (!IsPlantPageCurrent(frame, key)) return;
                var live = PlantAlbumHistory.Memory(Service.Snapshot, plantId, organId);
                if (live == null || live.Kind != memory.Kind || live.RecordId != memory.RecordId)
                { fail("기록이 바뀌었어요. 돌아가서 다시 확인해 주세요."); return; }
                bool saved = experience ? Service.UpdateExperience(memory.RecordId, inputs[0].text, inputs[1].text, inputs[2].text, inputs[3].text)
                    : memory.Kind == MemoryRecordKind.DailyEmotion ? Service.UpdateDailyEmotion(memory.RecordId, inputs[0].text, inputs[1].text)
                    : Service.UpdateReflection(memory.RecordId, inputs[0].text, inputs[1].text);
                if (!saved) { fail("저장하지 못했어요. 입력은 그대로예요. 다시 시도해 주세요."); GardenSfxPlayer.Play(GardenSfxCue.Error);return; }
                GardenSfxPlayer.Play(GardenSfxCue.RecordSaved);
                UpdateGarden(); ReturnToPlantMemory(plantId, photoId, returnToOrigin);
            }).name = "PlantMemory.Save";
            ConfigureGardenPage(root, body, y, footer, 78, top:126);
        }

        void ShowPlantMemoryDeletion(string plantId, string organId, string photoId, Action returnToOrigin = null)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            PreparePlantMemoryPage();
            var memory = PlantAlbumHistory.Memory(Service.Snapshot, plantId, organId);
            if (memory == null) { ReturnToPlantMemory(plantId, photoId, returnToOrigin); return; }
            string key = "plant-memory-delete:" + organId;
            var root = BeginFigmaPage(key, () => ShowPlantMemoryDeletion(plantId, organId, photoId, returnToOrigin), FigmaMissionUi.Background, true, "기록");
            var frame = activityFrame;
            var memo = PlantAlbumHistory.MemoryRecord(Service.Snapshot, plantId, organId);
            FigmaHeader(root, "기억 잎 기록 삭제", memo == null ? "" : PlantAlbumHistory.Date(memo, Service.CurrentTimeZone));
            var body = FigmaScroll(root, 126, 500, 1);
            pageViewport.name = "PlantMemory.DeleteScroll";
            var copy = FigmaMissionUi.Label(body, "이 기억의 내용을 삭제할까요?\n기억 잎의 기록 연결도 해제돼요.\n식물의 모습과 성장 기록은 그대로예요.", 24, 12, 342, 110, 18);
            float copyHeight = Mathf.Max(110, copy.preferredHeight); copy.rectTransform.sizeDelta = new Vector2(342, copyHeight);
            var failure = notice; failure.name = "PlantMemory.Error";
            failure.text = ""; failure.gameObject.SetActive(false);
            var footer = FigmaMissionUi.Box(root, "PlantMemory.Actions", 0, 670, 390, 78, FigmaMissionUi.Background);
            FigmaMissionUi.Button(footer, "돌아가기", 24, 10, 165, 56, () => {
                if (IsPlantPageCurrent(frame, key)) ReturnToPlantMemory(plantId, photoId, returnToOrigin);
            }, Color.white, FigmaMissionUi.Green).name = "PlantMemory.Cancel";
            FigmaMissionUi.Button(footer, "삭제", 201, 10, 165, 56, () => {
                if (!IsPlantPageCurrent(frame, key)) return;
                var live = PlantAlbumHistory.Memory(Service.Snapshot, plantId, organId);
                if (live == null) { ReturnToPlantMemory(plantId, photoId, returnToOrigin); return; }
                if (live.Kind != memory.Kind || live.RecordId != memory.RecordId) return;
                bool saved = memory.Kind == MemoryRecordKind.DailyEmotion ? Service.DeleteDailyEmotion(memory.RecordId)
                    : memory.Kind == MemoryRecordKind.Experience ? Service.DeleteExperience(memory.RecordId)
                    : Service.UpdateReflection(memory.RecordId, "", "");
                if (!saved) { failure.text = "삭제하지 못했어요. 기록은 그대로예요. 다시 시도해 주세요.";
                    failure.gameObject.SetActive(true); root.GetComponent<GardenPageLayout>()?.Refresh(); return; }
                UpdateGarden(); ReturnToPlantMemory(plantId, photoId, returnToOrigin);
            }, FigmaMissionUi.Brown, Color.white).name = "PlantMemory.DeleteConfirm";
            ConfigureGardenPage(root, body, copyHeight + 24, footer, 78, top:126);
        }
    }
}
