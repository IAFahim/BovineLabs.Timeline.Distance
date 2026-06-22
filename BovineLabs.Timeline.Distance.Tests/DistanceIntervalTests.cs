using BovineLabs.Timeline.Distance.Authoring;
using BovineLabs.Timeline.Distance.Data;
using NUnit.Framework;
using UnityEngine;

namespace BovineLabs.Timeline.Distance.Tests
{
    public class DistanceIntervalTests
    {
        [Test]
        public void Resolve_IntervalZero_ClampsToDefault()
        {
            Assert.AreEqual(DistanceInterval.Default, DistanceInterval.Resolve(DistanceUpdateMode.Interval, 0f));
        }

        [Test]
        public void Resolve_IntervalNegative_ClampsToDefault()
        {
            Assert.AreEqual(DistanceInterval.Default, DistanceInterval.Resolve(DistanceUpdateMode.Interval, -1f));
        }

        [Test]
        public void Resolve_IntervalPositive_Unchanged()
        {
            Assert.AreEqual(0.25f, DistanceInterval.Resolve(DistanceUpdateMode.Interval, 0.25f));
        }

        [Test]
        public void Resolve_ContinuousZero_Unchanged()
        {
            Assert.AreEqual(0f, DistanceInterval.Resolve(DistanceUpdateMode.Continuous, 0f));
        }

        [Test]
        public void Resolve_OnStartZero_Unchanged()
        {
            Assert.AreEqual(0f, DistanceInterval.Resolve(DistanceUpdateMode.OnStart, 0f));
        }

        [Test]
        public void Default_MatchesClipFieldDefault()
        {
            var clip = ScriptableObject.CreateInstance<DistanceToStatClip>();
            try
            {
                Assert.AreEqual(DistanceInterval.Default, clip.interval);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }
    }
}
