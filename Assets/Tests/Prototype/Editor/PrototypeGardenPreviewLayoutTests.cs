using CapstoneDesign.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace CapstoneDesign.Tests
{
    public sealed class PrototypeGardenPreviewLayoutTests
    {
        [TestCase(1280)] [TestCase(1600)] [TestCase(1800)]
        public void HomeAreaUsesAvailableWidthAndHeight(int height)
        {
            float pageHeight = height - 236;
            Assert.That(PrototypeGardenPreviewLayout.TryGetArea(new Rect(0, -pageHeight, 720, pageHeight),
                -144, -pageHeight + 316, 8, 12, out var area), Is.True);
            Assert.That(area.width, Is.EqualTo(704));
            Assert.That(area.height, Is.EqualTo(pageHeight - 484));
            Assert.That(area.yMax, Is.LessThan(-144));
            Assert.That(area.yMin, Is.GreaterThan(-pageHeight + 316));
        }
        [TestCase(.5f)] [TestCase(.8f)] [TestCase(1)] [TestCase(1.4f)] [TestCase(2)]
        public void ProjectionMatchesDisplayAspectAndProtectsBothAxes(float aspect)
        {
            Assert.That(PrototypeGardenPreviewLayout.TryGetSize(aspect, 1.52f, 1.61f, .02f, out float size), Is.True);
            Assert.That(size * .96f, Is.GreaterThanOrEqualTo(1.61f - .0001f));
            Assert.That(size * aspect * .96f, Is.GreaterThanOrEqualTo(1.52f - .0001f));
            // Equal UI pixel density along X/Y: stretching the shared RT cannot distort the plant.
            float height = 800, width = height * aspect;
            Assert.That(width / (size * aspect * 2), Is.EqualTo(height / (size * 2)).Within(.001f));
        }
        [TestCase(1280, 1.04f)] [TestCase(1600, 1.35f)]
        public void LargerUiIncreasesPlantSizeWithoutCropping(int height, float minimumGain)
        {
            float available = height - 236 - 484;
            PrototypeGardenPreviewLayout.TryGetSize(704 / available, 1.52f, 1.61f, .02f, out float size);
            Assert.That((available / (2 * size)) / (540 / (2 * 1.7f)), Is.GreaterThan(minimumGain));
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidAspectCannotChangeCamera(float aspect)
        { Assert.That(PrototypeGardenPreviewLayout.TryGetSize(aspect, 1.52f, 1.61f, .02f, out _), Is.False); }
        [Test] public void OverlappingBoundariesDoNotProduceAViewport()
        { Assert.That(PrototypeGardenPreviewLayout.TryGetArea(new Rect(0, -500, 720, 500), -300, -250, 8, 12, out _), Is.False); }
        [Test] public void MarginIsBounded()
        {
            PrototypeGardenPreviewLayout.TryGetSize(1, 1, 1, 9, out float size);
            Assert.That(size, Is.EqualTo(1 / .6f).Within(.0001f));
        }
    }
}
