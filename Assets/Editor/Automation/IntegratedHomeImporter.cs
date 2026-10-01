using System;
using System.Collections.Generic;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Regeneration imports only the source prototype's visual scene objects.
    // PrototypeController and its temporary PrototypeSession stay in Prototype.unity.
    public static class IntegratedHomeImporter
    {
        const string SourceScene = "Assets/Scenes/Prototype.unity";

        public static void Import(Scene destination)
        {
            if (!System.IO.File.Exists(SourceScene)) throw new InvalidOperationException("Prototype design scene is missing.");
            var source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            try
            {
                var canvasObject = Root(source, "PrototypeCanvas");
                var garden = Root(source, "Garden_Editable3D");
                var cameraObject = Root(source, "Garden Camera");
                foreach (var item in new[] { canvasObject, garden, cameraObject })
                    EditorSceneManager.MoveGameObjectToScene(item, destination);
                // The static garden renders on state/orbit/layout changes, avoiding
                // a continuous second camera on a phone carried all day.
                cameraObject.GetComponent<Camera>().enabled = false;
                UseIndependentGardenPalette(garden);

                canvasObject.name = "IntegratedHomeCanvas";
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 30;
                var frame = Require(canvasObject.transform, "SafePortraitFrame");
                DeleteChildrenExcept(frame, "MainContent");
                var content = Require(frame, "MainContent");
                DeleteChildrenExcept(content, "Brand", "Date", "GardenAndGrowth", "BottomNavigation", "HomeTab", "MoodTab");
                var backdrop = canvasObject.transform.Find("ModalBackdrop");
                if (backdrop != null) UnityEngine.Object.DestroyImmediate(backdrop.gameObject);

                var home = Require(content, "GardenAndGrowth");
                var missionButton = Require(home, "GardenAction").GetComponent<Button>();
                ClearPersistent(missionButton);
                var shopObject = UnityEngine.Object.Instantiate(missionButton.gameObject, home);
                shopObject.name = "GardenShopAction";
                var shopButton = shopObject.GetComponent<Button>();
                ClearPersistent(shopButton);
                HalfButton(missionButton.GetComponent<RectTransform>(), false);
                HalfButton(shopButton.GetComponent<RectTransform>(), true);
                missionButton.GetComponentInChildren<TMP_Text>(true).text = "오늘의 활동";
                shopButton.GetComponentInChildren<TMP_Text>(true).text = "정원 가꾸기";

                var homeTab = Require(content, "HomeTab").GetComponent<Button>();
                var activityTab = Require(content, "MoodTab").GetComponent<Button>();
                var settingsObject = UnityEngine.Object.Instantiate(activityTab.gameObject, content);
                settingsObject.name = "SettingsTab";
                var settingsTab = settingsObject.GetComponent<Button>();
                settingsTab.GetComponentInChildren<PrototypeUiIcon>(true).symbol = PrototypeUiIcon.Symbol.Gear;
                activityTab.name = "ActivityTab";
                foreach (var button in new[] { homeTab, activityTab, settingsTab }) ClearPersistent(button);
                Tab(homeTab, .04f, .31f);
                Tab(activityTab, .36f, .64f);
                Tab(settingsTab, .69f, .96f);

                // The home-owned camera is parked outside the other screens.
                // Its independent palette needs no continuously active light.
                var visuals = new GameObject("IntegratedGardenVisuals");
                SceneManager.MoveGameObjectToScene(visuals, destination);
                foreach (var item in new[] { garden, cameraObject })
                    item.transform.SetParent(visuals.transform, true);

                var oldUi = Root(destination, "UiCanvas");
                var navigation = oldUi.GetComponent<MockupNavigation>();
                var presenter = oldUi.AddComponent<GardenHomePresenter>();
                presenter.homeCanvas = canvas;
                presenter.gardenVisuals = visuals;
                presenter.oldCamera = Root(destination, "MockupCamera").GetComponent<Camera>();
                presenter.oldLight = Root(destination, "MoonLight").GetComponent<Light>();
                presenter.gardenCamera = cameraObject.GetComponent<Camera>();
                presenter.activity = navigation.activitiesPanel.GetComponent<WeekOneQuestDemo>();
                presenter.navigation = navigation;
                presenter.plant = garden.GetComponentInChildren<PrototypePlantView>(true);
                presenter.gardenRoot = garden.transform;
                presenter.date = Require(content, "Date").GetComponent<TMP_Text>();
                presenter.title = Require(home, "Title").GetComponent<TMP_Text>();
                presenter.guide = Require(home, "Guide").GetComponent<TMP_Text>();
                presenter.stage = Require(home, "PlantStage").GetComponent<TMP_Text>();
                presenter.dailyStatus = Require(home, "DailyStatus").GetComponent<TMP_Text>();
                presenter.missionName = Require(home, "MissionName").GetComponent<TMP_Text>();
                presenter.missionActionLabel = missionButton.GetComponentInChildren<TMP_Text>(true);
                presenter.homeTabLabel = homeTab.GetComponentInChildren<TMP_Text>(true);
                presenter.activityTabLabel = activityTab.GetComponentInChildren<TMP_Text>(true);
                presenter.settingsTabLabel = settingsTab.GetComponentInChildren<TMP_Text>(true);
                presenter.missionButton = missionButton;
                presenter.shopButton = shopButton;
                presenter.homeTab = homeTab;
                presenter.activityTab = activityTab;
                presenter.settingsTab = settingsTab;
                presenter.progressDots = Enumerable.Range(1, 4).Select(i => Require(home, "GrowthStep_" + i).GetComponent<PrototypeUiSurface>()).ToArray();
                navigation.home = presenter;
                if (presenter.plant == null) throw new InvalidOperationException("Prototype plant art is missing.");
                EditorSceneManager.MarkSceneDirty(destination);
            }
            finally
            {
                EditorSceneManager.CloseScene(source, true);
            }
        }

        static GameObject Root(Scene scene, string name)
        {
            var result = scene.GetRootGameObjects().FirstOrDefault(item => item.name == name);
            if (result == null) throw new InvalidOperationException(name + " missing from " + scene.path);
            return result;
        }
        static void UseIndependentGardenPalette(GameObject garden)
        {
            const string folder = "Assets/Materials/IntegratedHome";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Materials", "IntegratedHome");
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP Unlit shader unavailable for integrated garden.");
            var converted = new Dictionary<Material, Material>();
            foreach (var renderer in garden.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null) continue;
                    if (!converted.TryGetValue(source, out var target))
                    {
                        var path = folder + "/" + source.name + ".mat";
                        target = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (target == null)
                        {
                            target = new Material(shader) { name = source.name };
                            AssetDatabase.CreateAsset(target, path);
                        }
                        target.shader = shader;
                        target.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                        EditorUtility.SetDirty(target);
                        converted.Add(source, target);
                    }
                    materials[i] = target;
                }
                renderer.sharedMaterials = materials;
            }
        }
        static Transform Require(Transform root, string path)
        {
            var result = root.Find(path);
            if (result == null) throw new InvalidOperationException(path + " missing under " + root.name);
            return result;
        }
        static void DeleteChildrenExcept(Transform parent, params string[] names)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (!names.Contains(child.name)) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
        static void ClearPersistent(Button button)
        {
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
        }
        static void HalfButton(RectTransform rect, bool right)
        {
            // Source uses a 720px portrait design frame with top-left anchored coordinates.
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);
            var position = rect.anchoredPosition;
            position.x = right ? 372 : 48;
            rect.anchoredPosition = position;
        }
        static void Tab(Button button, float left, float right)
        {
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(left, 0);
            rect.anchorMax = new Vector2(right, 0);
            rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 24);
            rect.sizeDelta = new Vector2(0, 54);
        }
    }
}
