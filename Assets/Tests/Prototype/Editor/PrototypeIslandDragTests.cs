using CapstoneDesign.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CapstoneDesign.Tests
{
    public sealed class PrototypeIslandDragTests
    {
        private GameObject surface, cameraObject;
        private PrototypeIslandDrag drag;
        private PointerEventData Pointer(int id, Vector2 position, PointerEventData.InputButton button = PointerEventData.InputButton.Left)
            => new PointerEventData(null) { pointerId = id, position = position, pressPosition = Vector2.zero, button = button };

        [SetUp] public void SetUp()
        {
            surface = new GameObject("Orbit test", typeof(RectTransform));
            ((RectTransform)surface.transform).sizeDelta = new Vector2(600, 540);
            cameraObject = new GameObject("Test camera", typeof(Camera));
            drag = surface.AddComponent<PrototypeIslandDrag>();
            drag.previewCamera = cameraObject.GetComponent<Camera>(); drag.ResetView();
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(surface); Object.DestroyImmediate(cameraObject); }

        [Test] public void CloserDefaultKeepsFocusAndViewingDirection()
        {
            Assert.That(drag.distance, Is.EqualTo(5.8f));
            Assert.That(drag.focus, Is.EqualTo(new Vector3(0, .58f, 0)));
            Assert.That(Vector3.Distance(drag.previewCamera.transform.position, drag.focus), Is.EqualTo(5.8f).Within(.001f));
            Assert.That(Quaternion.Angle(drag.previewCamera.transform.rotation, Quaternion.Euler(28, -25, 0)), Is.LessThan(.001f));
        }

        [TestCase(300, 270)] [TestCase(600, 540)] [TestCase(1200, 1080)]
        public void RelativeMotionHasIdenticalSensitivity(float width, float height)
        {
            Vector2 result = PrototypeIslandDrag.DragAngles(Vector2.zero, new Vector2(width / 4, height / 6), new Vector2(width, height));
            Assert.That(result.x, Is.EqualTo(90).Within(.001f)); Assert.That(result.y, Is.EqualTo(-30).Within(.001f));
        }
        [TestCase(150, 0, 65, 28)]
        [TestCase(-150, 0, -115, 28)]
        [TestCase(0, 54, -25, 10)]
        [TestCase(0, -54, -25, 46)]
        public void PointerDragUsesReversedDirectionOnBothAxes(float dx, float dy, float yaw, float pitch)
        {
            drag.OnBeginDrag(Pointer(1, Vector2.zero));
            drag.OnDrag(Pointer(1, new Vector2(dx, dy)));
            Assert.That(drag.Angles.x, Is.EqualTo(yaw).Within(.001f));
            Assert.That(drag.Angles.y, Is.EqualTo(pitch).Within(.001f));
        }
        [TestCase(10000, -85)] [TestCase(-10000, 85)]
        public void PitchSupportsTopAndUndersideWithoutFlipping(float delta, float expected)
        { Assert.That(PrototypeIslandDrag.DragAngles(Vector2.zero, new Vector2(0, delta), new Vector2(600,540)).y, Is.EqualTo(expected)); }
        [TestCase(-180)] [TestCase(0)] [TestCase(179)]
        public void FullHorizontalTurnReturnsToSameAngle(float yaw)
        { Assert.That(PrototypeIslandDrag.DragAngles(new Vector2(yaw, 28), new Vector2(600, 0), new Vector2(600, 540)).x, Is.EqualTo(yaw)); }
        [Test] public void InvalidViewportDoesNotChangeOrientation()
        { Assert.That(PrototypeIslandDrag.DragAngles(drag.Angles, Vector2.one, Vector2.zero), Is.EqualTo(drag.Angles)); }
        [Test] public void DragMovesCameraNotPlantAndReleaseStops()
        {
            Vector3 before = drag.previewCamera.transform.position;
            drag.OnBeginDrag(Pointer(1, Vector2.zero)); drag.OnDrag(Pointer(1, new Vector2(150, 54)));
            Assert.That(drag.IsDragging, Is.True); Assert.That(drag.Angles, Is.Not.EqualTo(drag.defaultAngles));
            Assert.That(Vector3.Distance(before, drag.previewCamera.transform.position), Is.GreaterThan(1));
            Assert.That(Vector3.Distance(drag.previewCamera.transform.position, drag.focus), Is.EqualTo(drag.distance).Within(.001f));
            Assert.That(Vector3.Dot(drag.previewCamera.transform.forward, (drag.focus - drag.previewCamera.transform.position).normalized), Is.GreaterThan(.999f));
            drag.OnEndDrag(Pointer(1, Vector2.zero)); var ended = drag.Angles;
            drag.OnDrag(Pointer(1, Vector2.one * 200)); Assert.That(drag.Angles, Is.EqualTo(ended));
        }
        [Test] public void SecondPointerCannotStealOrEndFirstDrag()
        {
            drag.OnBeginDrag(Pointer(1, Vector2.zero)); drag.OnBeginDrag(Pointer(2, Vector2.zero));
            drag.OnDrag(Pointer(2, Vector2.one * 150)); drag.OnEndDrag(Pointer(2, Vector2.zero));
            Assert.That(drag.Angles, Is.EqualTo(drag.defaultAngles)); Assert.That(drag.IsDragging, Is.True);
            drag.OnDrag(Pointer(1, Vector2.one * 30)); Assert.That(drag.Angles, Is.Not.EqualTo(drag.defaultAngles));
        }
        [Test] public void RightMouseDoesNotBeginRotation()
        { drag.OnBeginDrag(Pointer(-2, Vector2.zero, PointerEventData.InputButton.Right)); Assert.That(drag.IsDragging, Is.False); }
        [Test] public void PageExitCancelsDragAndUsesDefaultCameraThenRestoresHomeAngle()
        {
            Quaternion baseline = drag.previewCamera.transform.rotation;
            drag.OnBeginDrag(Pointer(1, Vector2.zero)); drag.OnDrag(Pointer(1, new Vector2(150, 30)));
            var angle = drag.Angles; var rotated = drag.previewCamera.transform.rotation;
            surface.SetActive(false);
            Lifecycle("OnDisable"); // Non-ExecuteAlways callbacks are driven explicitly in EditMode.
            Assert.That(drag.IsDragging, Is.False); Assert.That(Quaternion.Angle(drag.previewCamera.transform.rotation, baseline), Is.LessThan(.001f));
            drag.OnDrag(Pointer(1, Vector2.one * 200)); Assert.That(drag.Angles, Is.EqualTo(angle));
            surface.SetActive(true);
            Lifecycle("OnEnable");
            Assert.That(Quaternion.Angle(drag.previewCamera.transform.rotation, rotated), Is.LessThan(.001f));
            Assert.That(drag.IsDragging, Is.False);
        }
        [Test] public void FocusLossCancelsHeldPointer()
        {
            drag.OnBeginDrag(Pointer(1, Vector2.zero)); Lifecycle("OnApplicationFocus", false);
            Assert.That(drag.IsDragging, Is.False);
        }
        [Test] public void MissingCameraFailsSafely()
        { drag.previewCamera = null; drag.OnBeginDrag(Pointer(1, Vector2.zero)); Assert.That(drag.IsDragging, Is.False); }

        private void Lifecycle(string method, params object[] args)
            => typeof(PrototypeIslandDrag).GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(drag, args);
    }
}
