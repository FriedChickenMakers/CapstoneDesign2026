using System;
using System.Linq;
using CapstoneDesign.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    public static class PrototypeGardenUpgrade
    {
        [MenuItem("Capstone Prototype/Apply Closer Garden View")]
        public static void ApplyCloserView()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != PrototypeSceneBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved Prototype scene and stop Play Mode first.");
            var app = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PrototypeController>(true)).Single();
            var drag = app.pages[(int)PrototypePage.Garden].transform.Find("MainPlant").GetComponent<PrototypeIslandDrag>();
            var camera = drag.previewCamera;
            if (camera == null || !camera.orthographic)
                throw new InvalidOperationException("The garden must use its orthographic preview camera.");
            // Distance controls the orbit; orthographic size controls visible magnification.
            Undo.RecordObjects(new UnityEngine.Object[] { drag, camera, camera.transform }, "Closer garden view");
            drag.distance = PrototypeIslandDrag.DefaultDistance;
            camera.orthographicSize = PrototypeGardenArt.DefaultOrthographicSize;
            drag.ResetView();
            EditorUtility.SetDirty(drag);
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(camera.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"PROTOTYPE_CLOSER_VIEW_OK distance={drag.distance} orthographicSize={camera.orthographicSize}");
        }

        [MenuItem("Capstone Prototype/Apply Garden Art")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != PrototypeSceneBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved Prototype scene and stop Play Mode first.");
            var app = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PrototypeController>(true)).Single();
            // Only the owned garden roots are replaced; scene UI, navigation and all unrelated edits stay intact.
            Undo.RecordObject(app, "Upgrade four-stage garden");
            Undo.RecordObject(app.definition, "Include seed in four visible stages");
            foreach (var root in scene.GetRootGameObjects().Where(r => r.name == "Garden_Editable3D"
                || r.name == "Garden Camera" || r.name == "Garden Soft Light" || r.name == "Garden Fill Light"))
                Undo.DestroyObjectImmediate(root);
            PrototypeDefaults.MigrateDailyPrototype(app.definition);
            EditorUtility.SetDirty(app.definition);
            app.plantView = PrototypeGardenArt.Build(out RenderTexture preview, out Camera camera);
            foreach (var image in app.portraitLayout.GetComponentsInChildren<RawImage>(true))
            {
                Undo.RecordObject(image, "Bind refined garden preview");
                image.texture = preview;
                EditorUtility.SetDirty(image);
            }
            ConfigureDrag(app, camera);
            app.gardenGuide.text = "섬을 드래그해 천천히 둘러보세요.";
            app.plantView.Render(new PrototypeSession(app.definition), app.definition);
            app.progressDots[0].color = new Color(.31f, .47f, .37f);
            foreach (var root in scene.GetRootGameObjects().Where(r => r.name.StartsWith("Garden")))
                Undo.RegisterCreatedObjectUndo(root, "Upgrade garden art");
            EditorUtility.SetDirty(app);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PROTOTYPE_GARDEN_ART_OK four stages including seed, home orbit " + scene.path);
        }

        public static void ConfigureDrag(PrototypeController app, Camera camera)
        {
            var image = app.pages[(int)PrototypePage.Garden].transform.Find("MainPlant").GetComponent<RawImage>();
            image.raycastTarget = true;
            var drag = image.GetComponent<PrototypeIslandDrag>();
            if (drag == null) drag = image.gameObject.AddComponent<PrototypeIslandDrag>();
            drag.previewCamera = camera;
            drag.ResetView();
            EditorUtility.SetDirty(drag);
            PrototypePreviewUpgrade.Configure(app);
        }
    }
}
