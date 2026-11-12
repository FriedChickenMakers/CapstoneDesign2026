using System;
using System.Globalization;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        GardenMemoPopup plantMemoPopup;

        public bool OpenPlantMemo(string plantInstanceId, string memoId, string photoId, Transform owner)
        {
            if (this == null || !opened || HasPendingFullReset || owner == null || !owner.gameObject.activeInHierarchy
                || string.IsNullOrEmpty(plantInstanceId) || string.IsNullOrEmpty(memoId)) return false;
            var state = Service.Snapshot;
            var individual = state.PlantCollection?.Plants.FirstOrDefault(plant => plant.Id == plantInstanceId);
            var knownOrgan = individual?.Memories.FirstOrDefault(memory => memory.OrganId == memoId);
            if (knownOrgan != null && knownOrgan.Kind == MemoryRecordKind.None) return false;
            GardenPlantMemo memo = PlantAlbumHistory.MemoryRecord(state, plantInstanceId, memoId);
            if (string.IsNullOrEmpty(photoId))
            {
                if (screen != "plant-memories:" + plantInstanceId
                    && (!GardenPlantVisuals.HasCurrentPlant(state) || state.PlantAppearance.Recipe.InstanceId != plantInstanceId)) return false;
                if (memo == null) memo = state.PlantAppearance.Memos.FirstOrDefault(value => value.Id == memoId && value.PlantInstanceId == plantInstanceId);
            }
            else
            {
                if (screen != "plant-photo:" + photoId && screen != "plant-memories:" + plantInstanceId) return false;
                var photo = state.PlantPhotos.FirstOrDefault(value => value.Id == photoId && value.SourcePlantInstanceId == plantInstanceId);
                if (photo == null) return false;
                if (memo == null) memo = PlantAlbumHistory.Records(state, photo).FirstOrDefault(value => value.Id == memoId && value.PlantInstanceId == plantInstanceId);
            }
            if (memo == null) return false;
            // The exact individual/organ identity owns this record; viewing another
            // archived plant never changes the active growth target.
            plantMemoPopup = GardenMemoPopup.Show(owner, memo, PlantAlbumHistory.Date(memo, Service.CurrentTimeZone));
            var memory = PlantAlbumHistory.Memory(state, plantInstanceId, memoId);
            if (plantMemoPopup != null && memory != null)
            {
                string organId = memory.OrganId;
                bool fromActivity = owner.IsChildOf(transform);
                Action returnToOrigin = CapturePlantMemoryReturn(plantInstanceId, photoId, fromActivity);
                Action prepareOrigin = () => {
                    if (fromActivity) return;
                    PreparePlantMemoryPage();
                    // Android Back from an editor opened on the garden returns to
                    // the garden, rather than an unrelated previously visited tab.
                    EnterPage("plant-memory-garden:" + plantInstanceId, returnToOrigin);
                };
                plantMemoPopup.ConfigureActions(() => {
                    prepareOrigin(); ShowPlantMemoryEditor(plantInstanceId, organId, photoId, returnToOrigin);
                }, () => {
                    prepareOrigin(); ShowPlantMemoryDeletion(plantInstanceId, organId, photoId, returnToOrigin);
                });
            }
            if(plantMemoPopup!=null)GardenSfxPlayer.Play(GardenSfxCue.UiClick);
            return plantMemoPopup != null;
        }

        Action CapturePlantMemoryReturn(string plantId, string photoId, bool fromActivity)
        {
            bool list = fromActivity && screen == "plant-memories:" + plantId;
            bool photo = fromActivity && !string.IsNullOrEmpty(photoId) && screen == "plant-photo:" + photoId;
            var sourceScroll = list ? pageViewport?.GetComponent<ScrollRect>()
                : photo ? activityFrame?.GetComponentsInChildren<ScrollRect>().FirstOrDefault(value => value.name == "PlantAlbum.RecordsViewport") : null;
            Vector2 position = sourceScroll != null ? sourceScroll.content.anchoredPosition : Vector2.zero;
            return () => {
                if (this == null || !opened || HasPendingFullReset) return;
                if (!fromActivity)
                {
                    int origin = pageHistory.FindLastIndex(entry => entry.Key == "plant-memory-garden:" + plantId);
                    if (origin >= 0) pageHistory.RemoveRange(origin + 1, pageHistory.Count - origin - 1);
                    GoGarden(); return;
                }
                if (list) ShowPlantMemories(plantId);
                else if (photo) ShowPlantPhoto(photoId);
                else { ReturnToPlantMemory(plantId, photoId); return; }
                Canvas.ForceUpdateCanvases();
                var restoredScroll = list ? pageViewport?.GetComponent<ScrollRect>()
                    : activityFrame?.GetComponentsInChildren<ScrollRect>().FirstOrDefault(value => value.name == "PlantAlbum.RecordsViewport");
                if (restoredScroll != null)
                {
                    restoredScroll.StopMovement();
                    restoredScroll.content.anchoredPosition = position;
                    CourseRecordsLayout.ClampScroll(restoredScroll.viewport);
                }
            };
        }

        void ClosePlantMemoPopup()
        {
            if (plantMemoPopup != null) plantMemoPopup.Close();
            plantMemoPopup = null;
        }
    }
}
