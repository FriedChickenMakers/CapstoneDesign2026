using CapstoneDesign.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Tests
{
    public sealed class GardenThemeSurfaceTests
    {
        [TestCase(GardenThemeSurface.Style.Paper)]
        [TestCase(GardenThemeSurface.Style.Wood)]
        [TestCase(GardenThemeSurface.Style.Primary)]
        [TestCase(GardenThemeSurface.Style.Selected)]
        [TestCase(GardenThemeSurface.Style.Backdrop)]
        [TestCase(GardenThemeSurface.Style.Header)]
        [TestCase(GardenThemeSurface.Style.Shelf)]
        [TestCase(GardenThemeSurface.Style.Input)]
        [TestCase(GardenThemeSurface.Style.ProgressTrack)]
        [TestCase(GardenThemeSurface.Style.ProgressFill)]
        public void ApplyingAndRestylingDecorationDoesNotAddInputOrLayoutTargets(GardenThemeSurface.Style style)
        {
            var owner = new GameObject("Synthetic control", typeof(RectTransform), typeof(Image), typeof(Button));
            try
            {
                var rect = (RectTransform)owner.transform;
                rect.sizeDelta = new Vector2(420, 96);
                var button = owner.GetComponent<Button>();
                button.targetGraphic = owner.GetComponent<Image>();
                int calls = 0;
                button.onClick.AddListener(() => calls++);
                var label = new GameObject("Label", typeof(RectTransform));
                label.transform.SetParent(owner.transform, false);
                var surface = GardenThemeSurface.Apply(owner, style);
                for (int repeat = 0; repeat < 5; repeat++) GardenThemeSurface.Apply(owner, style);
                Assert.That(owner.GetComponentsInChildren<GardenThemeSurface>().Length, Is.EqualTo(1), "Rebuilding a page must not accumulate decoration.");
                Assert.That(surface.raycastTarget, Is.False, "Decorative surfaces must never intercept touches.");
                Assert.That(surface.GetComponent<LayoutElement>().ignoreLayout, Is.True, "Decoration must not add rows to vertical layouts.");
                Assert.That(surface.transform.GetSiblingIndex(), Is.LessThan(label.transform.GetSiblingIndex()), "Decoration must remain behind content.");
                Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(420, 96)), "Styling must preserve the control's touch rectangle.");
                Assert.That(button.interactable, Is.True);
                button.onClick.Invoke();
                Assert.That(calls, Is.EqualTo(1), "Styling must preserve existing navigation or mission listeners.");
                button.interactable = false;
                GardenThemeSurface.Apply(owner, GardenThemeSurface.Style.Selected);
                Assert.That(button.interactable, Is.False, "Restyling cannot unlock a disabled activity.");
                Assert.That(owner.GetComponentsInChildren<GardenThemeSurface>().Length, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
