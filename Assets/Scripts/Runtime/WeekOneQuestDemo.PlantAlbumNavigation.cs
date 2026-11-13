using System.Collections.Generic;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        sealed class PlantPhotoBookmark
        {
            public GardenPlantPhotoView.ViewState View;
            public Vector2 Records;
        }
        readonly Dictionary<string, PlantPhotoBookmark> plantPhotoBookmarks = new Dictionary<string, PlantPhotoBookmark>();
        GardenStateService albumNavigationOwner;
        string albumNavigationInstallation, activePlantPhotoId, albumReturnSelection;
        int albumReturnPage;
        Vector2 albumReturnPosition;
        bool albumReturnPending;

        void BindPlantAlbumNavigation()
        {
            string installation = Service.Snapshot.InstallationId;
            if (albumNavigationOwner == Service && albumNavigationInstallation == installation) return;
            albumNavigationOwner = Service; albumNavigationInstallation = installation;
            plantPhotoBookmarks.Clear(); activePlantPhotoId = null; albumReturnPending = false;
        }

        void RememberPlantAlbumPosition(Button origin = null)
        {
            if (!opened || screen != "plant-album") return;
            BindPlantAlbumNavigation();
            var scroll = pageViewport == null ? null : pageViewport.GetComponent<ScrollRect>();
            albumReturnPosition = scroll?.content == null ? Vector2.zero : scroll.content.anchoredPosition;
            albumReturnPage = plantAlbumPage; albumReturnSelection = origin == null ? null : origin.name;
            albumReturnPending = true;
        }

        void RestorePlantAlbumPosition()
        {
            BindPlantAlbumNavigation();
            if (!albumReturnPending || albumReturnPage != plantAlbumPage) return;
            var scroll = pageViewport == null ? null : pageViewport.GetComponent<ScrollRect>();
            if (scroll?.content == null) return;
            Canvas.ForceUpdateCanvases();
            var origin = string.IsNullOrEmpty(albumReturnSelection) ? null : FigmaMissionUi.Find<Button>(view.transform, albumReturnSelection);
            if (origin != null) EventSystem.current?.SetSelectedGameObject(origin.gameObject);
            scroll.StopMovement(); scroll.content.anchoredPosition = albumReturnPosition;
            CourseRecordsLayout.ClampScroll(scroll.viewport);
            albumReturnPending = false;
        }

        // Store only navigation values, never a texture, mesh, recipe or wallet.
        // EnterPage sets the next screen key before destroying the previous view,
        // so use the bound individual and actual preview rather than screen here.
        void RememberPlantPhotoView()
        {
            if (!opened || activityFrame == null || activePlantPhotoId == null || albumNavigationOwner != Service) return;
            var preview = FigmaMissionUi.Find<GardenPlantPhotoView>(activityFrame.transform, "PlantAlbum.DetailPreview");
            if (preview == null || !preview.isActiveAndEnabled) return;
            var scroll = FigmaMissionUi.Find<ScrollRect>(activityFrame.transform, "PlantAlbum.RecordsViewport");
            plantPhotoBookmarks[activePlantPhotoId] = new PlantPhotoBookmark { View = preview.CaptureViewState(),
                Records = scroll?.content == null ? Vector2.zero : scroll.content.anchoredPosition };
        }

        void RestorePlantPhotoView(string photoId, GardenPlantPhotoView preview)
        {
            BindPlantAlbumNavigation(); activePlantPhotoId = photoId;
            if (!plantPhotoBookmarks.TryGetValue(photoId, out var saved)) return;
            preview.RestoreViewState(saved.View);
            var label = FigmaMissionUi.Find<Text>(activityFrame.transform, "PlantAlbum.MemoryPageLabel");
            if (label != null) label.text = "기억 잎 " + (preview.MemoryPage + 1) + " / " + preview.MemoryPageCount;
            Canvas.ForceUpdateCanvases();
            var scroll = FigmaMissionUi.Find<ScrollRect>(activityFrame.transform, "PlantAlbum.RecordsViewport");
            if (scroll?.content == null) return;
            scroll.StopMovement(); scroll.content.anchoredPosition = saved.Records;
            CourseRecordsLayout.ClampScroll(scroll.viewport);
        }
    }
}
