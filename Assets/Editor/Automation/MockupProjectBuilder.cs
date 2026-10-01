using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CapstoneDesign.Runtime;

namespace CapstoneDesign.EditorTools
{
    /// <summary>
    /// Builds the first mockup entirely through the Unity Editor API. Keeping
    /// generation here makes the source reviewable and avoids hand-maintained
    /// serialized Scene/Prefab YAML.
    /// </summary>
    public static class MockupProjectBuilder
    {
        private const string ScenePath = "Assets/Scenes/MockupMain.unity";
        private const string IslandMeshPath = "Assets/Art/FloatingIsland.asset";
        private const string PlantMeshPath = "Assets/Art/RewardPlant.asset";
        private const string KenneyRockPath = "Assets/Art/External/KenneyNatureKit/rock_largeA.fbx";
        private const string KenneyTreePath = "Assets/Art/External/KenneyNatureKit/tree_default.fbx";
        private const string KenneyBushPath = "Assets/Art/External/KenneyNatureKit/plant_bush.fbx";

        private static readonly Color Twilight = new Color(0.035f, 0.065f, 0.15f, 1f);
        private static readonly Color Panel = new Color(0.055f, 0.09f, 0.16f, 0.94f);
        private static readonly Color PanelSoft = new Color(0.10f, 0.15f, 0.22f, 0.92f);
        private static readonly Color TextPrimary = new Color(0.92f, 0.95f, 1f, 1f);
        private static readonly Color TextMuted = new Color(0.68f, 0.76f, 0.88f, 1f);
        private static readonly Color Accent = new Color(0.57f, 0.80f, 0.72f, 1f);

        [MenuItem("Capstone Mockup/Generate All")]
        public static void BuildMockup()
        {
            EnsureFolders();
            EnsureUrpPipeline();
            ConfigurePlayerSettings();
            CreateMaterials();
            CreateMeshes();
            CreateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Capstone mockup generated: " + ScenePath);
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/Art",
                "Assets/Materials",
                "Assets/Scenes",
                "Assets/Settings"
            };

            for (int i = 0; i < folders.Length; i++)
            {
                string[] pieces = folders[i].Split('/');
                string current = pieces[0];
                for (int pieceIndex = 1; pieceIndex < pieces.Length; pieceIndex++)
                {
                    string next = current + "/" + pieces[pieceIndex];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, pieces[pieceIndex]);
                    }

                    current = next;
                }
            }
        }

        private static void EnsureUrpPipeline()
        {
            const string pipelinePath = "Assets/Settings/MockupUrp.asset";
            const string rendererPath = "Assets/Settings/MockupRenderer.asset";
            UniversalRenderPipelineAsset pipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);

            if (pipeline == null)
            {
                UniversalRendererData rendererData =
                    AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    rendererData.name = "MockupRenderer";
                    AssetDatabase.CreateAsset(rendererData, rendererPath);
                }

                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                pipeline.name = "MockupUrp";
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }

            pipeline.renderScale = 1f;
            pipeline.shadowDistance = 25f;
            pipeline.msaaSampleCount = 1;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "CapstoneDesign2026";
            PlayerSettings.productName = "Sky Island Mockup";
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android,
                "com.capstonedesign2026.mockup");
            PlayerSettings.bundleVersion = "0.1.0-mockup";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        private static void CreateMaterials()
        {
            CreateMaterial("IslandTop", new Color(0.18f, 0.38f, 0.31f, 1f), 0.08f);
            CreateMaterial("IslandSide", new Color(0.16f, 0.12f, 0.16f, 1f), 0.12f);
            CreateMaterial("Trunk", new Color(0.25f, 0.15f, 0.11f, 1f), 0.05f);
            CreateMaterial("PlantLeaf", new Color(0.32f, 0.60f, 0.41f, 1f), 0.02f);
            CreateMaterial("PlantLeafLight", new Color(0.55f, 0.76f, 0.44f, 1f), 0.02f);
            CreateMaterial("TestSphere", new Color(0.93f, 0.71f, 0.40f, 1f), 0.18f);
        }

        private static Material CreateMaterial(string name, Color color, float metallic)
        {
            string path = "Assets/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            SetMaterialColor(material, color);
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.35f);
            }

            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static void CreateMeshes()
        {
            Mesh island = AssetDatabase.LoadAssetAtPath<Mesh>(IslandMeshPath);
            if (island == null)
            {
                island = BuildIslandMesh();
                island.name = "FloatingIsland";
                AssetDatabase.CreateAsset(island, IslandMeshPath);
            }

            Mesh plant = AssetDatabase.LoadAssetAtPath<Mesh>(PlantMeshPath);
            if (plant == null)
            {
                plant = BuildConeMesh(0.72f, 1.25f, 7);
                plant.name = "RewardPlant";
                AssetDatabase.CreateAsset(plant, PlantMeshPath);
            }
        }

        private static Mesh BuildIslandMesh()
        {
            const int segments = 9;
            const float topY = 0f;
            const float bottomY = -1.55f;
            List<Vector3> vertices = new List<Vector3>(segments * 2 + 2);
            List<int> topTriangles = new List<int>(segments * 3);
            List<int> sideTriangles = new List<int>(segments * 6);
            vertices.Add(new Vector3(0f, topY, 0f));

            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                float radius = 2.8f + Mathf.Sin(i * 1.7f) * 0.22f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, topY, Mathf.Sin(angle) * radius));
            }

            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                float radius = 2.05f + Mathf.Cos(i * 1.2f) * 0.17f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, bottomY, Mathf.Sin(angle) * radius));
            }

            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, bottomY, 0f));
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int topCurrent = 1 + i;
                int topNext = 1 + next;
                int bottomCurrent = 1 + segments + i;
                int bottomNext = 1 + segments + next;

                // Counter-clockwise from above gives an upward top normal.
                topTriangles.Add(0);
                topTriangles.Add(topNext);
                topTriangles.Add(topCurrent);

                sideTriangles.Add(topCurrent);
                sideTriangles.Add(topNext);
                sideTriangles.Add(bottomCurrent);
                sideTriangles.Add(topNext);
                sideTriangles.Add(bottomNext);
                sideTriangles.Add(bottomCurrent);

                sideTriangles.Add(bottomCenter);
                sideTriangles.Add(bottomCurrent);
                sideTriangles.Add(bottomNext);
            }

            Mesh mesh = new Mesh { name = "FloatingIsland" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(topTriangles, 0);
            mesh.SetTriangles(sideTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildConeMesh(float radius, float height, int segments)
        {
            List<Vector3> vertices = new List<Vector3>(segments + 2);
            List<int> triangles = new List<int>(segments * 6);
            vertices.Add(new Vector3(0f, height, 0f));
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            int baseCenter = vertices.Count;
            vertices.Add(Vector3.zero);
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int currentRing = 1 + i;
                int nextRing = 1 + next;
                triangles.Add(0);
                triangles.Add(currentRing);
                triangles.Add(nextRing);
                triangles.Add(baseCenter);
                triangles.Add(nextRing);
                triangles.Add(currentRing);
            }

            Mesh mesh = new Mesh { name = "RewardPlant" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.055f, 0.075f, 0.15f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = Twilight;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.018f;

            GameObject app = new GameObject("MockupApp");
            app.AddComponent<MockupRuntime>();
            app.AddComponent<AndroidTestMode>();

            CreateCamera();
            CreateMoonLight();
            CreateWorld();
            CreateUi();
            CreateEventSystem();
            IntegratedHomeImporter.Import(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("MockupCamera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 4.5f, -10.5f);
            cameraObject.transform.LookAt(new Vector3(0f, -0.35f, 0f));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 43f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Twilight;
            camera.allowHDR = true;
            camera.allowMSAA = false;
        }

        private static void CreateMoonLight()
        {
            GameObject lightObject = new GameObject("MoonLight", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.18f;
            light.color = new Color(0.62f, 0.72f, 1f, 1f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.45f;
        }

        private static void CreateWorld()
        {
            Mesh islandMesh = AssetDatabase.LoadAssetAtPath<Mesh>(IslandMeshPath);
            Mesh plantMesh = AssetDatabase.LoadAssetAtPath<Mesh>(PlantMeshPath);
            Material islandTop = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/IslandTop.mat");
            Material islandSide = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/IslandSide.mat");
            Material trunkMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Trunk.mat");
            Material leafMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PlantLeaf.mat");
            Material leafLightMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PlantLeafLight.mat");
            Material sphereMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TestSphere.mat");

            GameObject islandRoot = new GameObject("FloatingIsland");
            islandRoot.transform.position = new Vector3(0f, -0.35f, 0f);
            islandRoot.AddComponent<IslandMotion>();
            MeshFilter islandFilter = islandRoot.AddComponent<MeshFilter>();
            islandFilter.sharedMesh = islandMesh;
            MeshRenderer islandRenderer = islandRoot.AddComponent<MeshRenderer>();
            islandRenderer.sharedMaterials = new[] { islandTop, islandSide };
            MeshCollider collider = islandRoot.AddComponent<MeshCollider>();
            collider.sharedMesh = islandMesh;

            // Prefer the downloaded CC0 Kenney models when their FBX imports
            // exist. The procedural fallback keeps offline/headless bootstrap
            // deterministic and avoids making the art archive mandatory.
            GameObject rockAsset = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyRockPath);
            if (rockAsset != null)
            {
                GameObject rock = InstantiateExternal(rockAsset, "KenneyRock", islandRoot.transform);
                rock.transform.localPosition = new Vector3(-1.25f, 0.25f, -0.75f);
                rock.transform.localScale = Vector3.one * 0.72f;
            }

            GameObject treeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyTreePath);
            if (treeAsset != null)
            {
                GameObject tree = InstantiateExternal(treeAsset, "RewardPlant_Kenney", islandRoot.transform);
                tree.transform.localPosition = new Vector3(-0.45f, 0.15f, 0.12f);
                tree.transform.localScale = Vector3.one * 0.75f;
            }
            else
            {
                GameObject plant = new GameObject("RewardPlant_Procedural");
                plant.transform.SetParent(islandRoot.transform, false);
                plant.transform.localPosition = new Vector3(-0.45f, 0.2f, 0.12f);
                CreateTrunk(plant.transform, trunkMaterial);
                CreateCanopy(plant.transform, plantMesh, leafMaterial, new Vector3(0f, 1.15f, 0f), 1f);
                CreateCanopy(plant.transform, plantMesh, leafLightMaterial, new Vector3(0.28f, 1.8f, 0.05f), 0.67f);
            }

            GameObject bushAsset = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyBushPath);
            if (bushAsset != null)
            {
                GameObject bush = InstantiateExternal(bushAsset, "RewardBush_Kenney", islandRoot.transform);
                bush.transform.localPosition = new Vector3(1.1f, 0.22f, 0.6f);
                bush.transform.localScale = Vector3.one * 0.65f;
            }
            else
            {
                GameObject bush = new GameObject("RewardBush_Procedural");
                bush.transform.SetParent(islandRoot.transform, false);
                bush.transform.localPosition = new Vector3(1.1f, 0.22f, 0.6f);
                CreateCanopy(bush.transform, plantMesh, leafLightMaterial, Vector3.zero, 0.75f);
            }

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "PhysicsTestSphere";
            sphere.transform.SetParent(islandRoot.transform, false);
            sphere.transform.localPosition = new Vector3(1.05f, 1.35f, -0.4f);
            sphere.transform.localScale = Vector3.one * 0.42f;
            sphere.GetComponent<MeshRenderer>().sharedMaterial = sphereMaterial;
            Rigidbody body = sphere.AddComponent<Rigidbody>();
            body.mass = 0.25f;
            body.linearDamping = 0.15f;
            body.angularDamping = 0.2f;
            body.useGravity = true;
        }

        private static GameObject InstantiateExternal(GameObject asset, string name, Transform parent)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(asset);
            }

            instance.name = name;
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static void CreateTrunk(Transform parent, Material material)
        {
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "TreeTrunk";
            trunk.transform.SetParent(parent, false);
            trunk.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            trunk.transform.localScale = new Vector3(0.22f, 0.58f, 0.22f);
            trunk.GetComponent<MeshRenderer>().sharedMaterial = material;
            RemoveCollider(trunk);
        }

        private static void CreateCanopy(Transform parent, Mesh mesh, Material material, Vector3 localPosition, float scale)
        {
            GameObject canopy = new GameObject("LeafCanopy");
            canopy.transform.SetParent(parent, false);
            canopy.transform.localPosition = localPosition;
            canopy.transform.localScale = Vector3.one * scale;
            canopy.AddComponent<MeshFilter>().sharedMesh = mesh;
            canopy.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static void CreateUi()
        {
            GameObject canvasObject = new GameObject("UiCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1200f, 2000f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject islandPanel = CreateUiObject("IslandPanel", canvasObject.transform);
            SetFullRect(islandPanel.GetComponent<RectTransform>());
            Text greeting = CreateText("오늘도 천천히", islandPanel.transform, 52, TextAnchor.UpperCenter, TextPrimary);
            SetRect(greeting.rectTransform, new Vector2(0.05f, 0.8f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero);
            greeting.fontStyle = FontStyle.Bold;
            Text rewardHint = CreateText("작은 걸음이 섬을 가꿔요", islandPanel.transform, 28, TextAnchor.UpperCenter, TextMuted);
            SetRect(rewardHint.rectTransform, new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.84f), Vector2.zero, Vector2.zero);

            GameObject activitiesPanel = CreatePanel("ActivitiesPanel", canvasObject.transform, Panel);
            CreateActivityContent(activitiesPanel.transform);

            GameObject settingsPanel = CreatePanel("SettingsPanel", canvasObject.transform, Panel);
            Text settingsText = CreateText("설정", settingsPanel.transform, 50, TextAnchor.MiddleCenter, TextPrimary);
            SetRect(settingsText.rectTransform, new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.65f), Vector2.zero, Vector2.zero);

            GameObject sensorPanel = CreatePanel("SensorPanel", canvasObject.transform, new Color(0.015f, 0.025f, 0.06f, 0.88f));
            SetRect(sensorPanel.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -1030f), new Vector2(620f, -24f));
            Text sensorText = CreateText("ANDROID PLATFORM", sensorPanel.transform, 18, TextAnchor.UpperLeft, TextMuted);
            SetRect(sensorText.rectTransform, new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.97f), Vector2.zero, Vector2.zero);
            sensorText.horizontalOverflow = HorizontalWrapMode.Wrap;
            sensorText.verticalOverflow = VerticalWrapMode.Overflow;

            Button startSensors = CreateButton("StartSensorsButton", sensorPanel.transform, "Trial On", Accent, TextPrimary);
            Button stopSensors = CreateButton("StopSensorsButton", sensorPanel.transform, "Trial Off", new Color(0.34f, 0.38f, 0.50f, 1f), TextPrimary);
            Button sensorPermission = CreateButton("SensorPermissionButton", sensorPanel.transform, "Sensor Perm", new Color(0.29f, 0.45f, 0.55f, 1f), TextPrimary);
            Button healthPermission = CreateButton("HealthPermissionButton", sensorPanel.transform, "Health Perm", new Color(0.30f, 0.48f, 0.43f, 1f), TextPrimary);
            Button refreshHealth = CreateButton("RefreshHealthButton", sensorPanel.transform, "Refresh", new Color(0.35f, 0.43f, 0.58f, 1f), TextPrimary);
            SetRect(startSensors.GetComponent<RectTransform>(), new Vector2(0.03f, 0.035f), new Vector2(0.205f, 0.145f), Vector2.zero, Vector2.zero);
            SetRect(stopSensors.GetComponent<RectTransform>(), new Vector2(0.22f, 0.035f), new Vector2(0.395f, 0.145f), Vector2.zero, Vector2.zero);
            SetRect(sensorPermission.GetComponent<RectTransform>(), new Vector2(0.41f, 0.035f), new Vector2(0.615f, 0.145f), Vector2.zero, Vector2.zero);
            SetRect(healthPermission.GetComponent<RectTransform>(), new Vector2(0.63f, 0.035f), new Vector2(0.815f, 0.145f), Vector2.zero, Vector2.zero);
            SetRect(refreshHealth.GetComponent<RectTransform>(), new Vector2(0.83f, 0.035f), new Vector2(0.97f, 0.145f), Vector2.zero, Vector2.zero);

            SensorRawDisplay sensors = sensorPanel.AddComponent<SensorRawDisplay>();
            sensors.output = sensorText;
            sensors.startServiceButton = startSensors;
            sensors.stopServiceButton = stopSensors;
            sensors.sensorPermissionButton = sensorPermission;
            sensors.healthPermissionButton = healthPermission;
            sensors.refreshHealthButton = refreshHealth;

            GameObject navigation = CreatePanel("BottomNavigation", canvasObject.transform, PanelSoft);
            SetRect(navigation.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 190f));
            Button activitiesButton = CreateButton("ActivitiesButton", navigation.transform, "오늘의 활동", Accent, TextPrimary);
            Button islandButton = CreateButton("IslandButton", navigation.transform, "하늘섬", new Color(0.25f, 0.46f, 0.50f, 1f), TextPrimary);
            Button settingsButton = CreateButton("SettingsButton", navigation.transform, "설정", new Color(0.28f, 0.35f, 0.49f, 1f), TextPrimary);
            SetRect(activitiesButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.333f, 1f), Vector2.zero, Vector2.zero);
            SetRect(islandButton.GetComponent<RectTransform>(), new Vector2(0.333f, 0f), new Vector2(0.667f, 1f), Vector2.zero, Vector2.zero);
            SetRect(settingsButton.GetComponent<RectTransform>(), new Vector2(0.667f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            MockupNavigation nav = canvasObject.AddComponent<MockupNavigation>();
            nav.islandPanel = islandPanel;
            nav.activitiesPanel = activitiesPanel;
            nav.settingsPanel = settingsPanel;
            nav.activitiesButton = activitiesButton;
            nav.islandButton = islandButton;
            nav.settingsButton = settingsButton;
            nav.legacyBottomNavigation = navigation;
        }

        private static void CreateActivityContent(Transform parent)
        {
            Text header = CreateText("오늘의 활동", parent, 48, TextAnchor.MiddleCenter, TextPrimary);
            SetRect(header.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero);
            Text summary = CreateText("천천히, 할 수 있는 만큼", parent, 23, TextAnchor.MiddleCenter, TextMuted);
            SetRect(summary.rectTransform, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero);

            GameObject scrollObject = CreateUiObject("ActivityScroll", parent);
            SetRect(scrollObject.GetComponent<RectTransform>(), new Vector2(0.07f, 0.20f), new Vector2(0.93f, 0.71f), Vector2.zero, Vector2.zero);
            Image scrollBackground = scrollObject.AddComponent<Image>();
            scrollBackground.color = new Color(0.07f, 0.11f, 0.19f, 0.60f);
            ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            GameObject viewportObject = CreateUiObject("Viewport", scrollObject.transform);
            SetFullRect(viewportObject.GetComponent<RectTransform>());
            viewportObject.AddComponent<RectMask2D>();
            scroll.viewport = viewportObject.GetComponent<RectTransform>();

            GameObject contentObject = CreateUiObject("Content", viewportObject.transform);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            IReadOnlyList<QuestDefinition> quests = WeekOneQuestLibrary.Create();
            contentRect.sizeDelta = new Vector2(0f, quests.Count * 142f + 28f);
            scroll.content = contentRect;

            Button[] buttons = new Button[quests.Count];
            Text[] labels = new Text[quests.Count];
            for (int i = 0; i < quests.Count; i++)
            {
                Button row = CreateButton(
                    "Activity_" + (i + 1),
                    contentObject.transform,
                    quests[i].title,
                    new Color(0.12f, 0.18f, 0.27f, 0.94f),
                    TextPrimary);
                RectTransform rowRect = row.GetComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0.04f, 1f);
                rowRect.anchorMax = new Vector2(0.96f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -18f - i * 142f);
                rowRect.sizeDelta = new Vector2(0f, 118f);
                Text item = row.GetComponentInChildren<Text>();
                item.alignment = TextAnchor.MiddleLeft;
                item.fontSize = 27;
                SetRect(item.rectTransform, new Vector2(0.08f, 0f), new Vector2(0.94f, 1f), Vector2.zero, Vector2.zero);
                item.horizontalOverflow = HorizontalWrapMode.Wrap;
                item.verticalOverflow = VerticalWrapMode.Truncate;
                buttons[i] = row;
                labels[i] = item;
            }

            WeekOneQuestDemo questDemo = parent.gameObject.AddComponent<WeekOneQuestDemo>();
            questDemo.questButtons = buttons;
            questDemo.questLabels = labels;
            questDemo.summary = summary;
            GameObject rewardPlant = GameObject.Find("RewardPlant_Kenney") ??
                GameObject.Find("RewardPlant_Procedural");
            questDemo.rewardPlant = rewardPlant == null ? null : rewardPlant.transform;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = CreateUiObject(name, parent);
            SetFullRect(panel.GetComponent<RectTransform>());
            Image image = panel.AddComponent<Image>();
            image.color = color;
            return panel;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color, Color textColor)
        {
            GameObject buttonObject = CreateUiObject(name, parent);
            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.14f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            Text text = CreateText(label, buttonObject.transform, 26, TextAnchor.MiddleCenter, textColor);
            SetFullRect(text.rectTransform);
            return button;
        }

        private static Text CreateText(string content, Transform parent, int size, TextAnchor alignment, Color color)
        {
            GameObject textObject = CreateUiObject("Text", parent);
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            text.text = content;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject objectInstance = new GameObject(name, typeof(RectTransform));
            objectInstance.transform.SetParent(parent, false);
            return objectInstance;
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void SetFullRect(RectTransform rect)
        {
            SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
