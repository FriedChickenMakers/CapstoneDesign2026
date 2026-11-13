using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // An explicit synthetic renderer, not an automatic editor callback. It renders
    // the actual vector components, independently of saves, devices or network.
    public static class MissionArtQualityValidation
    {
        [Serializable] sealed class Check { public string code, status, detail; public int variants, maxVertices; }
        [Serializable] sealed class Report
        {
            public string utc, environment;
            public int passed, failed, assertions, variants;
            public List<Check> checks = new List<Check>();
            public List<string> captures = new List<string>(), errors = new List<string>();
        }
        sealed class Variant
        {
            public string Code, Key, Choice, Trigger = "", Activity = "", Period = "";
            public float Motion;
            public bool CurrentReview;
            public Variant(string c, string k, string v = "") { Code = c; Key = k; Choice = v; }
        }
        static Report report;
        static string output;
        static bool baseline;
        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-artQualityReport");
            if (!Application.isBatchMode || Application.companyName != "CapstoneSyntheticValidation"
                || Application.productName != "MemoPopup-v07" || index < 0)
                throw new InvalidOperationException("Mission art review requires the isolated synthetic project.");
            output = Path.GetDirectoryName(args[index + 1]); Directory.CreateDirectory(output);
            baseline = args.Contains("-artQualityBaseline");
            report = new Report { utc = DateTimeOffset.UtcNow.ToString("O"),
                environment = "Actual vector component meshes and offscreen GPU renders; synthetic preview scenes; no user state" };
            Application.logMessageReceived += Observe;
            try
            {
                foreach (var mission in FocusMissionCatalog.All)
                {
                    var check = new Check { code = mission.Code };
                    try
                    {
                        var variants = Variants(mission).ToArray(); check.variants = variants.Length;
                        using (var scene = new ArtScene(256, 256))
                        {
                            var graphic = scene.Add(new Variant(mission.Code, "overview"), 0, 0, 256);
                            foreach (var variant in variants)
                            {
                                Configure(graphic, variant); var mesh = Mesh(graphic);
                                try
                                {
                                    report.variants++; check.maxVertices = Math.Max(check.maxVertices, mesh.vertexCount);
                                    Assert(mesh.vertexCount > 30 && mesh.vertexCount < 65000, "A visible bounded UI mesh: " + variant.Key);
                                    Assert(!graphic.raycastTarget, "Decoration cannot intercept input");
                                    Assert(mesh.triangles.Length % 3 == 0, "Complete triangles");
                                    foreach (var point in mesh.vertices)
                                        Assert(!float.IsNaN(point.x) && !float.IsInfinity(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.y), "Finite artwork geometry");
                                    var rect = graphic.rectTransform.rect; var vertices = mesh.vertices; var colors = mesh.colors32;
                                    int escaped = 0;
                                    for (int i = 0; i < vertices.Length; i++)
                                        if (colors[i].a > 8 && (vertices[i].x < rect.xMin - 1 || vertices[i].x > rect.xMax + 1
                                            || vertices[i].y < rect.yMin - 1 || vertices[i].y > rect.yMax + 1)) escaped++;
                                    if (!baseline) Assert(escaped == 0, "Opaque artwork stays within its square: " + variant.Key + " choice=" + variant.Choice + " escaped=" + escaped);
                                    var hash = Hash(mesh);
                                    Configure(graphic, new Variant(mission.Code, "guide-help", ""));
                                    using (var intermediate = new OwnedMesh(Mesh(graphic))) { }
                                    Configure(graphic, variant);
                                    using (var returned = new OwnedMesh(Mesh(graphic)))
                                        Assert(Hash(returned.Value) == hash, "Back/reconfigure restores exact stage and choice geometry: " + variant.Key);
                                }
                                finally { UnityEngine.Object.DestroyImmediate(mesh); }
                            }
                        }
                        CaptureSheet(mission.Code + "-stages", Representative(mission).ToArray(), 4, 224);
                        check.status = "pass"; report.passed++;
                    }
                    catch (Exception error) { check.status = "fail"; check.detail = error.ToString(); report.failed++; }
                    report.checks.Add(check);
                }
                CaptureSheet("P-overviews", FocusMissionCatalog.All.Where(m => m.Code.StartsWith("P")).Select(m => new Variant(m.Code, "overview")).ToArray(), 7, 180);
                CaptureSheet("M-overviews", FocusMissionCatalog.All.Where(m => m.Code.StartsWith("M")).Select(m => new Variant(m.Code, "overview")).ToArray(), 6, 180);
            }
            finally
            {
                Application.logMessageReceived -= Observe;
                if (report.errors.Count > 0) report.failed++;
                File.WriteAllText(args[index + 1], JsonUtility.ToJson(report, true));
            }
            Debug.Log("MISSION_ART_QUALITY " + report.passed + " pass " + report.failed + " fail; variants=" + report.variants);
            EditorApplication.Exit(report.failed == 0 ? 0 : 1);
        }
        static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) report.errors.Add(message + "\n" + trace); }
        static void Assert(bool condition, string message) { report.assertions++; if (!condition) throw new InvalidOperationException(message); }
        static IEnumerable<Variant> Variants(FocusMissionDefinition mission)
        {
            var choices = new List<string> { "" };
            choices.AddRange(mission.Steps.SelectMany(s => s.Options).Where(s => !string.IsNullOrEmpty(s)).Distinct());
            if (mission.Code == "P01") choices = new List<string> { "", "코", "가슴", "배" };
            foreach (var key in Keys(mission)) foreach (var choice in choices.Distinct()) yield return new Variant(mission.Code, key, choice);
            if (mission.Code == "P27")
                foreach (var trigger in new[] { "점심 후", "샤워 후" })
                    foreach (var activity in new[] { "호흡", "몸의 접촉", "주변 소리" })
                        foreach (var key in new[] { "trigger", "activity", "wait", "practice", "notice" })
                            yield return new Variant(mission.Code, key, activity) { Trigger = trigger, Activity = activity };
            if (mission.Code == "P28")
                foreach (var activity in new[] { "호흡", "몸의 접촉", "주변 소리", "잠시 멈춤" })
                    foreach (var period in new[] { "아침", "점심", "저녁", "생활 행동 뒤", "나중에 정하기" })
                        foreach (var key in new[] { "time", "review", "practice" })
                            yield return new Variant(mission.Code, key, key == "practice" ? "호흡" : period) { Activity = activity, Period = period };
            if (mission.Code == "M17")
                foreach (float motion in new[] { -3f, 3f }) yield return new Variant("M17", "breath") { Motion = motion };
            if (mission.Code == "P28")
                foreach (var activity in new[] { "호흡", "몸의 접촉", "주변 소리", "잠시 멈춤" })
                    foreach (var period in new[] { "아침", "점심", "저녁", "생활 행동 뒤", "나중에 정하기" })
                        yield return new Variant("P28", "review", "몸의 접촉") { Activity = activity, Period = period, CurrentReview = true };
        }
        static IEnumerable<string> Keys(FocusMissionDefinition mission)
        {
            if (mission.Code == "P01") return new[] { "overview", "preparation", "choice", "notice", "observe", "return", "practice", "reflection", "guide-help" };
            if (mission.Code == "P02") return new[] { "overview", "preparation", "notice", "observe", "return", "practice", "reflection", "guide-help" };
            if (mission.Code == "M01") return new[] { "overview", "choice", "curtain", "lamp", "brighten", "guide-help", "complete" };
            if (mission.Code.StartsWith("P") && int.Parse(mission.Code.Substring(1)) <= 7)
                return new[] { "overview", "preparation", "choice", "cue0", "cue1", "cue2", "practice", "reflection", "guide-help" };
            return new[] { "overview" }.Concat(mission.Steps.Select(s => s.Key))
                .Concat(mission.Code == "M10" ? new[] { "plan-0", "plan-1", "plan-2", "guide-help" } : new[] { "guide-help" }).Distinct();
        }
        static IEnumerable<Variant> Representative(FocusMissionDefinition mission)
        {
            foreach (string key in Keys(mission)) yield return new Variant(mission.Code, key);
            // Capture choices where they actually change the illustration. The
            // complete mesh matrix above remains broader than these visual sheets.
            if (mission.Code == "P17")
            {
                foreach (var activity in new[] { "물 마시기", "쉬기", "창밖 보기" })
                    yield return new Variant("P17", "action", activity);
                yield break;
            }
            if (mission.Code == "P22")
            {
                foreach (var activity in new[] { "걷기", "앉아서 살피기" })
                    foreach (var key in new[] { "contact", "move" })
                        yield return new Variant("P22", key, activity);
                yield break;
            }
            if (mission.Code == "P24")
            {
                foreach (var route in new[] { "상대의 이야기", "앱의 짧은 이야기" })
                    yield return new Variant("P24", "listen", route);
                yield break;
            }
            if (mission.Code == "P26")
            {
                foreach (var help in new[] { "사람", "물건", "환경" })
                    foreach (var key in new[] { "help", "check" })
                        yield return new Variant("P26", key, help);
                yield return new Variant("P26", "check", "아직 없어요");
                yield break;
            }
            if (mission.Code == "P27")
            {
                foreach (var trigger in new[] { "점심 후", "샤워 후" })
                    foreach (var activity in new[] { "호흡", "몸의 접촉", "주변 소리" })
                        yield return new Variant("P27", "practice", activity) { Trigger = trigger, Activity = activity };
                yield return new Variant("P27", "wait", "몸의 접촉") { Trigger = "샤워 후", Activity = "몸의 접촉" };
                yield return new Variant("P27", "notice", "주변 소리") { Trigger = "샤워 후", Activity = "주변 소리" };
                yield break;
            }
            if (mission.Code == "P28")
            {
                foreach (var activity in new[] { "호흡", "몸의 접촉", "주변 소리", "잠시 멈춤" })
                {
                    yield return new Variant("P28", "practice", activity);
                    yield return new Variant("P28", "review", "저녁") { Activity = activity, Period = "저녁" };
                }
                foreach (var period in new[] { "아침", "나중에 정하기" })
                    yield return new Variant("P28", "time", period) { Activity = "몸의 접촉", Period = period };
                yield return new Variant("P28", "next-activity", "호흡") { Activity = "몸의 접촉" };
                yield return new Variant("P28", "review", "몸의 접촉") { Activity = "주변 소리", Period = "저녁", CurrentReview = true };
                yield break;
            }
            string[] choices = mission.Code == "P01" ? new[] { "코", "가슴", "배" }
                : mission.Steps.Where(s => new[] { "practice", "activity", "experience", "route", "next-activity", "notice" }.Contains(s.Key))
                    .SelectMany(s => s.Options).Distinct().Take(5).ToArray();
            foreach (string choice in choices)
                yield return new Variant(mission.Code, mission.Code == "P01" ? "notice"
                    : mission.Code == "P06" || mission.Code == "P07" ? "cue0"
                    : mission.Code == "M02" || mission.Code == "M12" ? "notice"
                    : mission.Steps.FirstOrDefault(s => s.Kind == "focus" || s.Kind == "breathe")?.Key ?? "overview", choice);
            if (mission.Code == "M13")
                foreach (var key in new[] { "recall", "word" }) yield return new Variant("M13", key, "잘 모르겠어요");
            if (mission.Code == "M17")
                foreach (float motion in new[] { -3f, 3f }) yield return new Variant("M17", "breath") { Motion = motion };
        }
        static void Configure(MaskableGraphic graphic, Variant variant)
        {
            var mission = FocusMissionCatalog.Find(variant.Code);
            if (graphic is P01BreathIllustration p1)
                p1.Configure((P01BreathIllustration.Stage)Enum.Parse(typeof(P01BreathIllustration.Stage), variant.Key == "guide-help" ? "Help" : variant.Key, true), variant.Choice);
            else if (graphic is P02SoundIllustration p2)
                p2.Configure((P02SoundIllustration.Stage)Enum.Parse(typeof(P02SoundIllustration.Stage), variant.Key == "guide-help" ? "Help" : variant.Key, true));
            else if (graphic is M01LightIllustration m1)
                m1.Configure((M01LightIllustration.Stage)Enum.Parse(typeof(M01LightIllustration.Stage), variant.Key == "guide-help" ? "Help" : variant.Key, true));
            else if (graphic is FocusPracticeIllustration p)
            {
                p.Configure(variant.Code, variant.Key, variant.Choice, FigmaMissionUi.ActionColor(FigmaMissionUi.Hex(mission.AccentHex)));
                p.ConfigureContext(variant.Trigger, variant.Activity, variant.Period, variant.CurrentReview);
            }
            else if (graphic is AdditionalMissionIllustration m)
            {
                m.Configure(variant.Code, variant.Key, variant.Choice, FigmaMissionUi.ActionColor(FigmaMissionUi.Hex(mission.AccentHex)));
                // Sample both extrema of the existing animation without playing
                // a real user scene or pretending this checks frame timing.
                typeof(AdditionalMissionIllustration).GetField("breathMotion", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(m, variant.Motion);
                m.SetVerticesDirty();
            }
        }
        static Mesh Mesh(MaskableGraphic graphic)
        {
            using (var helper = new VertexHelper())
            {
                graphic.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                var result = new Mesh(); helper.FillMesh(result); return result;
            }
        }
        sealed class OwnedMesh : IDisposable
        { public readonly Mesh Value; public OwnedMesh(Mesh value) { Value = value; } public void Dispose() { UnityEngine.Object.DestroyImmediate(Value); } }
        static string Hash(Mesh mesh)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                foreach (var v in mesh.vertices) { writer.Write(v.x); writer.Write(v.y); }
                foreach (var c in mesh.colors32) { writer.Write(c.r); writer.Write(c.g); writer.Write(c.b); writer.Write(c.a); }
                foreach (var t in mesh.triangles) writer.Write(t);
                writer.Flush(); using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
            }
        }
        static void CaptureSheet(string name, Variant[] variants, int columns, int tile)
        {
            int rows = Mathf.CeilToInt(variants.Length / (float)columns), height = rows * (tile + 36), width = columns * tile;
            using (var scene = new ArtScene(width, height))
            {
                for (int i = 0; i < variants.Length; i++)
                {
                    var item = variants[i]; float x = i % columns * tile, y = i / columns * (tile + 36);
                    var mission = FocusMissionCatalog.Find(item.Code);
                    var background = FigmaMissionUi.Box(scene.Root.transform, "Theme", x, y, tile, tile + 36, FigmaMissionUi.Hex(mission.BackgroundHex), 0);
                    background.GetComponent<Image>().raycastTarget = false;
                    scene.Add(item, x + 10, y + 30, tile - 20);
                    string title = item.Code + " · " + item.Key, detail = item.Choice;
                    if (item.Code == "P27" && item.Trigger.Length > 0)
                        detail = item.Trigger + " · " + item.Activity;
                    if (item.Code == "P28" && item.Activity.Length > 0)
                    {
                        if (item.CurrentReview) { title += " · 현재"; detail = item.Choice + " / 다음: " + item.Activity; }
                        else
                        {
                            if (item.Key == "review") title += " · 계획";
                            detail = item.Activity + (item.Period.Length == 0 ? "" : " · " + item.Period);
                        }
                    }
                    FigmaMissionUi.Label(scene.Root.transform, title + (detail.Length == 0 ? "" : "\n" + detail),
                        x + 4, y + 4, tile - 8, 32, 11, FigmaMissionUi.Ink, false, TextAnchor.UpperCenter);
                }
                string path = Path.Combine(output, name + ".png"); scene.Save(path); report.captures.Add(path);
            }
        }
        sealed class ArtScene : IDisposable
        {
            public readonly GameObject Root;
            readonly Scene scene;
            readonly Camera camera;
            readonly RenderTexture target;
            public ArtScene(int width, int height)
            {
                scene = EditorSceneManager.NewPreviewScene();
                Root = new GameObject("Synthetic art canvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(Root, scene);
                var canvas = Root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)Root.transform).sizeDelta = new Vector2(width, height);
                var go = new GameObject("Synthetic art camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(go, scene);
                camera = go.GetComponent<Camera>(); camera.scene = scene; camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = height / 2f; camera.aspect = width / (float)height;
                camera.transform.position = new Vector3(0, 0, -10); camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                target = new RenderTexture(width, height, 24); camera.targetTexture = target; canvas.worldCamera = camera;
            }
            public MaskableGraphic Add(Variant variant, float x, float y, float size)
            {
                var rect = FigmaMissionUi.Rect(Root.transform, "Art." + variant.Code, x, y, size, size);
                MaskableGraphic graphic = variant.Code == "P01" ? rect.gameObject.AddComponent<P01BreathIllustration>()
                    : variant.Code == "P02" ? rect.gameObject.AddComponent<P02SoundIllustration>()
                    : variant.Code == "M01" ? rect.gameObject.AddComponent<M01LightIllustration>()
                    : variant.Code.StartsWith("P") ? (MaskableGraphic)rect.gameObject.AddComponent<FocusPracticeIllustration>()
                    : rect.gameObject.AddComponent<AdditionalMissionIllustration>();
                Configure(graphic, variant); Canvas.ForceUpdateCanvases(); return graphic;
            }
            public void Save(string path)
            {
                Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                try { RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
                finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
            }
            public void Dispose()
            { camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
