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
            FigmaHeader(root, "식물 사진첩", "함께한 식물의 모습을 간직해요.");
            SetPlantAlbumNotice();
            var body = FigmaScroll(root, 132, 628, 800);
            float y = 8;

            if (hasCurrent && current?.Recipe != null)
            {
                string source = current.Recipe.InstanceId;
                bool saved = photos.Any(photo => photo.SourcePlantInstanceId == source);
                bool completed = GardenPlantVisuals.IsCompleted(state);
                bool compact = photos.Length > 0;
                var card = FigmaMissionUi.Box(body, "PlantAlbum.Current", 24, y, 342, compact ? 144 : 310, Color.white, 20);
                FigmaMissionUi.Label(card, "지금 정원의 식물", compact ? 12 : 20, compact ? 8 : 16,
                    compact ? 318 : 302, compact ? 20 : 28, compact ? 13 : 18, null, true);
                CreatePlantAlbumPreview(card, "PlantAlbum.CurrentPreview", compact ? 12 : 16, compact ? 36 : 56,
                    compact ? 80 : 140, compact ? 96 : 152,
                    current.Recipe, GardenPlantVisuals.ScaleForGrowth(GardenPlantVisuals.TotalGrowth(state)), false, current.Memos);
                FigmaMissionUi.Label(card, GardenHomePresenter.CurrentPlantName(current.Recipe), compact ? 104 : 172,
                    compact ? 30 : 64, compact ? 222 : 150, compact ? 26 : 52, compact ? 18 : 19, null, true);
                FigmaMissionUi.Label(card, saved ? "사진첩에 보관했어요" : completed ? "사진으로 남겨 자연으로 보내요" : "아직 자라는 중이에요",
                    compact ? 104 : 172, compact ? 60 : 124, compact ? 222 : 150, compact ? 20 : 48,
                    compact ? 13 : 15, FigmaMissionUi.Green);
                if (!compact)
                {
                    string description = saved ? "함께한 모습은 사진첩에 남아 있어요." : completed
                        ? "사진을 남기고 자연으로 보내요." : "완성하면 사진첩에 남길 수 있어요.";
                    FigmaMissionUi.Label(card, description, 20, 210, 302, 28, 14, FigmaMissionUi.Muted);
                }
                var save = FigmaMissionUi.Button(card, saved ? "사진첩에 저장됨" : "사진첩에 담아 보내기", compact ? 104 : 20,
                    compact ? 88 : 246, compact ? 222 : 302, 48,
                    () => { if (IsPlantPageCurrent(albumFrame, "plant-album")) SaveCurrentPlantPhoto(source); }, saved || !completed ? FigmaMissionUi.Soft : FigmaMissionUi.Green,
                    saved || !completed ? FigmaMissionUi.Muted : Color.white);
                save.name = "PlantAlbum.SaveCurrent";
                save.interactable = completed && !saved;
                y += compact ? 160 : 330;
            }
            else if (current?.Recipe != null)
            {
                string archivedSource = current.Recipe.InstanceId;
                var card = FigmaMissionUi.Box(body, "PlantAlbum.EmptyGarden", 24, y, 342, 144, Color.white, 20);
                FigmaMissionUi.Label(card, "자연으로 돌려보냈어요", 20, 12, 302, 28, 19, null, true);
                FigmaMissionUi.Label(card, state.StoredGrowthSteps > 0 ? "보관한 성장 " + state.StoredGrowthSteps + "회 · 새 식물에 반영해요."
                    : "함께한 모습은 사진첩에서 다시 만나요.", 20, 48, 302, 24, 14, FigmaMissionUi.Muted).name = "PlantAlbum.StoredGrowth";
                FigmaMissionUi.Button(card, "새 식물 키우기", 20, 88, 302, 48,
                    () => { if (IsPlantPageCurrent(albumFrame, "plant-album")) StartNewGardenPlant(archivedSource); },
                    FigmaMissionUi.Green, Color.white).name = "PlantAlbum.StartNew";
                y += 160;
            }
            FigmaMissionUi.Label(body, "간직한 식물 · " + photos.Length + "개", 24, y, 342, 28, 18, null, true);
            y += 44;

            if (photos.Length == 0)
            {
                var empty = FigmaMissionUi.Box(body, "PlantAlbum.Empty", 24, y, 342, 128, FigmaMissionUi.Soft, 20);
                FigmaMissionUi.Label(empty, "아직 저장한 사진이 없어요", 20, 24, 302, 28, 18, FigmaMissionUi.Green, true);
                FigmaMissionUi.Label(empty, "완성한 식물을 저장하면\n이곳에서 다시 볼 수 있어요.", 20, 64, 302, 48, 15, FigmaMissionUi.Muted);
                y += 148;
            }
            else
            {
                var visible = photos.Skip(plantAlbumPage * PlantPhotosPerPage).Take(PlantPhotosPerPage).ToArray();
                for (int i = 0; i < visible.Length; i++)
                {
                    var photo = visible[i];
                    string photoId = photo.Id;
                    var tile = FigmaMissionUi.Button(body, "", 24 + (i % 2) * 177, y + (i / 2) * 288, 165, 272,
                        () => { if (IsPlantPageCurrent(albumFrame, "plant-album")) ShowPlantPhoto(photoId); }, Color.white, FigmaMissionUi.Ink);
                    tile.name = "PlantAlbum.Photo." + photoId;
                    CreatePlantAlbumPreview(tile.transform, "PlantAlbum.Thumbnail." + photoId, 12, 12, 141, 152, photo.Recipe, photo.Scale, false, photo.Memos);
                    FigmaMissionUi.Label(tile.transform, photo.DisplayName, 12, 174, 141, 42, 16, null, true);
                    FigmaMissionUi.Label(tile.transform, PlantAlbumHistory.Period(photo, Service.CurrentTimeZone), 12, 218, 141, 42, 12, FigmaMissionUi.Muted)
                        .name = "PlantAlbum.Period." + photoId;
                }
                y += ((visible.Length + 1) / 2) * 288;
                if (pages > 1)
                {
                    FigmaMissionUi.Label(body, (plantAlbumPage + 1) + " / " + pages, 24, y, 342, 28, 14, FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter);
                    var previous = FigmaMissionUi.Button(body, "이전", 24, y + 36, 165, 48,
                        () => { plantAlbumPage--; ShowPlantAlbum(); }, Color.white, FigmaMissionUi.Green);
                    previous.name = "PlantAlbum.PreviousPage"; previous.interactable = plantAlbumPage > 0;
                    var next = FigmaMissionUi.Button(body, "다음", 201, y + 36, 165, 48,
                        () => { plantAlbumPage++; ShowPlantAlbum(); }, Color.white, FigmaMissionUi.Green);
                    next.name = "PlantAlbum.NextPage"; next.interactable = plantAlbumPage < pages - 1;
                    y += 104;
                }
            }
            FigmaMissionUi.Button(body, "기록으로 돌아가기", 24, y, 342, 48, ShowFocusCourse, Color.clear, FigmaMissionUi.Green).name = "PlantAlbum.Back";
            body.sizeDelta = new Vector2(390, Mathf.Max(628, y + 72));
        }

        void SaveCurrentPlantPhoto(string source)
        {
            if (!opened || screen != "plant-album") return;
            var current = Service.Snapshot;
            if (!GardenPlantVisuals.HasCurrentPlant(current) || current.PlantAppearance.Recipe.InstanceId != source) return;
            // A failed write retains the same command, including after leaving
            // and returning. The domain also guards duplicates by source identity.
            if (pendingPlantPhotoSource != source || string.IsNullOrEmpty(pendingPlantPhotoRequest))
            {
                pendingPlantPhotoSource = source;
                pendingPlantPhotoRequest = Guid.NewGuid().ToString("N");
            }
            if (!Service.SavePlantPhoto(pendingPlantPhotoRequest, source))
            {
                plantAlbumSaveFailed = true;
                SetPlantAlbumNotice();
                return;
            }
            error = null;
            plantAlbumSaveFailed = false;
            // Retain the successful request too; the archived/source guard above
            // makes queued clicks harmless after this plant has left the garden.
            plantAlbumPage = 0;
            UpdateGarden();
            ShowPlantAlbum();
        }

        public void StartNewGardenPlant(string archivedSource)
        {
            if (!opened) return;
            var current = Service.Snapshot;
            // Each button belongs to one empty planter. A delayed event from an
            // earlier plant must never replace its successor or start a new cycle.
            if (GardenPlantVisuals.HasCurrentPlant(current) || current.PlantAppearance?.Recipe?.InstanceId != archivedSource) return;
            if (pendingNewPlantSource != archivedSource || string.IsNullOrEmpty(pendingNewPlantRequest))
            {
                pendingNewPlantSource = archivedSource;
                pendingNewPlantRequest = Guid.NewGuid().ToString("N");
            }
            if (!Service.StartNewPlant(pendingNewPlantRequest, archivedSource))
            {
                error = Service.LastError;
                newPlantStartFailed = true;
                if (notice != null)
                {
                    notice.gameObject.SetActive(true);
                    notice.text = "새 식물을 심지 못했어요.\n사진첩은 그대로예요. 다시 시도해 주세요.";
                }
                transform.root.GetComponent<GardenHomePresenter>()?.RefreshFromState();
                return;
            }
            error = null;
            newPlantStartFailed = false;
            plantAlbumSaveFailed = false;
            GoGarden();
            if (transform.root.GetComponent<MockupNavigation>() == null) ShowPlantAlbum();
        }

        void SetPlantAlbumNotice(bool showSaveFailure = true)
        {
            if (notice == null) return;
            bool failed = showSaveFailure && (plantAlbumSaveFailed || newPlantStartFailed);
            notice.gameObject.SetActive(failed);
            notice.text = !failed ? "" : newPlantStartFailed
                ? "새 식물을 심지 못했어요.\n사진첩은 그대로예요. 다시 시도해 주세요."
                : "사진을 남기지 못했어요.\n식물은 정원에 있어요. 다시 시도해 주세요.";
            notice.transform.SetAsLastSibling();
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
            FigmaHeader(root, "식물 사진", "함께한 기간과 기록 · 앱 날짜 기준");
            SetPlantAlbumNotice(false);
            Func<bool> current = () => isActiveAndEnabled && IsPlantPageCurrent(detailFrame, "plant-photo:" + photoId)
                && !GardenMemoPopup.IsOpen;
            var card = FigmaMissionUi.Box(root, "PlantAlbum.Detail", 24, 132, 342, 288, Color.white, 20);
            var name = FigmaMissionUi.Label(card, photo.DisplayName, 16, 2, 310, 24, 19, null, true);
            name.name = "PlantAlbum.DetailName";
            var period = FigmaMissionUi.Label(card, PlantAlbumHistory.Period(photo, Service.CurrentTimeZone), 16, 26, 310, 20, 14, FigmaMissionUi.Muted);
            period.name = "PlantAlbum.DetailPeriod";
            var preview = CreatePlantAlbumPreview(card, "PlantAlbum.DetailPreview", 16, 48, 310, 288, photo.Recipe, photo.Scale, InteractivePlantPhotos, photo.Memos);
            preview.MemoClicked += memoId => OpenPlantMemo(photo.SourcePlantInstanceId, memoId, photo.Id, preview.transform);
            var records = PlantAlbumHistory.Records(state, photo);
            var heading = FigmaMissionUi.Label(root, "그날의 기록 · " + records.Length + "개", 24, 472, 342, 24, 16, FigmaMissionUi.Ink, true);
            heading.name = "PlantAlbum.RecordHeading";
            var viewport = FigmaMissionUi.Box(root, "PlantAlbum.RecordsViewport", 24, 500, 342, 200, FigmaMissionUi.Soft, 14);
            viewport.GetComponent<Image>().raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
            var content = FigmaMissionUi.Rect(viewport, "PlantAlbum.RecordsContent", 0, 0, 342, 200);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.content = content; scroll.viewport = viewport;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            AddPlantRecordScrollbar(viewport, scroll);
            float y = 6;
            foreach (var record in records)
            {
                string memoId = record.Id;
                var row = FigmaMissionUi.Button(content, "", 6, y, 326, 98, () =>
                {
                    if (current()) OpenPlantMemo(photo.SourcePlantInstanceId, memoId, photo.Id, root);
                }, Color.white, FigmaMissionUi.Ink);
                row.name = "PlantAlbum.Record." + memoId;
                FigmaMissionUi.Label(row.transform, PlantAlbumHistory.Date(record, Service.CurrentTimeZone), 12, 6, 302, 20, 14, FigmaMissionUi.Ink, true)
                    .name = "PlantAlbum.RecordDate." + memoId;
                string mood = string.IsNullOrWhiteSpace(record.Mood) ? "메모를 남긴 날" : record.Mood;
                if (mood.Length > 36) mood = mood.Substring(0, 36) + "…";
                var summary = FigmaMissionUi.Label(row.transform, mood, 12, 28, 302, 22, 16, FigmaMissionUi.Green, true);
                summary.name = "PlantAlbum.RecordMood." + memoId;
                float moodHeight = Mathf.Max(22, summary.preferredHeight);
                summary.rectTransform.sizeDelta = new Vector2(302, moodHeight);
                var basic = FigmaMissionUi.Label(row.transform, PlantAlbumHistory.MissionLine(record, true), 12, 32 + moodHeight, 302, 18, 13, FigmaMissionUi.Muted);
                basic.name = "PlantAlbum.RecordBasic." + memoId;
                float basicHeight = Mathf.Max(18, basic.preferredHeight);
                basic.rectTransform.sizeDelta = new Vector2(302, basicHeight);
                var additional = FigmaMissionUi.Label(row.transform, PlantAlbumHistory.MissionLine(record, false), 12, 34 + moodHeight + basicHeight, 302, 18, 13, FigmaMissionUi.Muted);
                additional.name = "PlantAlbum.RecordAdditional." + memoId;
                float additionalHeight = Mathf.Max(18, additional.preferredHeight);
                additional.rectTransform.sizeDelta = new Vector2(302, additionalHeight);
                float height = 42 + moodHeight + basicHeight + additionalHeight;
                FigmaMissionUi.SetRect((RectTransform)row.transform, 6, y, 326, height);
                y += height + 6;
            }
            if (records.Length == 0)
            {
                FigmaMissionUi.Label(content, "이 식물에 남긴 감정 기록이 없어요.", 18, 22, 306, 48, 16, FigmaMissionUi.Muted)
                    .name = "PlantAlbum.NoRecords";
                y = 82;
            }
            var back = FigmaMissionUi.Button(root, "사진첩으로 돌아가기", 24, 712, 342, 48,
                () => { if (current()) ReturnToPlantAlbum(); }, Color.clear, FigmaMissionUi.Green);
            back.name = "PlantAlbum.DetailBack";
            var layout = root.gameObject.AddComponent<GardenPlantAlbumDetailLayout>();
            layout.Card = card; layout.Preview = (RectTransform)preview.transform; layout.PlantName = name.rectTransform;
            layout.Period = period.rectTransform; layout.RecordHeading = heading.rectTransform;
            layout.RecordViewport = viewport; layout.RecordContent = content; layout.RecordContentHeight = y;
            layout.Back = (RectTransform)back.transform; layout.Navigation = (RectTransform)root.GetComponentInChildren<MissionBottomNavigation>().transform;
            layout.Notice = notice; layout.Refresh();
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
            PlantRecipe recipe, float scale, bool interactive, IReadOnlyList<GardenPlantMemo> memos = null)
        {
            var surface = FigmaMissionUi.Rect(parent, name, x, y, width, height);
            surface.gameObject.AddComponent<RawImage>();
            var preview = surface.gameObject.AddComponent<GardenPlantPhotoView>();
            preview.Initialize(recipe, scale, interactive, memos);
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
