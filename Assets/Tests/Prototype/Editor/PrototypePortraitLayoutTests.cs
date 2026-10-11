using CapstoneDesign.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace CapstoneDesign.Tests
{
    public sealed class PrototypePortraitLayoutTests
    {
        [TestCase(360,640,0,0,0,0)]
        [TestCase(360,800,0,0,0,0)]
        [TestCase(390,844,0,34,0,47)]
        [TestCase(412,915,0,24,0,32)]
        [TestCase(720,1280,0,0,0,0)]
        [TestCase(720,1600,0,40,0,80)]
        [TestCase(1080,2400,0,90,0,120)]
        [TestCase(768,1024,0,20,0,24)]
        [TestCase(1920,1080,80,24,30,40)]
        public void SharedFrameFillsSafeHeightWithoutCropping(int width,int height,int left,int bottom,int right,int top)
        {
            var screen=new Vector2(width,height);
            var canvas=screen*.73f; // CanvasScaler need not map one unit to one pixel.
            var safe=new Rect(left,bottom,width-left-right,height-bottom-top);
            Assert.That(PrototypePortraitLayout.TryCalculate(screen,canvas,safe,new Vector2(720,1280),
                out var size,out float scale,out var position),Is.True);
            var pixelSize=size*scale/.73f;
            var center=position/.73f+screen*.5f;
            Assert.That(size.x,Is.EqualTo(720).Within(.01));
            Assert.That(size.y,Is.GreaterThanOrEqualTo(1280));
            Assert.That(pixelSize.y,Is.EqualTo(safe.height).Within(.01));
            Assert.That(center.y-pixelSize.y*.5f,Is.EqualTo(safe.yMin).Within(.01));
            Assert.That(center.y+pixelSize.y*.5f,Is.EqualTo(safe.yMax).Within(.01));
            Assert.That(center.x-pixelSize.x*.5f,Is.GreaterThanOrEqualTo(safe.xMin-.01));
            Assert.That(center.x+pixelSize.x*.5f,Is.LessThanOrEqualTo(safe.xMax+.01));
        }

        [TestCase(1280)] [TestCase(1440)] [TestCase(1600)] [TestCase(1800)]
        public void TallerPortraitAddsSpaceInsteadOfMovingToAFixedHeightInset(int height)
        {
            var screen=new Vector2(720,height);
            Assert.That(PrototypePortraitLayout.TryCalculate(screen,screen,new Rect(0,0,720,height),
                new Vector2(720,1280),out var size,out float scale,out var position),Is.True);
            Assert.That(size,Is.EqualTo(screen)); Assert.That(scale,Is.EqualTo(1));
            Assert.That(position,Is.EqualTo(Vector2.zero));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void InvalidTransientDimensionsDoNotProduceAnInvalidTransform(int invalid)
        {
            var screen=new Vector2(720,1280);var canvas=screen;var reference=screen;
            var safe=new Rect(0,0,720,1280);
            if(invalid==0)screen=Vector2.zero;
            if(invalid==1)canvas=Vector2.zero;
            if(invalid==2)reference=Vector2.zero;
            if(invalid==3)safe=new Rect(0,0,0,0);
            Assert.That(PrototypePortraitLayout.TryCalculate(screen,canvas,safe,reference,
                out _,out _,out _),Is.False);
        }

        [Test] public void SafeAreaIsClampedToTheScreen()
        {
            var screen=new Vector2(720,1600);
            Assert.That(PrototypePortraitLayout.TryCalculate(screen,screen,new Rect(-10,-20,760,1650),
                new Vector2(720,1280),out var size,out _,out var position),Is.True);
            Assert.That(size,Is.EqualTo(screen)); Assert.That(position,Is.EqualTo(Vector2.zero));
        }

        [Test] public void VisibleAndroidNavigationBarReservesItsActualHeight()
        {
            var screen = new Vector2(1200, 2000);
            var safe = AndroidSystemBarSafeArea.Merge(screen, new Rect(0, 0, 1200, 2000),
                new[] { 1200, 2000, 0, 0, 1200, 2000, 0, 0, 0, 72 });
            Assert.That(safe, Is.EqualTo(new Rect(0, 72, 1200, 1928)));
            Assert.That(PrototypePortraitLayout.TryCalculate(screen, screen, safe, new Vector2(720, 1280),
                out var size, out float scale, out var position), Is.True);
            // Entire common shell, including the tab card, stays above the bar.
            Assert.That(1000 + position.y - size.y * scale / 2, Is.EqualTo(72).Within(.01));
        }

        [Test] public void UnitySafeAreaAndAndroidBarAreIntersectedWithoutAddingInsets()
        {
            var screen = new Vector2(1200, 2000);
            var safe = new Rect(0, 72, 1200, 1880); // Also preserve Unity's top cutout.
            Assert.That(AndroidSystemBarSafeArea.Merge(screen, safe,
                new[] { 1200, 2000, 0, 0, 1200, 2000, 0, 0, 0, 72 }), Is.EqualTo(safe));
        }

        [Test] public void APlayerSurfaceAlreadyAboveTheBarDoesNotLoseHeightAgain()
        {
            var screen = new Vector2(1200, 1928);
            var safe = new Rect(0, 0, 1200, 1928);
            Assert.That(AndroidSystemBarSafeArea.Merge(screen, safe,
                new[] { 1200, 2000, 0, 0, 1200, 1928, 0, 0, 0, 72 }), Is.EqualTo(safe));
        }

        [Test] public void OffsetAndScaledRenderSurfaceUsesOnlyRemainingInsets()
        {
            // A window has already consumed 24 px at the top and bottom; the
            // half-resolution Unity buffer must reserve just the remaining 48 px.
            var screen = new Vector2(600, 976);
            Assert.That(AndroidSystemBarSafeArea.Merge(screen, new Rect(0, 0, 600, 976),
                new[] { 1200, 2000, 0, 24, 1200, 1952, 0, 24, 0, 72 }),
                Is.EqualTo(new Rect(0, 24, 600, 952)));
        }

        [Test] public void LandscapeSideNavigationBarKeepsBothEdgesUsable()
        {
            var screen = new Vector2(2000, 1200);
            Assert.That(AndroidSystemBarSafeArea.Merge(screen, new Rect(40, 0, 1960, 1200),
                new[] { 2000, 1200, 0, 0, 2000, 1200, 0, 0, 72, 0 }),
                Is.EqualTo(new Rect(40, 0, 1888, 1200)));
        }

        [Test] public void HiddenBarsRestoreTheFullPlayerArea()
        {
            var screen = new Vector2(1200, 2000);
            var safe = new Rect(0, 0, 1200, 2000);
            Assert.That(AndroidSystemBarSafeArea.Merge(screen, safe,
                new[] { 1200, 2000, 0, 0, 1200, 2000, 0, 0, 0, 0 }), Is.EqualTo(safe));
        }

        [Test] public void StaleRotationAndDetachedSurfaceSamplesKeepUnitySafeArea()
        {
            var screen = new Vector2(1200, 2000);
            var safe = new Rect(0, 24, 1200, 1976);
            Assert.That(AndroidSystemBarSafeArea.Merge(screen, safe,
                new[] { 2000, 1200, 0, 0, 2000, 1200, 0, 0, 72, 0 }), Is.EqualTo(safe));
            Assert.That(AndroidSystemBarSafeArea.Merge(screen, safe,
                new[] { 1200, 2000, 0, 0, 0, 0, 0, 0, 0, 72 }), Is.EqualTo(safe));
            Assert.That(AndroidSystemBarSafeArea.Merge(screen, safe, null), Is.EqualTo(safe));
        }
    }
}
