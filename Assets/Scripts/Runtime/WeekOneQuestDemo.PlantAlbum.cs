using System;
using System.Collections.Generic;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        // Bound preview textures to one page without limiting the saved collection.
        const int PlantPhotosPerPage = 12;
        const bool InteractivePlantPhotos = true;
        int plantAlbumPage;
        string pendingPlantPhotoRequest, pendingPlantPhotoSource;
        bool plantAlbumSaveFailed;
        string pendingNewPlantRequest, pendingNewPlantSource;
        bool newPlantStartFailed;
        public bool NewPlantStartFailed => opened && IsNewPlantFailure(Service.Snapshot);

        bool IsNewPlantFailure(GardenState state) => newPlantStartFailed && !GardenPlantVisuals.HasCurrentPlant(state)
            && state.PlantAppearance?.Recipe?.InstanceId == pendingNewPlantSource;

        void RefreshNewPlantAttempt(GardenState state)
        {
            if (newPlantStartFailed && !IsNewPlantFailure(state))
            {
                newPlantStartFailed = false;
                error = null;
            }
            if (pendingNewPlantSource != state.PlantAppearance?.Recipe?.InstanceId)
            {
                pendingNewPlantRequest = null;
                pendingNewPlantSource = null;
            }
        }

        bool IsPlantPageCurrent(GameObject page, string key) => opened && screen == key && page != null
            && page.activeInHierarchy && (activityFrame != null ? activityFrame : pageViewport) == page;

        public void ShowPlantAlbum()
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot;
            var current = state.PlantAppearance;
            bool hasCurrent = GardenPlantVisuals.HasCurrentPlant(state);
            RefreshNewPlantAttempt(state);
            if (pendingPlantPhotoSource != current?.Recipe?.InstanceId)
            {
                plantAlbumSaveFailed = false;
                pendingPlantPhotoRequest = null;
                pendingPlantPhotoSource = null;
            }
            var photos = state.PlantPhotos.OrderByDescending(photo => photo.SavedUtc, StringComparer.Ordinal).ToArray();
            int pages = Math.Max(1, (photos.Length + PlantPhotosPerPage - 1) / PlantPhotosPerPage);
            plantAlbumPage = Mathf.Clamp(plantAlbumPage, 0, pages - 1);
            var root = BeginFigmaPage("plant-album", ShowPlantAlbum, FigmaMissionUi.Background, true, "기록");
            var albumFrame = activityFrame;
            root.GetComponent<FocusMobilePageLayout>().FixedContent = true;
            Func<bool> currentPage = () => isActiveAndEnabled && IsPlantPageCurrent(albumFrame, "plant-album")
                && !GardenMemoPopup.IsOpen && !GardenDetailSheet.IsOpen;
            FigmaHeader(root, "식물 사진첩", "");
            SetPlantAlbumNotice();
            var back = FigmaMissionUi.Button(root, "‹ 기록", 24, 0, 100, 38,
                () => { if (currentPage()) ShowFocusCourse(); }, Color.clear, FigmaMissionUi.Green, 15);
            back.name = "PlantAlbum.Back";
            RectTransform currentCard = null;

            if (hasCurrent && current?.Recipe != null)
            {
                string source = current.Recipe.InstanceId;
                var individual = WeeklyPlants.Active(state);
                currentCard = FigmaMissionUi.Box(root, "PlantAlbum.Current", 24, 110, 342, 108, Color.white, 16);
                FigmaMissionUi.Label(currentCard, "지금 정원의 식물", 100, 8, 230, 20, 13, FigmaMissionUi.Muted);
                CreatePlantAlbumPreview(currentCard, "PlantAlbum.CurrentPreview", 12, 8, 76, 92,
                    current.Recipe, individual?.RenderScale ?? GardenPlantVisuals.ScaleForGrowth(GardenPlantVisuals.TotalGrowth(state)),
                    false, current.Memos, PlantAlbumHistory.Leaves(state, source));
                FigmaMissionUi.Label(currentCard, GardenHomePresenter.CurrentPlantName(current.Recipe), 100, 30, 230, 26, 18, null, true)
                    .name = "PlantAlbum.CurrentName";
                Button memories = null;
                memories = FigmaMissionUi.Button(currentCard, "기억 잎 보기", 100, 60, 230, 40, () => {
                    if (!currentPage()) return;
                    RememberPlantAlbumPosition(memories); ShowPlantMemories(source);
                }, FigmaMissionUi.Green, Color.white, 15);
                memories.name = "PlantAlbum.CurrentMemories";
            }
            var count = FigmaMissionUi.Label(root, "간직한 식물 · " + photos.Length + "개", 24, 230, 342, 28, 18, null, true);
            count.name = "PlantAlbum.SavedCount";
            var body = FigmaScroll(root, 266, 480, 1);
            pageViewport.name = "PlantAlbum.Scroll";
            body.name = "PlantAlbum.Grid";
            float y = 6;

            if (photos.Length == 0)
            {
                var empty = FigmaMissionUi.Box(body, "PlantAlbum.Empty", 24, y, 342, 128, FigmaMissionUi.Soft, 20);
                FigmaMissionUi.Label(empty, "아직 저장한 사진이 없어요", 20, 24, 302, 28, 18, FigmaMissionUi.Green, true);
                FigmaMissionUi.Label(empty, "식물을 완성하면 자동으로 담겨요.\n함께한 모습을 여기서 다시 만나요.", 20, 64, 302, 48, 15, FigmaMissionUi.Muted);
                y += 148;
            }
            else
            {
                var visible = photos.Skip(plantAlbumPage * PlantPhotosPerPage).Take(PlantPhotosPerPage).ToArray();
                float rowHeight = 0;
                for (int i = 0; i < visible.Length; i++)
                {
                    var photo = visible[i];
                    string photoId = photo.Id;
                    Button tile = null;
                    tile = FigmaMissionUi.Button(body, "", 24 + (i % 2) * 177, y, 165, 280, () => {
                        if (!currentPage()) return;
                        RememberPlantAlbumPosition(tile); ShowPlantPhoto(photoId);
                    }, Color.white, FigmaMissionUi.Ink);
                    tile.name = "PlantAlbum.Photo." + photoId;
                    CreatePlantAlbumPreview(tile.transform, "PlantAlbum.Thumbnail." + photoId, 12, 12, 141, 152, photo.Recipe, photo.Scale, false, photo.Memos, PlantAlbumHistory.Leaves(state, photo.SourcePlantInstanceId));
                    var name = FigmaMissionUi.Label(tile.transform, photo.DisplayName, 12, 174, 141, 26, 17, null, true);
                    name.name = "PlantAlbum.Name." + photoId;
                    float nameHeight = Mathf.Max(26, Mathf.Ceil(name.preferredHeight));
                    name.rectTransform.sizeDelta = new Vector2(141, nameHeight);
                    var period = FigmaMissionUi.Label(tile.transform, PlantAlbumHistory.Period(photo, Service.CurrentTimeZone),
                        12, 180 + nameHeight, 141, 42, 14, FigmaMissionUi.Muted);
                    period.name = "PlantAlbum.Period." + photoId;
                    float periodHeight = Mathf.Max(42, Mathf.Ceil(period.preferredHeight));
                    period.rectTransform.sizeDelta = new Vector2(141, periodHeight);
                    float tileHeight = 192 + nameHeight + periodHeight;
                    FigmaMissionUi.SetRect((RectTransform)tile.transform, 24 + (i % 2) * 177, y, 165, tileHeight);
                    rowHeight = Mathf.Max(rowHeight, tileHeight);
                    if (i % 2 == 1 || i == visible.Length - 1) { y += rowHeight + 16; rowHeight = 0; }
                }
            }
            RectTransform pager = null;
            if (pages > 1)
            {
                pager = FigmaMissionUi.Rect(root, "PlantAlbum.Pager", 0, 688, 390, 56);
                FigmaMissionUi.Label(pager, (plantAlbumPage + 1) + " / " + pages, 132, 4, 126, 48, 15,
                    FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter).name = "PlantAlbum.PageNumber";
                var previous = FigmaMissionUi.Button(pager, "이전", 24, 4, 100, 48, () => {
                    if (!currentPage() || plantAlbumPage <= 0) return;
                    RememberPlantAlbumPosition(); plantAlbumPage--; ShowPlantAlbum();
                }, Color.clear, FigmaMissionUi.Green);
                previous.name = "PlantAlbum.PreviousPage"; previous.interactable = plantAlbumPage > 0;
                var next = FigmaMissionUi.Button(pager, "다음", 266, 4, 100, 48, () => {
                    if (!currentPage() || plantAlbumPage >= pages - 1) return;
                    RememberPlantAlbumPosition(); plantAlbumPage++; ShowPlantAlbum();
                }, Color.clear, FigmaMissionUi.Green);
                next.name = "PlantAlbum.NextPage"; next.interactable = plantAlbumPage < pages - 1;
            }
            var layout = root.gameObject.AddComponent<GardenPlantAlbumLayout>();
            layout.Current = currentCard; layout.Count = count.rectTransform;
            layout.Viewport = (RectTransform)pageViewport.transform; layout.Content = body; layout.ContentHeight = y + 8;
            layout.Pager = pager; layout.Back = (RectTransform)back.transform;
            layout.Navigation = (RectTransform)root.GetComponentInChildren<MissionBottomNavigation>().transform;
            layout.Notice = notice; layout.Refresh();
            RestorePlantAlbumPosition();
        }

        // Retained entry points for old serialized events. Collection transitions
        // happen only in the atomic growth transaction, never in these view callbacks.
        void SaveCurrentPlantPhoto(string source) { if (opened) ShowPlantAlbum(); }
        public void StartNewGardenPlant(string archivedSource) { if (opened) ShowHome(); }

        void SetPlantAlbumNotice(bool showSaveFailure = true)
        {
            if (notice == null) return;
            notice.gameObject.SetActive(false); notice.text = "";
        }

        public void ShowPlantPhoto(string photoId)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot;
            var photo = state.PlantPhotos.FirstOrDefault(item => item.Id == photoId);
            if (photo == null) { ShowPlantAlbum(); return; }
            var root = BeginFigmaPage("plant-photo:" + photoId, () => ShowPlantPhoto(photoId), FigmaMissionUi.Background, true, "기록");
            var detailFrame = activityFrame;
            root.GetComponent<FocusMobilePageLayout>().FixedContent = true;
            FigmaHeader(root, "식물 사진", "");
            SetPlantAlbumNotice(false);
            Func<bool> current = () => isActiveAndEnabled && IsPlantPageCurrent(detailFrame, "plant-photo:" + photoId)
                && !GardenMemoPopup.IsOpen && !GardenDetailSheet.IsOpen;
            var back = FigmaMissionUi.Button(root, "‹ 사진첩", 24, 0, 110, 38,
                () => { if (current()) ReturnToPlantAlbum(); }, Color.clear, FigmaMissionUi.Green, 15);
            back.name = "PlantAlbum.DetailBack";
            var card = FigmaMissionUi.Box(root, "PlantAlbum.Detail", 24, 110, 342, 360, Color.white, 20);
            var name = FigmaMissionUi.Label(card, photo.DisplayName, 16, 8, 310, 28, 21, null, true, TextAnchor.MiddleCenter);
            name.name = "PlantAlbum.DetailName";
            var period = FigmaMissionUi.Label(card, PlantAlbumHistory.Period(photo, Service.CurrentTimeZone), 16, 40, 310, 24, 15,
                FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter);
            period.name = "PlantAlbum.DetailPeriod";
            var preview = CreatePlantAlbumPreview(card, "PlantAlbum.DetailPreview", 16, 48, 310, 288, photo.Recipe, photo.Scale, InteractivePlantPhotos, photo.Memos, PlantAlbumHistory.Leaves(state, photo.SourcePlantInstanceId));
            preview.MemoClicked += memoId => { if (current()) OpenPlantMemo(photo.SourcePlantInstanceId, memoId, photo.Id, preview.transform); };
            var records = PlantAlbumHistory.Records(state, photo);
            var heading = FigmaMissionUi.Label(root, "기억 잎 기록 · " + records.Length + "개", 24, 472, 342, 28, 18, FigmaMissionUi.Ink, true);
            heading.name = "PlantAlbum.RecordHeading";
            var viewport = FigmaMissionUi.Box(root, "PlantAlbum.RecordsViewport", 24, 500, 342, 200, Color.clear);
            viewport.GetComponent<Image>().raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
            var content = FigmaMissionUi.Rect(viewport, "PlantAlbum.RecordsContent", 0, 0, 342, 200);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.content = content; scroll.viewport = viewport;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            AddPlantRecordScrollbar(viewport, scroll);
            float y = 6;
            if (preview.MemoryPageCount > 1)
            {
                var pageLabel = FigmaMissionUi.Label(content, "기억 잎 1 / " + preview.MemoryPageCount, 88, y, 166, 44,
                    14, FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter);
                pageLabel.name = "PlantAlbum.MemoryPageLabel";
                Action<int> selectPage = delta => {
                    if (!current()) return;
                    preview.SetMemoryPage(preview.MemoryPage + delta);
                    pageLabel.text = "기억 잎 " + (preview.MemoryPage + 1) + " / " + preview.MemoryPageCount;
                };
                FigmaMissionUi.Button(content, "이전 잎", 6, y, 80, 44, () => selectPage(-1), Color.white, FigmaMissionUi.Green, 14)
                    .name = "PlantAlbum.MemoryPrevious";
                FigmaMissionUi.Button(content, "다음 잎", 256, y, 80, 44, () => selectPage(1), Color.white, FigmaMissionUi.Green, 14)
                    .name = "PlantAlbum.MemoryNext";
                y += 54;
            }
            float firstRecordBottom = 0;
            foreach (var record in records)
            {
                string memoId = record.Id;
                y += CreatePlantMemoryRecordRow(content, "PlantAlbum", memoId, record, 6, y, 326, () =>
                {
                    if (current()) OpenPlantMemo(photo.SourcePlantInstanceId, memoId, photo.Id, root);
                }) + 6;
                if (firstRecordBottom == 0) firstRecordBottom = y;
            }
            if (records.Length == 0)
            {
                FigmaMissionUi.Label(content, "이 식물에 연결된 기억 잎 기록이 없어요.", 18, 22, 306, 48, 16, FigmaMissionUi.Muted)
                    .name = "PlantAlbum.NoRecords";
                y = 82;
            }
            var layout = root.gameObject.AddComponent<GardenPlantAlbumDetailLayout>();
            layout.Card = card; layout.Preview = (RectTransform)preview.transform; layout.PlantName = name.rectTransform;
            layout.Period = period.rectTransform; layout.RecordHeading = heading.rectTransform;
            layout.RecordViewport = viewport; layout.RecordContent = content; layout.RecordContentHeight = y;
            // Keep the first record readable together with optional leaf paging
            // on short displays; the 3D preview uses the remaining space.
            layout.MinimumRecordHeight = Mathf.Clamp(firstRecordBottom + 4, 136, 208);
            layout.Back = (RectTransform)back.transform; layout.Navigation = (RectTransform)root.GetComponentInChildren<MissionBottomNavigation>().transform;
            layout.Notice = notice; layout.Refresh();
            RestorePlantPhotoView(photoId, preview);
        }

        float CreatePlantMemoryRecordRow(Transform parent, string prefix, string id, GardenPlantMemo record,
            float x, float y, float width, Action open)
        {
            var row = FigmaMissionUi.Button(parent, "", x, y, width, 98, open, Color.white, FigmaMissionUi.Ink);
            row.name = prefix + ".Record." + id;
            float textWidth = width - 24;
            FigmaMissionUi.Label(row.transform, PlantAlbumHistory.Date(record, Service.CurrentTimeZone),
                12, 6, textWidth - 36, 20, 14, FigmaMissionUi.Ink, true).name = prefix + ".RecordDate." + id;
            FigmaMissionUi.Label(row.transform, "보기", width - 46, 6, 34, 20, 12, FigmaMissionUi.Muted,
                false, TextAnchor.MiddleRight).name = prefix + ".RecordOpen." + id;
            string mood = string.IsNullOrWhiteSpace(record.Mood) ? "메모를 남긴 날" : record.Mood;
            if (mood.Length > 36) mood = mood.Substring(0, 36) + "…";
            var summary = FigmaMissionUi.Label(row.transform, mood, 12, 28, textWidth, 22, 16, FigmaMissionUi.Green, true);
            summary.name = prefix + ".RecordMood." + id;
            float moodHeight = Mathf.Max(22, summary.preferredHeight);
            summary.rectTransform.sizeDelta = new Vector2(textWidth, moodHeight);
            var basic = FigmaMissionUi.Label(row.transform, PlantAlbumHistory.MissionLine(record, true),
                12, 32 + moodHeight, textWidth, 18, 13, FigmaMissionUi.Muted);
            basic.name = prefix + ".RecordBasic." + id;
            float basicHeight = Mathf.Max(18, basic.preferredHeight);
            basic.rectTransform.sizeDelta = new Vector2(textWidth, basicHeight);
            var additional = FigmaMissionUi.Label(row.transform, PlantAlbumHistory.MissionLine(record, false),
                12, 34 + moodHeight + basicHeight, textWidth, 18, 13, FigmaMissionUi.Muted);
            additional.name = prefix + ".RecordAdditional." + id;
            float additionalHeight = Mathf.Max(18, additional.preferredHeight);
            additional.rectTransform.sizeDelta = new Vector2(textWidth, additionalHeight);
            float height = 42 + moodHeight + basicHeight + additionalHeight;
            FigmaMissionUi.SetRect((RectTransform)row.transform, x, y, width, height);
            return height;
        }

        static void AddPlantRecordScrollbar(RectTransform viewport, ScrollRect scroll)
        {
            var track = FigmaMissionUi.Box(viewport, "PlantAlbum.RecordScrollbar", 336, 4, 4, 192, FigmaMissionUi.Border, 2);
            track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one; track.offsetMin = new Vector2(-6, 4); track.offsetMax = new Vector2(-2, -4);
            var handle = FigmaMissionUi.Box(track, "Handle", 0, 0, 4, 192, FigmaMissionUi.Muted, 2);
            handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.offsetMin = handle.offsetMax = Vector2.zero;
            var bar = track.gameObject.AddComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle; bar.targetGraphic = handle.GetComponent<Image>(); bar.navigation = new Navigation { mode = Navigation.Mode.None };
            track.GetComponent<Image>().raycastTarget = handle.GetComponent<Image>().raycastTarget = true;
            bar.SetValueWithoutNotify(1); scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        void ReturnToPlantAlbum()
        {
            if (pageHistory.Count > 1 && pageHistory[pageHistory.Count - 2].Key == "plant-album") TryNavigateBack();
            else ShowPlantAlbum();
        }

        static GardenPlantPhotoView CreatePlantAlbumPreview(Transform parent, string name, float x, float y, float width, float height,
            PlantRecipe recipe, float scale, bool interactive, IReadOnlyList<GardenPlantMemo> memos = null,
            IReadOnlyList<PlantMemoryLeaf> memories = null, int memoryPage = 0)
        {
            var surface = FigmaMissionUi.Rect(parent, name, x, y, width, height);
            surface.gameObject.AddComponent<RawImage>();
            var preview = surface.gameObject.AddComponent<GardenPlantPhotoView>();
            preview.Initialize(recipe, scale, interactive, memos, memories, memoryPage);
            return preview;
        }

        string PlantPhotoDate(string utc)
        {
            if (!DateTimeOffset.TryParse(utc, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var saved)) return "저장된 식물";
            return TimeZoneInfo.ConvertTime(saved, Service.CurrentTimeZone).ToString("yyyy.MM.dd");
        }
    }
}
