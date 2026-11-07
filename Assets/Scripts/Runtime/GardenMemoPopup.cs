using System;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // View-owned memory. Opening never saves; editing/deletion are explicit actions.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GardenMemoPopup : MonoBehaviour
    {
        static GardenMemoPopup active;
        Transform source;
        GameObject previousSelection;
        RectTransform design, card, viewport, content, closeRect, actionsRect;
        Text moodText, noteText, basicMissionText, additionalMissionText;
        float lastHeight = -1;
        public static bool IsOpen => active != null && active.isActiveAndEnabled;
        public string MemoId { get; private set; }

        public static GardenMemoPopup Show(Transform owner, GardenPlantMemo memo, string date)
        {
            if (owner == null || !owner.gameObject.activeInHierarchy || memo == null) return null;
            var canvas = owner.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            TryCloseOpen();
            var root = new GameObject("GardenMemoPopup", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(canvas.rootCanvas.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var popup = root.AddComponent<GardenMemoPopup>();
            popup.source = owner; popup.MemoId = memo.Id;
            popup.previousSelection = EventSystem.current?.currentSelectedGameObject;
            active = popup;
            popup.Build(memo.Copy(), date);
            return popup;
        }

        public void ConfigureActions(Action edit, Action delete)
        {
            if (actionsRect != null || (edit == null && delete == null)) return;
            actionsRect = FigmaMissionUi.Rect(card, "GardenMemoPopup.Actions", 24, 432, 294, 48);
            if (edit != null)
                FigmaMissionUi.Button(actionsRect, "수정", 0, 0, 143, 48, () => {
                    if (active != this || !IsOpen) return;
                    Close(); edit();
                }, Color.white, FigmaMissionUi.Green).name = "GardenMemoPopup.Edit";
            if (delete != null)
                FigmaMissionUi.Button(actionsRect, "삭제", 151, 0, 143, 48, () => {
                    if (active != this || !IsOpen) return;
                    Close(); delete();
                }, Color.white, FigmaMissionUi.Brown).name = "GardenMemoPopup.Delete";
            Layout();
        }

        void Build(GardenPlantMemo memo, string date)
        {
            var shade = new GameObject("GardenMemoPopup.Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            var shadeRect = (RectTransform)shade.transform; shadeRect.SetParent(transform, false);
            shadeRect.anchorMin = Vector2.zero; shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
            var shadeImage = shade.GetComponent<Image>(); shadeImage.color = new Color(.06f, .13f, .09f, .62f);
            var backdrop = shade.GetComponent<Button>(); backdrop.targetGraphic = shadeImage;
            backdrop.transition = Selectable.Transition.None;
            backdrop.navigation = new Navigation { mode = Navigation.Mode.None };
            backdrop.onClick.AddListener(Close);

            design = FigmaMissionUi.Rect(transform, "GardenMemoPopup.Design", 0, 0, 390, 844);
            design.gameObject.AddComponent<FigmaGardenThemeScope>();
            var fit = gameObject.AddComponent<FigmaMissionCanvasFit>(); fit.Design = design; fit.Fit();
            card = FigmaMissionUi.Box(design, "GardenMemoPopup.Card", 24, 100, 342, 560, new Color(1f, .98f, .89f), 20);
            card.GetComponent<Image>().raycastTarget = true;
            var titlePlaque=FigmaMissionUi.Box(card,"GardenMemoPopup.TitlePlaque",12,14,318,50,FigmaMissionUi.Warm,10);
            CapstoneDesign.Prototype.GardenThemeSurface.Apply(titlePlaque.gameObject,CapstoneDesign.Prototype.GardenThemeSurface.Style.Header);
            FigmaMissionUi.Label(card, "감정 기록", 24, 22, 294, 34, 24, CapstoneDesign.Prototype.GardenThemeSurface.WoodInk, true).name = "GardenMemoPopup.Title";
            FigmaMissionUi.Label(card, date, 24, 62, 294, 24, 14, FigmaMissionUi.Muted).name = "GardenMemoPopup.Date";
            viewport = FigmaMissionUi.Box(card, "GardenMemoPopup.Viewport", 24, 108, 294, 360, Color.clear);
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            content = FigmaMissionUi.Rect(viewport, "GardenMemoPopup.Content", 0, 0, 278, 360);
            FigmaMissionUi.Label(content, "그때의 감정", 0, 0, 278, 24, 14, FigmaMissionUi.Muted);
            moodText = FigmaMissionUi.Label(content, string.IsNullOrWhiteSpace(memo.Mood) ? "선택한 감정이 없어요." : memo.Mood,
                0, 32, 278, 34, 21, FigmaMissionUi.Green, true);
            moodText.name = "GardenMemoPopup.Mood";
            FigmaMissionUi.Label(content, "남긴 메모", 0, 90, 278, 24, 14, FigmaMissionUi.Muted).name = "GardenMemoPopup.NoteHeading";
            noteText = FigmaMissionUi.Label(content, string.IsNullOrWhiteSpace(memo.Note) ? "남긴 메모가 없어요." : memo.Note,
                0, 122, 278, 100, 17, FigmaMissionUi.Ink);
            noteText.name = "GardenMemoPopup.Note";
            basicMissionText = FigmaMissionUi.Label(content, PlantAlbumHistory.MissionLine(memo, true), 0, 240, 278, 22, 14, FigmaMissionUi.Muted);
            basicMissionText.name = "GardenMemoPopup.BasicMission";
            additionalMissionText = FigmaMissionUi.Label(content, PlantAlbumHistory.MissionLine(memo, false), 0, 268, 278, 22, 14, FigmaMissionUi.Muted);
            additionalMissionText.name = "GardenMemoPopup.AdditionalMission";
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.name = "GardenMemoPopup.Scroll";
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            var track = FigmaMissionUi.Box(viewport, "GardenMemoPopup.Scrollbar", 288, 0, 4, 360, FigmaMissionUi.Border, 2);
            track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one;
            track.offsetMin = new Vector2(-4, 0); track.offsetMax = Vector2.zero;
            var handle = FigmaMissionUi.Box(track, "Handle", 0, 0, 4, 360, FigmaMissionUi.Muted, 2);
            handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.offsetMin = handle.offsetMax = Vector2.zero;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            track.GetComponent<Image>().raycastTarget = handle.GetComponent<Image>().raycastTarget = true;
            scrollbar.SetValueWithoutNotify(1);
            scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            var close = FigmaMissionUi.Button(card, "닫기", 24, 488, 294, 48, Close);
            close.name = "GardenMemoPopup.Close"; closeRect = (RectTransform)close.transform;
            close.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = close, selectOnDown = close, selectOnLeft = close, selectOnRight = close };
            Layout();
            EventSystem.current?.SetSelectedGameObject(close.gameObject);
        }

        public void Layout()
        {
            if (design == null || noteText == null) return;
            GetComponent<FigmaMissionCanvasFit>().Fit();
            float moodHeight = Mathf.Max(34, moodText.preferredHeight);
            moodText.rectTransform.sizeDelta = new Vector2(278, moodHeight);
            var heading = content.Find("GardenMemoPopup.NoteHeading") as RectTransform;
            FigmaMissionUi.SetRect(heading, 0, 32 + moodHeight + 24, 278, 24);
            float noteY = 32 + moodHeight + 56;
            float noteHeight = Mathf.Max(28, noteText.preferredHeight);
            FigmaMissionUi.SetRect(noteText.rectTransform, 0, noteY, 278, noteHeight);
            float missionY = noteY + noteHeight + 24;
            float basicHeight = Mathf.Max(22, basicMissionText.preferredHeight);
            float additionalHeight = Mathf.Max(22, additionalMissionText.preferredHeight);
            FigmaMissionUi.SetRect(basicMissionText.rectTransform, 0, missionY, 278, basicHeight);
            FigmaMissionUi.SetRect(additionalMissionText.rectTransform, 0, missionY + basicHeight + 6, 278, additionalHeight);
            float bodyHeight = missionY + basicHeight + 6 + additionalHeight;
            float maximum = Mathf.Min(560, Mathf.Max(240, design.rect.height - 48));
            float actionHeight = actionsRect == null ? 0 : 56;
            float height = Mathf.Min(maximum, Mathf.Max(320 + actionHeight, bodyHeight + 204 + actionHeight));
            FigmaMissionUi.SetRect(card, 24, Mathf.Max(24, (design.rect.height - height) * .5f), 342, height);
            viewport.sizeDelta = new Vector2(294, Mathf.Max(64, height - 192 - actionHeight));
            if (actionsRect != null) FigmaMissionUi.SetRect(actionsRect, 24, height - 128, 294, 48);
            FigmaMissionUi.SetRect(closeRect, 24, height - 72, 294, 48);
            content.sizeDelta = new Vector2(278, Mathf.Max(viewport.rect.height, bodyHeight + 12));
            lastHeight = design.rect.height;
        }

        void Update()
        {
            if (source == null || !source.gameObject.activeInHierarchy) { Close(); return; }
            if (design != null && !Mathf.Approximately(lastHeight, design.rect.height)) Layout();
        }
        public static bool TryCloseOpen()
        {
            if (!IsOpen) return false;
            active.Close(); return true;
        }
        public void Close()
        {
            if (this == null) return;
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
        void OnDisable()
        {
            if (active != this) return;
            active = null;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        }
    }
}
