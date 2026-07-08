using BovineLabs.Essence.Data;
using BovineLabs.Timeline.Core;
using BovineLabs.Timeline.Distance.Data;
using BovineLabs.Nerve.ObjectManagement;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Distance.Tests
{
    public class DistanceSamplingTests
    {
        [Test]
        public void ShouldSample_OnStart_FiresOnlyOnFirstFrame_TimerUnchanged()
        {
            var firstFrame = DistanceSampling.ShouldSample(DistanceUpdateMode.OnStart, true, 1f, 0.1f, 0.7f,
                out var firstTimer);
            Assert.IsTrue(firstFrame);
            Assert.AreEqual(0.7f, firstTimer);

            var laterFrame = DistanceSampling.ShouldSample(DistanceUpdateMode.OnStart, false, 1f, 0.1f, 0.7f,
                out var laterTimer);
            Assert.IsFalse(laterFrame);
            Assert.AreEqual(0.7f, laterTimer);
        }

        [Test]
        public void ShouldSample_Continuous_AlwaysFires_TimerUnchanged()
        {
            Assert.IsTrue(DistanceSampling.ShouldSample(DistanceUpdateMode.Continuous, false, 1f, 0.1f, 0.42f,
                out var timer));
            Assert.AreEqual(0.42f, timer);
        }

        [Test]
        public void ShouldSample_Interval_AccumulatesAndCarriesRemainder()
        {
            const float interval = 0.5f;
            const float dt = 0.2f;
            var timer = 0f;

            var f1 = DistanceSampling.ShouldSample(DistanceUpdateMode.Interval, false, interval, dt, timer,
                out timer);
            Assert.IsFalse(f1);
            Assert.AreEqual(0.2f, timer, 1e-5f);

            var f2 = DistanceSampling.ShouldSample(DistanceUpdateMode.Interval, false, interval, dt, timer,
                out timer);
            Assert.IsFalse(f2);
            Assert.AreEqual(0.4f, timer, 1e-5f);

            var priorBeforeFire = timer;
            var f3 = DistanceSampling.ShouldSample(DistanceUpdateMode.Interval, false, interval, dt, timer,
                out timer);
            Assert.IsTrue(f3);
            Assert.AreEqual(priorBeforeFire + dt - interval, timer, 1e-5f);
            Assert.AreNotEqual(0f, timer);
        }

        [Test]
        public void ShouldSample_Interval_FirstFrameResetsTimerAndFires()
        {
            var fired = DistanceSampling.ShouldSample(DistanceUpdateMode.Interval, true, 0.5f, 0.2f, 0.9f,
                out var timer);
            Assert.IsTrue(fired);
            Assert.AreEqual(0f, timer);
        }

        [Test]
        public void ShouldSample_DefaultMode_ReturnsFalse()
        {
            var mode = (DistanceUpdateMode)200;
            Assert.IsFalse(DistanceSampling.ShouldSample(mode, true, 1f, 0.1f, 0.3f, out var timer));
            Assert.AreEqual(0.3f, timer);
        }

        [Test]
        public void TryComputeModifier_RoundsDistanceTimesMultiplier()
        {
            var statKey = new StatKey { Value = new BovineLabs.Core.BLId(7) };

            Assert.IsTrue(DistanceSampling.TryComputeModifier(float3.zero, new float3(5f, 0f, 0f), 1f, 1f, statKey,
                out var m1));
            Assert.AreEqual(5, m1.Value);
            Assert.AreEqual(StatModifyType.Added, m1.ModifyType);
            Assert.AreEqual(7, m1.Type.Value.ID);

            Assert.IsTrue(DistanceSampling.TryComputeModifier(float3.zero, new float3(1.5f, 0f, 0f), 100f, 1f, statKey,
                out var m2));
            Assert.AreEqual(150, m2.Value);
            Assert.AreEqual(StatModifyType.Added, m2.ModifyType);
        }

        [Test]
        public void TryComputeModifier_ScalesByWeight()
        {
            var statKey = new StatKey { Value = new BovineLabs.Core.BLId(7) };
            var to = new float3(2f, 0f, 0f);

            // weight 0 -> no contribution; 0.5 -> half; 1 -> full (2m x100 = 200).
            Assert.IsTrue(DistanceSampling.TryComputeModifier(float3.zero, to, 100f, 0f, statKey, out var zero));
            Assert.AreEqual(0, zero.Value);

            Assert.IsTrue(DistanceSampling.TryComputeModifier(float3.zero, to, 100f, 0.5f, statKey, out var half));
            Assert.AreEqual(100, half.Value);

            Assert.IsTrue(DistanceSampling.TryComputeModifier(float3.zero, to, 100f, 1f, statKey, out var full));
            Assert.AreEqual(200, full.Value);
        }

        [Test]
        public void TryComputeModifier_NonFinite_ReturnsFalse()
        {
            var statKey = new StatKey { Value = new BovineLabs.Core.BLId(1) };

            Assert.IsFalse(DistanceSampling.TryComputeModifier(float3.zero, new float3(float.NaN, 0f, 0f), 1f,
                1f, statKey, out _));
            Assert.IsFalse(DistanceSampling.TryComputeModifier(float3.zero, new float3(float.PositiveInfinity, 0f, 0f),
                1f, 1f, statKey, out _));
            Assert.IsFalse(DistanceSampling.TryComputeModifier(float3.zero, new float3(5f, 0f, 0f),
                float.PositiveInfinity, 1f, statKey, out _));
        }

        [Test]
        public void ShouldDropModifier_SameSource_DropsEvenWhenNotExisting()
        {
            var source = new Entity { Index = 3, Version = 1 };
            Assert.IsTrue(DistanceSampling.ShouldDropModifier(source, source, false));
        }

        [Test]
        public void ShouldDropModifier_StaleNonNullSource_Drops()
        {
            var entrySource = new Entity { Index = 3, Version = 1 };
            var mutationSource = new Entity { Index = 9, Version = 1 };
            Assert.IsTrue(DistanceSampling.ShouldDropModifier(entrySource, mutationSource, false));
        }

        [Test]
        public void ShouldDropModifier_NullSource_NeverDrops()
        {
            var mutationSource = new Entity { Index = 9, Version = 1 };
            Assert.IsFalse(DistanceSampling.ShouldDropModifier(Entity.Null, mutationSource, false));
            Assert.IsFalse(DistanceSampling.ShouldDropModifier(Entity.Null, mutationSource, true));
        }

        [Test]
        public void ShouldDropModifier_LiveDifferentSource_Keeps()
        {
            var entrySource = new Entity { Index = 3, Version = 1 };
            var mutationSource = new Entity { Index = 9, Version = 1 };
            Assert.IsFalse(DistanceSampling.ShouldDropModifier(entrySource, mutationSource, true));
        }
    }
}
