using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CapstoneDesign.Tests
{
    public sealed class GardenVisualClockTests
    {
        static Type Clock => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CapstoneDesign.Runtime.GardenVisualClock")).First(t => t != null);
        static Type Dial => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CapstoneDesign.Runtime.CircularTimeSlider")).First(t => t != null);
        static void SetMinutes(int value) => Clock.GetMethod("SetOverrideMinutes").Invoke(null, new object[] { value });
        static double Current => (double)Clock.GetProperty("CurrentHour").GetValue(null);
        static object Override => Clock.GetProperty("OverrideHour").GetValue(null);
        static int MinuteAt(Vector2 local, int step) => (int)Dial.GetMethod("MinuteAt").Invoke(null, new object[] { local, step });
        [TearDown] public void Reset() => Clock.GetMethod("UseSystemTime").Invoke(null, null);

        [TestCase(0, 1, 0)]
        [TestCase(1, 0, 360)]
        [TestCase(0, -1, 720)]
        [TestCase(-1, 0, 1080)]
        public void DialRunsClockwiseForFullDay(float x, float y, int minute) =>
            Assert.AreEqual(minute, MinuteAt(new Vector2(x, y), 15));

        [Test]
        public void MidnightSeamRoundsAndWrapsInBothDirections()
        {
            Assert.AreEqual(0, MinuteAt(new Vector2(-.001f, 1), 5));
            Assert.AreEqual(0, MinuteAt(new Vector2(.001f, 1), 15));
            float angle = 1439 / 1440f * Mathf.PI * 2;
            Assert.AreEqual(1439, MinuteAt(new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)), 1));
        }

        [Test]
        public void OverrideWrapsAndExplicitResetReturnsToSystemClock()
        {
            SetMinutes(245);
            Assert.AreEqual(245, Clock.GetProperty("CurrentMinute").GetValue(null));
            SetMinutes(-1);
            Assert.AreEqual(1439 / 60d, Current);
            SetMinutes(1440);
            Assert.AreEqual(0, Override);
            Reset();
            Assert.IsNull(Override);
            Assert.That(Current, Is.InRange(0d, 24d));
        }

        [Test]
        public void ClockSignalsOnlyChangedPresentationValues()
        {
            Reset();
            int changed = 0;
            System.Action count = () => changed++;
            Clock.GetEvent("Changed").AddEventHandler(null, count);
            try
            {
                SetMinutes(720); SetMinutes(720);
                Reset(); Reset();
                Assert.AreEqual(2, changed);
            }
            finally { Clock.GetEvent("Changed").RemoveEventHandler(null, count); }
        }
    }
}
