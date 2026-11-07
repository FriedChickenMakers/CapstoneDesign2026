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
            GardenPlantMemo memo;
            if (string.IsNullOrEmpty(photoId))
            {
                if (!GardenPlantVisuals.HasCurrentPlant(state) || state.PlantAppearance.Recipe.InstanceId != plantInstanceId) return false;
                memo = state.PlantAppearance.Memos.FirstOrDefault(value => value.Id == memoId && value.PlantInstanceId == plantInstanceId);
            }
            else
            {
                if (screen != "plant-photo:" + photoId) return false;
                var photo = state.PlantPhotos.FirstOrDefault(value => value.Id == photoId && value.SourcePlantInstanceId == plantInstanceId);
                memo = PlantAlbumHistory.Records(state, photo).FirstOrDefault(value => value.Id == memoId && value.PlantInstanceId == plantInstanceId);
            }
            if (memo == null) return false;
            // Use the immutable paper snapshot, not a potentially removed/reused daily record.
            plantMemoPopup = GardenMemoPopup.Show(owner, memo, PlantAlbumHistory.Date(memo, Service.CurrentTimeZone));
            return plantMemoPopup != null;
        }

        void ClosePlantMemoPopup()
        {
            if (plantMemoPopup != null) plantMemoPopup.Close();
            plantMemoPopup = null;
        }
    }
}
