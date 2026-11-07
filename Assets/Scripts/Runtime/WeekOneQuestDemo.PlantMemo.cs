using System;
using System.Globalization;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;

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
                plantMemoPopup.ConfigureActions(() => ShowPlantMemoryEditor(plantInstanceId, organId, photoId),
                    () => ShowPlantMemoryDeletion(plantInstanceId, organId, photoId));
            }
            return plantMemoPopup != null;
        }

        void ClosePlantMemoPopup()
        {
            if (plantMemoPopup != null) plantMemoPopup.Close();
            plantMemoPopup = null;
        }
    }
}
