using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CapstoneDesign.Runtime
{
    // The garden renders into its own HDR texture before the overlay UI is
    // composed. Only that camera samples this volume, so text and cards stay crisp.
    public static class GardenPostProcessing
    {
        public const int VolumeLayer = 30;
        const string VolumeName = "Garden bloom and color grading";

        public static Volume Ensure(Camera camera, Transform visualRoot)
        {
            if (camera == null || visualRoot == null) return null;
            var profile = Resources.Load<VolumeProfile>("GardenAtmosphere/PostProcessing");
            if (profile == null)
            {
                Debug.LogError("Garden post-processing profile is missing.");
                return null;
            }

            camera.allowHDR = true;
            // An HDR MSAA target would multiply mobile memory/bandwidth. FXAA
            // runs in the final garden pass and never filters the overlay UI.
            camera.allowMSAA = false;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = 1 << VolumeLayer;
            cameraData.volumeTrigger = camera.transform;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cameraData.dithering = true;

            var node = visualRoot.Find(VolumeName);
            if (node == null)
            {
                node = new GameObject(VolumeName).transform;
                node.SetParent(visualRoot, false);
                node.gameObject.layer = VolumeLayer;
            }
            var volume = node.GetComponent<Volume>() ?? node.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            volume.weight = 1;
            // A serialized Resources profile also preserves the required URP
            // bloom/tonemapping shader variants when building an Android player.
            volume.sharedProfile = profile;
            return volume;
        }
    }
}
