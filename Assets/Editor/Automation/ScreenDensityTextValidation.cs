using System;
using System.Reflection;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Synthetic labels only: verify layout-sized text keeps every rendered glyph
    // at both device densities, without reading app state or opening a project scene.
    public static class ScreenDensityTextValidation
    {
        static readonly MethodInfo Populate = typeof(ScreenDensityText).GetMethod("OnPopulateMesh",
            BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null);
        static readonly MethodInfo PopulateLegacy = typeof(Text).GetMethod("OnPopulateMesh",
            BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null);

        public static void Run()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            int cases = 0, legacyMismatches = 0;
            try
            {
                var root = new GameObject("Synthetic text layout", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(root, scene);
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var frame = new GameObject("Nested design frame", typeof(RectTransform));
                frame.transform.SetParent(root.transform, false);
                var label = Create<ScreenDensityText>(frame.transform);
                var legacy = Create<Text>(frame.transform);
                string[] samples = {
                    "어느 기간이든 감각 관찰부터 나를 돌보는 계획과 다시 시작하는 방법까지 배워요.",
                    "기간을 바꿔도 지금까지의 기록과 정원은 남아요. 돌아오면 멈춘 진도부터 이어갈 수 있어요.",
                    "함께 기억하기로 고른 활동\n오늘의 작은 참여가 정원에 남아요.",
                    "가나다라마바사아자차카타파하 일상에서 몸의 감각과 생각을 천천히 관찰해 보세요."
                };
                foreach (int screenWidth in new[] { 1080, 1200 })
                foreach (float designWidth in new[] { 390f, 720f })
                foreach (int size in new[] { 14, 17, 28, 32 })
                {
                    canvas.scaleFactor = screenWidth / 720f;
                    frame.transform.localScale = Vector3.one * (720f / designWidth);
                    label.fontSize = legacy.fontSize = size;
                    foreach (string sample in samples)
                    foreach (float width in new[] { 165f, 268f, 310f, 528f })
                    {
                        label.text = legacy.text = sample;
                        label.rectTransform.sizeDelta = legacy.rectTransform.sizeDelta = new Vector2(width, 4000);
                        label.verticalOverflow = VerticalWrapMode.Overflow;
                        var complete = Render(label);
                        float measured = label.preferredHeight;
                        label.rectTransform.sizeDelta = new Vector2(width, measured + .5f);
                        label.verticalOverflow = VerticalWrapMode.Truncate;
                        var fitted = Render(label);
                        Require(fitted == complete, "Measured height clipped text: " + screenWidth + "/" + designWidth
                            + " size=" + size + " width=" + width + " height=" + measured);
                        // Preserve independent evidence that the previous canvas-only
                        // layout can disagree with the final-density mesh.
                        label.rectTransform.sizeDelta = new Vector2(width, legacy.preferredHeight + .5f);
                        if (Render(label) != complete) legacyMismatches++;
                        cases++;
                    }
                    label.text = "정원의 작은 기록을 함께 살펴보기";
                    label.verticalOverflow = VerticalWrapMode.Overflow;
                    label.rectTransform.sizeDelta = new Vector2(label.preferredWidth + .5f, 4000);
                    Render(label);
                    Require(label.cachedTextGenerator.lineCount == 1,
                        "Preferred width must hold one unbroken line at the rendered density");
                }

                Debug.Log("SCREEN_DENSITY_LAYOUT_PASS cases=" + cases + " legacyClippedCases=" + legacyMismatches);
                ValidateReentrantAtlasGeneration(root.transform);

                label.fontSize = legacy.fontSize = 17;
                label.rectTransform.sizeDelta = legacy.rectTransform.sizeDelta = new Vector2(268, 200);
                label.verticalOverflow = legacy.verticalOverflow = VerticalWrapMode.Overflow;
                var input = frame.AddComponent<InputField>();
                input.lineType = InputField.LineType.MultiLineNewline;
                input.textComponent = label;
                // InputField owns the label: writing label.text directly invokes
                // UpdateLabel and replaces it with the field's still-empty text.
                input.text = legacy.text = "입력 중인 합성 문장과 선택 영역";
                Require(label.text == legacy.text, "Input fixture must display the same sample in both renderers");
                Require(Mathf.Abs(label.preferredWidth - legacy.preferredWidth) < .001f
                    && Mathf.Abs(label.preferredHeight - legacy.preferredHeight) < .001f,
                    "InputField measurement must retain the legacy caret generator contract");
                Require(Render(label) == Render(legacy),
                    "InputField rendering must retain the legacy caret generator contract");
                Debug.Log("SCREEN_DENSITY_TEXT_VALIDATION_PASS cases=" + cases + " legacyClippedCases=" + legacyMismatches);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static void ValidateReentrantAtlasGeneration(Transform parent)
        {
            // During a Canvas rebuild, FontTextureChanged can render another
            // label synchronously into Graphic's shared VertexHelper. Recreate
            // that nested render from a real atlas callback, then inspect the
            // resulting mesh rather than only the generator's character count.
            // Unity does not support cloning dynamic Font assets. Request new
            // raster sizes from the real font instead; only glyph caches change.
            var atlas = GardenTypography.Body;
            var label = Create<ScreenDensityText>(parent);
            var other = Create<Text>(parent);
            label.font = other.font = atlas;
            label.text = "새로운 글자 크기로 그리는 제목: 가나다라마바사 아자차카타파하 ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            other.text = "다른 버튼의 미뤄두기";
            other.fontSize = 17;
            label.rectTransform.sizeDelta = other.rectTransform.sizeDelta = new Vector2(5000, 4000);
            label.verticalOverflow = other.verticalOverflow = VerticalWrapMode.Overflow;
            int nestedRenders = 0, nestedVertices = 0;
            bool populating = false, nested = false;
            using var mesh = new VertexHelper();
            Action<Font> rebuilt = changed =>
            {
                if (changed != atlas || !populating || nested) return;
                nested = true;
                try
                {
                    PopulateLegacy.Invoke(other, new object[] { mesh });
                    nestedVertices = mesh.currentVertCount;
                    nestedRenders++;
                }
                finally { nested = false; }
            };
            Font.textureRebuilt += rebuilt;
            try
            {
                foreach (int size in new[] { 19, 31, 47, 67, 89, 113, 139, 167 })
                {
                    label.fontSize = size;
                    label.cachedTextGenerator.Invalidate();
                    populating = true;
                    try { Populate.Invoke(label, new object[] { mesh }); }
                    finally { populating = false; }
                    Require(mesh.currentVertCount == label.cachedTextGenerator.verts.Count,
                        "Font atlas callback left another label's vertices in the rendered mesh");
                    if (nestedRenders > 0) break;
                }
                Require(nestedRenders > 0, "Atlas regression fixture must exercise a synchronous nested text render");
                Require(nestedVertices > 0, "Nested label must contribute vertices to the shared mesh fixture");
                Debug.Log("SCREEN_DENSITY_ATLAS_REENTRANCY_PASS nestedRenders=" + nestedRenders);
            }
            finally
            {
                Font.textureRebuilt -= rebuilt;
                UnityEngine.Object.DestroyImmediate(label.gameObject);
                UnityEngine.Object.DestroyImmediate(other.gameObject);
            }
        }

        static T Create<T>(Transform parent) where T : Text
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<T>();
            label.font = GardenTypography.Body;
            Require(label.font != null && label.font.dynamic, "Dynamic project body font is required");
            label.rectTransform.pivot = new Vector2(0, 1);
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.lineSpacing = 1.2f;
            label.supportRichText = false;
            return label;
        }

        static Vector2Int Render(Text label)
        {
            var method = label is ScreenDensityText ? Populate : PopulateLegacy;
            using (var mesh = new VertexHelper()) method.Invoke(label, new object[] { mesh });
            return new Vector2Int(label.cachedTextGenerator.characterCountVisible, label.cachedTextGenerator.lineCount);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
