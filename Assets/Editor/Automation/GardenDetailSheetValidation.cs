using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // View-only synthetic fixtures: no state store, PlayerPrefs writes, scene
    // replacement or physical input. The caller owns the report and GPU policy.
    public static class GardenDetailSheetValidation
    {
        public static void Run(FocusMissionValidation.Report report, Action<bool, string> check, bool capture)
        {
            foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844), new Vector2Int(1200, 2000) })
            using (var f = new Fixture(size, report, capture))
            {
                string at = "Shared detail " + size + ": ";
                f.SelectOrigin();
                var outerBefore = f.Outer.content.anchoredPosition;
                var innerBefore = f.Inner.content.anchoredPosition;
                int closed = 0, requested = 0;
                GardenDetailSheet sheet = null;
                sheet = GardenDetailSheet.Create(f.Frame, "Synthetic.Detail", "기억과 실천을 함께 돌아보는 긴 기록 제목",
                    () => { requested++; sheet.Close(); }, f.Origin, f.Outer, f.Group);
                sheet.Closed += () => closed++;
                sheet.FooterHeight = 164;
                var preview = FigmaMissionUi.Box(sheet.Content, "Synthetic product preview", 16, 16, 334, 209, Color.gray);
                var purchase = FigmaMissionUi.Button(sheet.Footer, "구매하기", 16, 84, 334, 48, () => { });
                sheet.Content.sizeDelta = new Vector2(366, 1400);
                f.Layout();

                check(!f.Background.IsInteractable() && !f.Origin.IsInteractable(), at + "underlying controls reject keyboard submit.");
                check(sheet.CloseButton.IsInteractable() && purchase.IsInteractable(), at + "modal actions remain enabled above the parent input lock.");
                check(!f.Outer.enabled && !f.Inner.enabled && !f.DisabledScroll.enabled, at + "all underlying scrolling is paused.");
                check(f.Events.currentSelectedGameObject == sheet.CloseButton.gameObject, at + "focus starts on the visible close action.");
                ExecuteEvents.Execute(f.Background.gameObject, new BaseEventData(f.Events), ExecuteEvents.submitHandler);
                check(f.BackgroundClicks == 0, at + "a retained underlying submit cannot activate a background button.");
                check(f.Frame.GetComponentsInChildren<Selectable>(false).Where(control => control.IsInteractable())
                    .All(control => control.transform.IsChildOf(sheet.transform)), at + "keyboard traversal contains only modal controls.");
                if (capture)
                {
                    f.CheckCovered(f.Background, sheet.transform, check, at);
                    f.CheckFirstHit(sheet.CloseButton, check, at);
                    f.CheckFirstHit(purchase, check, at);
                }

                check(Mathf.Abs(f.Frame.rect.width - 390) < .01f && Mathf.Abs(sheet.Panel.rect.width - 366) < .01f,
                    at + "physical resizing preserves the shared 390-unit authoring frame.");
                check(Contains(f.Screen, f.Bounds(sheet.Panel)), at + "the entire sheet fits the available canvas.");
                check(Contains(f.Bounds(sheet.Viewport), f.Bounds(preview)), at + "the 334-unit product image fits its responsive viewport.");
                check(Contains(f.Bounds(sheet.Footer), f.Bounds((RectTransform)purchase.transform)), at + "fixed purchase action stays inside its footer.");
                check(f.Bounds(sheet.Heading.rectTransform).yMin >= f.Bounds(sheet.Viewport).yMax - .5f,
                    at + "wrapped title stays above the scroll body.");
                check(f.Bounds(sheet.Viewport).yMin >= f.Bounds(sheet.Footer).yMax - .5f,
                    at + "scroll body never extends beneath the footer.");
                var footerBefore = f.Bounds(sheet.Footer);
                sheet.Content.anchoredPosition = new Vector2(0, 250); f.Layout();
                check(f.Bounds(sheet.Footer) == footerBefore, at + "scrolling long content cannot move purchase actions.");
                check(f.Outer.content.anchoredPosition == outerBefore && f.Inner.content.anchoredPosition == innerBefore,
                    at + "modal scrolling leaves both underlying scroll positions intact.");

                sheet.CloseButton.onClick.Invoke(); f.Layout();
                check(requested == 1 && closed == 1 && !GardenDetailSheet.IsOpen, at + "close dispatches its owner request and cleanup once.");
                check(f.Group.interactable && f.Group.blocksRaycasts && f.Outer.enabled && f.Inner.enabled && !f.DisabledScroll.enabled,
                    at + "close restores previous input and enabled flags, including an already-disabled scroll.");
                check(f.Events.currentSelectedGameObject == f.Origin.gameObject, at + "close restores the originating record selection.");
                check(f.Outer.content.anchoredPosition == outerBefore && f.Inner.content.anchoredPosition == innerBefore,
                    at + "focus reveal cannot change either saved nested scroll offset.");

                var hidden = GardenDetailSheet.Create(f.Frame, "Synthetic.Hidden", "기록", null, f.Origin);
                int hiddenClosed = 0; hidden.Closed += () => hiddenClosed++;
                f.Frame.gameObject.SetActive(false); f.Frame.gameObject.SetActive(true); f.Layout();
                check(hiddenClosed == 1 && !GardenDetailSheet.IsOpen && !hidden.gameObject.activeSelf,
                    at + "leaving and returning to the owner never resurrects an unlocked old dialog.");
                check(f.Group.interactable && f.Group.blocksRaycasts && f.Outer.enabled && f.Inner.enabled,
                    at + "external owner hiding releases background input.");
                UnityEngine.Object.DestroyImmediate(hidden.gameObject);
                check(hiddenClosed == 1, at + "later destruction does not repeat close cleanup.");

                // Existing disabled state belongs to the caller, not the modal.
                f.Group.interactable = false; f.Group.blocksRaycasts = false;
                var alreadyDisabled = GardenDetailSheet.Create(f.Frame, "Synthetic.Disabled", "기록", null);
                check(alreadyDisabled.CloseButton.IsInteractable(), at + "modal works over a pre-disabled owner.");
                alreadyDisabled.Close(false);
                check(!f.Group.interactable && !f.Group.blocksRaycasts, at + "closing preserves a pre-disabled owner's exact flags.");
                f.Group.interactable = true; f.Group.blocksRaycasts = true;

                f.SelectOrigin(); outerBefore = f.Outer.content.anchoredPosition; innerBefore = f.Inner.content.anchoredPosition;
                var memo = GardenMemoPopup.Show(f.Frame, new GardenPlantMemo { Id = "synthetic-leaf", Mood = "편안함", Note = "Synthetic memory only." }, "2026. 11. 12");
                f.Layout();
                check(memo != null && !f.Origin.IsInteractable() && !f.Outer.enabled && !f.Inner.enabled,
                    at + "memory leaf popup uses the same background interaction lock.");
                if (capture) f.CheckCovered(f.Background, memo.transform, check, at);
                memo.Close(); f.Layout();
                check(!GardenMemoPopup.IsOpen && f.Events.currentSelectedGameObject == f.Origin.gameObject,
                    at + "memory popup returns focus to its selected record.");
                check(f.Outer.content.anchoredPosition == outerBefore && f.Inner.content.anchoredPosition == innerBefore,
                    at + "memory popup close preserves both nested list positions after focus restoration.");
                check(f.Origin.IsInteractable() && f.Outer.enabled && f.Inner.enabled,
                    at + "memory popup releases its underlying inputs.");
            }
        }

        static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - .5f && inner.xMax <= outer.xMax + .5f
            && inner.yMin >= outer.yMin - .5f && inner.yMax <= outer.yMax + .5f;

        sealed class Fixture : IDisposable
        {
            public readonly RectTransform Frame;
            public readonly CanvasGroup Group;
            public readonly Button Background, Origin;
            public readonly ScrollRect Outer, Inner, DisabledScroll;
            public readonly EventSystem Events;
            public int BackgroundClicks;
            public Rect Screen => new Rect(0, 0, size.x, size.y);
            readonly GameObject root;
            readonly Scene scene;
            readonly Camera camera;
            readonly RenderTexture target;
            readonly GraphicRaycaster raycaster;
            readonly EventSystem previousEvents;
            readonly FigmaMissionCanvasFit fit;
            readonly FocusMissionValidation.Report report;
            readonly Vector2Int size;
            readonly bool render;
            static readonly BindingFlags EventFlags = BindingFlags.Instance | BindingFlags.NonPublic;

            public Fixture(Vector2Int size, FocusMissionValidation.Report report, bool render)
            {
                this.size = size; this.report = report; this.render = render;
                previousEvents = EventSystem.current;
                scene = EditorSceneManager.NewPreviewScene();
                root = new GameObject("Synthetic shared modal canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
                ((RectTransform)root.transform).sizeDelta = size;
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var cameraObject = new GameObject("Synthetic shared modal camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene); camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.orthographic = true; camera.orthographicSize = size.y / 2f;
                camera.transform.position = new Vector3(0, 0, -10); camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target; camera.aspect = size.x / (float)size.y;
                canvas.worldCamera = camera; raycaster = root.GetComponent<GraphicRaycaster>();
                var eventObject = new GameObject("Synthetic modal focus", typeof(EventSystem));
                eventObject.transform.SetParent(root.transform, false); Events = eventObject.GetComponent<EventSystem>(); Events.enabled = false;
                Frame = FigmaMissionUi.Rect(root.transform, "Synthetic safe-area frame", 0, 0, 390, 844);
                fit = root.AddComponent<FigmaMissionCanvasFit>(); fit.Design = Frame; fit.Fit();
                Group = Frame.gameObject.AddComponent<CanvasGroup>();
                Background = FigmaMissionUi.Button(Frame, "기록", 20, 48, 350, 44, () => BackgroundClicks++);
                Outer = MakeScroll(Frame, "Synthetic outer records", 20, 160, 350, 400, 1400);
                Inner = MakeScroll(Outer.content, "Synthetic inner records", 10, 280, 330, 230, 800);
                Origin = FigmaMissionUi.Button(Inner.content, "선택한 기록", 16, 300, 280, 48, () => { });
                DisabledScroll = MakeScroll(Frame, "Synthetic disabled scroll", 0, 0, 10, 10, 30);
                DisabledScroll.enabled = false;
                root.SetActive(true);
                // Edit-mode preview fixtures do not receive runtime registration.
                typeof(EventSystem).GetMethod("OnEnable", EventFlags).Invoke(Events, null); EventSystem.current = Events;
                Layout();
            }

            static ScrollRect MakeScroll(Transform parent, string name, float x, float y, float w, float h, float contentHeight)
            {
                var viewport = FigmaMissionUi.Box(parent, name, x, y, w, h, Color.clear);
                viewport.GetComponent<Image>().raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
                var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport;
                scroll.content = FigmaMissionUi.Rect(viewport, name + " content", 0, 0, w, contentHeight);
                scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
                return scroll;
            }

            public void SelectOrigin()
            {
                Events.SetSelectedGameObject(null); Events.SetSelectedGameObject(Origin.gameObject, new PointerEventData(Events));
                Outer.content.anchoredPosition = new Vector2(0, 140);
                Inner.content.anchoredPosition = new Vector2(0, 75); Layout();
            }
            public void Layout()
            {
                for (int i = 0; i < 2; i++)
                {
                    fit.Fit(); Canvas.ForceUpdateCanvases();
                    foreach (var sheet in root.GetComponentsInChildren<GardenDetailSheet>(false)) sheet.Refresh();
                    foreach (var popup in root.GetComponentsInChildren<GardenMemoPopup>(false)) popup.Layout();
                    Canvas.ForceUpdateCanvases();
                }
                if (render) camera.Render();
            }
            public Rect Bounds(RectTransform rect)
            {
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var bottom = camera.WorldToScreenPoint(corners[0]); var top = camera.WorldToScreenPoint(corners[2]);
                return Rect.MinMaxRect(bottom.x, bottom.y, top.x, top.y);
            }
            List<RaycastResult> Hits(Vector2 point)
            {
                var hits = new List<RaycastResult>();
                raycaster.Raycast(new PointerEventData(Events) { position = point }, hits); report.raycastChecks++;
                return hits;
            }
            public void CheckCovered(Button button, Transform modal, Action<bool, string> check, string at)
            {
                var hits = Hits(Bounds((RectTransform)button.transform).center);
                check(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(modal)
                    && hits.All(hit => hit.gameObject.GetComponentInParent<Button>() != button), at + "the real pointer raycast is intercepted by the modal, including its dimmed area.");
            }
            public void CheckFirstHit(Button button, Action<bool, string> check, string at)
            {
                var hits = Hits(Bounds((RectTransform)button.transform).center);
                check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
                    at + "fixed action is the actual first pointer target: " + button.name);
            }
            public void Dispose()
            {
                GardenDetailSheet.CloseForOwner(Frame, false); GardenMemoPopup.TryCloseOpen();
                Events.SetSelectedGameObject(null); typeof(EventSystem).GetMethod("OnDisable", EventFlags).Invoke(Events, null);
                if (previousEvents != null) EventSystem.current = previousEvents;
                camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
