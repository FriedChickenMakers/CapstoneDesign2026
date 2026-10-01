using System;
using System.Linq;
using CapstoneDesign.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CapstoneDesign.EditorTools
{
    public static class PrototypePreviewUpgrade
    {
        [MenuItem("Capstone Prototype/Expand Plant UI Area")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != PrototypeSceneBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved Prototype scene and stop Play Mode first.");
            var app = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PrototypeController>(true)).Single();
            Undo.RegisterFullObjectHierarchyUndo(app.portraitLayout.gameObject, "Expand plant UI area");
            var drag = app.pages[0].GetComponentInChildren<PrototypeIslandDrag>(true);
            Undo.RecordObject(drag.previewCamera, "Fit expanded plant preview");
            Configure(app);
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PROTOTYPE_PREVIEW_AREA_OK responsive UI area, compact growth footer, uncropped garden");
        }

        public static void Configure(PrototypeController app)
        {
            var home = app.pages[0].transform;
            var orbit = home.GetComponentInChildren<PrototypeIslandDrag>(true);
            if (orbit == null || orbit.previewCamera == null) return; // Scene builder binds this after UI creation.
            // One compact row opens more vertical space without moving the common CTA/navigation.
            Bottom(app.stageText.rectTransform, 48, 316, 300, 60);
            for (int i = 0; i < 4; i++) Bottom(app.progressDots[i].rectTransform, 398 + i * 60, 290, 48, 8);
            Bottom(app.missionStatus.rectTransform, 48, 204, 624, 58);
            Bind(home.Find("MainPlant"), home.Find("Guide"), app.stageText.transform, orbit, app, 8);
            var mission = app.pages[2].transform;
            Bind(mission.Find("MissionPlant"), mission.Find("Guide"), app.missionStatus.transform, orbit, app, 8);

            // The growth result can also use the full safe-area height instead of a fixed-height modal.
            var growth = (RectTransform)app.growthPopup.transform;
            growth.anchorMin = Vector2.zero; growth.anchorMax = Vector2.one;
            growth.offsetMin = growth.offsetMax = Vector2.zero;
            var card = (RectTransform)growth.Find("Card");
            card.anchorMin = Vector2.zero; card.anchorMax = Vector2.one;
            card.offsetMin = new Vector2(28, 106); card.offsetMax = new Vector2(-28, -102);
            Top(app.growthTitle.rectTransform, 48, 138, 624, 64);
            Top(app.growthGuide.rectTransform, 48, 218, 624, 46);
            Bottom(app.growthStage.rectTransform, 48, 380, 624, 62);
            Bottom(app.growthSummary.rectTransform, 48, 306, 624, 52);
            Bottom((RectTransform)growth.Find("ConfirmGrowth"), 76, 234, 568, 96);
            Bind(growth.Find("GrownPlantPreview"), app.growthGuide.transform, app.growthStage.transform, orbit, app, 40);
            app.portraitLayout.Apply();
            foreach (var preview in app.portraitLayout.GetComponentsInChildren<PrototypeGardenPreviewLayout>(true)) preview.Apply();
        }

        private static void Bind(Transform image, Transform upper, Transform lower, PrototypeIslandDrag orbit, PrototypeController app, float padding)
        {
            var layout = image.GetComponent<PrototypeGardenPreviewLayout>();
            if (layout == null) layout = image.gameObject.AddComponent<PrototypeGardenPreviewLayout>();
            layout.upperBoundary = (RectTransform)upper; layout.lowerBoundary = (RectTransform)lower;
            layout.orbit = orbit; layout.gardenRoot = app.plantView.transform.parent;
            layout.horizontalPadding = padding; layout.boundaryGap = 12; layout.edgeMargin = .02f;
            layout.RecalculateBounds();
            EditorUtility.SetDirty(layout);
        }
        private static void Top(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        private static void Bottom(RectTransform rect, float x, float topFromBottom, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, topFromBottom); rect.sizeDelta = new Vector2(width, height);
        }
    }
}
