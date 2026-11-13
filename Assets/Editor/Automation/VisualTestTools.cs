using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CapstoneDesign.EditorTools
{
    public static class VisualTestTools
    {
        private const string ScenePath = "Assets/Scenes/MockupMain.unity";

        [MenuItem("Capstone Mockup/Capture Visual Preview")]
        public static void CaptureMockupPreview()
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ScenePath)))
            {
                MockupProjectBuilder.BuildMockup();
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = GameObject.Find("MockupCamera")?.GetComponent<Camera>();
            if (camera == null)
            {
                throw new InvalidOperationException("MockupCamera is missing from generated scene.");
            }

            string rendererName = SystemInfo.graphicsDeviceName ?? string.Empty;
            string rendererVendor = SystemInfo.graphicsDeviceVendor ?? string.Empty;
            string rendererText = (rendererName + " " + rendererVendor).ToLowerInvariant();
            Debug.Log("Visual renderer: " + SystemInfo.graphicsDeviceType + " / " + rendererName + " / vendor " + rendererVendor);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null ||
                rendererText.Contains("llvmpipe") ||
                rendererText.Contains("swiftshader") ||
                rendererText.Contains("software renderer"))
            {
                throw new InvalidOperationException("Visual preview selected a software/null renderer: " + rendererName);
            }
            const int width = 1200;
            const int height = 2000;
            RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "MockupPreviewRT",
                antiAliasing = 1
            };
            Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                image.Apply(false, false);
                byte[] png = image.EncodeToPNG();
                string artifacts = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
                if (string.IsNullOrWhiteSpace(artifacts))
                {
                    artifacts = "/artifacts";
                }

                string outputDirectory = Path.Combine(artifacts, "visual");
                Directory.CreateDirectory(outputDirectory);
                string outputPath = Path.Combine(outputDirectory, "mockup-preview.png");
                File.WriteAllBytes(outputPath, png);
                Debug.Log("Visual preview written to " + outputPath + " (" + png.Length + " bytes)");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }
    }
}
