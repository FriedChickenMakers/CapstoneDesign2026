using CapstoneDesign.Prototype;
using System.Linq;
using System.Reflection;
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
        [TestCase(GardenThemeSurface.Style.TabWood)]
        [TestCase(GardenThemeSurface.Style.Home)]
        [TestCase(GardenThemeSurface.Style.NavigationLeaf)]
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

        [TestCase(GardenThemeSurface.Style.Wood, "NavigationBackingUI")]
        [TestCase(GardenThemeSurface.Style.TabWood, "NavigationBackingUI")]
        [TestCase(GardenThemeSurface.Style.Header, "Wood062LitUI")]
        [TestCase(GardenThemeSurface.Style.Shelf, "Wood062LitUI")]
        [TestCase(GardenThemeSurface.Style.Backdrop, "Wood096Background")]
        [TestCase(GardenThemeSurface.Style.Primary, "LeafSet017UI")]
        [TestCase(GardenThemeSurface.Style.Home, "LeafSet024UI")]
        [TestCase(GardenThemeSurface.Style.NavigationLeaf, "LeafSet024DenseUI")]
        public void TextureRestylingUsesTheImportedResource(GardenThemeSurface.Style style, string resource)
        {
            var owner = new GameObject("Synthetic texture control", typeof(RectTransform));
            try
            {
                var texture = Resources.Load<Texture2D>("UI/Textures/" + resource);
                Assert.That(texture, Is.Not.Null, "The actual PNG must be imported, not an LFS pointer.");
                Assert.That(texture.width, Is.GreaterThan(64));
                var surface = GardenThemeSurface.Apply(owner, GardenThemeSurface.Style.Paper);
                Assert.That(GardenThemeSurface.Apply(owner, style), Is.SameAs(surface));
                Assert.That(surface.mainTexture, Is.SameAs(texture));
                GardenThemeSurface.Apply(owner, GardenThemeSurface.Style.Paper);
                Assert.That(surface.mainTexture, Is.Not.SameAs(texture), "A reused surface must not retain a previous wood/leaf texture.");
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [TestCase(390f, 84f)]
        [TestCase(768f, 84f)]
        public void NavigationShelfCoversEveryCornerWithAnOpaqueFace(float width, float height)
        {
            var owner = new GameObject("Synthetic full-width shelf", typeof(RectTransform));
            var mesh = new Mesh();
            try
            {
                ((RectTransform)owner.transform).sizeDelta = new Vector2(width, height);
                var surface = GardenThemeSurface.Apply(owner, GardenThemeSurface.Style.Shelf);
                using (var vertices = new VertexHelper())
                {
                    typeof(GardenThemeSurface).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance,
                        null, new[] { typeof(VertexHelper) }, null)
                        .Invoke(surface, new object[] { vertices });
                    vertices.FillMesh(mesh);
                }
                var r = surface.rectTransform.rect;
                var positions = mesh.vertices;
                var colors = mesh.colors32;
                foreach (var corner in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin),
                    new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax) })
                    Assert.That(Enumerable.Range(0, positions.Length).Any(i =>
                        Vector2.Distance(positions[i], corner) < .01f && colors[i].a == 255), Is.True,
                        "Rounded or translucent corner geometry must not expose the page behind the bottom bar.");
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(owner); }
        }

        [Test]
        public void WoodInkStopsAtAnInnerReadingSurface()
        {
            var owner = new GameObject("Synthetic wood", typeof(RectTransform));
            try
            {
                GardenThemeSurface.Apply(owner, GardenThemeSurface.Style.Wood);
                Assert.That(GardenThemeSurface.Foreground(owner.transform, Color.black), Is.EqualTo(GardenThemeSurface.WoodInk));
                var card = new GameObject("Reading card", typeof(RectTransform));
                card.transform.SetParent(owner.transform, false);
                GardenThemeSurface.Apply(card, GardenThemeSurface.Style.Paper);
                Assert.That(GardenThemeSurface.Foreground(card.transform, Color.black), Is.EqualTo(Color.black),
                    "A nested paper record must retain dark text instead of inheriting white wood text.");
                Assert.That((int)GardenThemeSurface.Style.TabWood, Is.EqualTo(10), "Keep the prior serialized style value.");
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
