using UnityEngine;

namespace CapstoneDesign.Prototype
{
    // Screen.safeArea can lag a system bar revealed by the Android keyboard.
    // One sample per frame keeps both canvases on the same shell geometry.
    public static class AndroidSystemBarSafeArea
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        static readonly object Gate = new object();
        static int[] snapshot;
        static Vector2 sampleScreen;
        static bool pending;
        static int generation, lastFrame = -1;
        static float nextRead;
        static Rect frameSafe;
#endif

        public static void Invalidate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            lock (Gate) { snapshot = null; generation++; }
            nextRead = 0;
            lastFrame = -1;
#endif
        }

        public static Rect Current()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (lastFrame == Time.frameCount) return frameSafe;
            lastFrame = Time.frameCount;
            var screen = new Vector2(Screen.width, Screen.height);
            frameSafe = Screen.safeArea;
            lock (Gate)
            {
                if (sampleScreen == screen && snapshot != null)
                    frameSafe = Merge(screen, frameSafe, snapshot);
                if (pending || Time.realtimeSinceStartup < nextRead) return frameSafe;
                pending = true;
            }
            nextRead = Time.realtimeSinceStartup + .2f;
            int requestGeneration = generation;
            // Unity owns these wrappers. Only wrappers created inside the callback
            // are disposed; no Activity/View is retained by this helper.
            try
            {
                var activity = UnityEngine.Android.AndroidApplication.currentActivity;
                var player = UnityEngine.Android.AndroidApplication.unityPlayer;
                if (activity == null || player == null) { lock (Gate) pending = false; return frameSafe; }
                UnityEngine.Android.AndroidApplication.InvokeOnUIThread(() =>
                {
                    int[] result = null;
                    try
                    {
                        using var surface = player.Call<AndroidJavaObject>("getSurfaceView");
                        using var reader = new AndroidJavaClass("com.capstonedesign2026.platform.SystemBarInsets");
                        result = reader.CallStatic<int[]>("read", activity, surface);
                    }
                    catch (AndroidJavaException) { /* Keep Unity's safe area during detach/recreation. */ }
                    finally
                    {
                        lock (Gate)
                        {
                            if (requestGeneration == generation) { snapshot = result; sampleScreen = screen; }
                            pending = false;
                        }
                    }
                });
            }
            catch (AndroidJavaException) { lock (Gate) pending = false; }
            return frameSafe;
#else
            return Screen.safeArea;
#endif
        }

        // Android coordinates start at the window's top-left; Unity's origin is
        // the render surface's bottom-left. A resized surface has already consumed
        // some/all insets. Intersecting (rather than adding) also respects safeArea.
        // snapshot: window w/h, surface x/y/w/h, visible left/top/right/bottom.
        public static Rect Merge(Vector2 screen, Rect unitySafe, int[] bars)
        {
            if (bars == null || bars.Length != 10 || screen.x <= 0 || screen.y <= 0
                || bars[0] <= 0 || bars[1] <= 0 || bars[4] <= 0 || bars[5] <= 0) return unitySafe;
            float surfaceAspect = (float)bars[4] / bars[5];
            // Ignore a sample taken during rotation/resize until the surface and
            // Unity agree. Render-resolution scaling with the same aspect is valid.
            if (Mathf.Abs(surfaceAspect / (screen.x / screen.y) - 1) > .01f) return unitySafe;
            float left = Mathf.Clamp(bars[6] - bars[2], 0, bars[4]);
            float top = Mathf.Clamp(bars[7] - bars[3], 0, bars[5]);
            float right = Mathf.Clamp(bars[2] + bars[4] - (bars[0] - bars[8]), 0, bars[4]);
            float bottom = Mathf.Clamp(bars[3] + bars[5] - (bars[1] - bars[9]), 0, bars[5]);
            var safe = Rect.MinMaxRect(Mathf.Max(unitySafe.xMin, left * screen.x / bars[4]),
                Mathf.Max(unitySafe.yMin, bottom * screen.y / bars[5]),
                Mathf.Min(unitySafe.xMax, screen.x - right * screen.x / bars[4]),
                Mathf.Min(unitySafe.yMax, screen.y - top * screen.y / bars[5]));
            return safe.width > 0 && safe.height > 0 ? safe : unitySafe;
        }
    }
}
