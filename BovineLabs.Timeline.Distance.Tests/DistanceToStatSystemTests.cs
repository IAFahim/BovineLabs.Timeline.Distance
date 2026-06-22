using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Testing;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Distance.Data;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Distance.Tests
{
    public class DistanceToStatSystemTests : ECSTestsFixture
    {
        [Test]
        public void Continuous_WritesRoundedDistanceAsStatModifier()
        {
            var bound = CreateStatBody(float3.zero);
            var target = CreatePoint(new float3(5f, 0f, 0f));
            BindTarget(bound, target);
            CreateClip(bound, new StatKey { Value = 1 }, DistanceUpdateMode.Continuous, 1f);

            RunSystem();

            var modifiers = Manager.GetBuffer<StatModifiers>(bound);
            Assert.AreEqual(1, modifiers.Length);
            Assert.AreEqual(5, modifiers[0].Value.Value);
            Assert.AreEqual(1, (int)modifiers[0].Value.Type.Value);
        }

        [Test]
        public void Multiplier_ScalesDistance()
        {
            var bound = CreateStatBody(float3.zero);
            var target = CreatePoint(new float3(0f, 10f, 0f));
            BindTarget(bound, target);
            CreateClip(bound, new StatKey { Value = 2 }, DistanceUpdateMode.Continuous, 0.5f);

            RunSystem();

            var modifiers = Manager.GetBuffer<StatModifiers>(bound);
            Assert.AreEqual(1, modifiers.Length);
            Assert.AreEqual(5, modifiers[0].Value.Value);
        }

        [Test]
        public void ZeroStatKey_WritesNothing()
        {
            var bound = CreateStatBody(float3.zero);
            var target = CreatePoint(new float3(5f, 0f, 0f));
            BindTarget(bound, target);
            CreateClip(bound, default, DistanceUpdateMode.Continuous, 1f);

            RunSystem();

            Assert.AreEqual(0, Manager.GetBuffer<StatModifiers>(bound).Length);
        }

        private Entity CreateStatBody(float3 position)
        {
            var entity = Manager.CreateEntity();
            Manager.AddComponentData(entity, new LocalToWorld { Value = float4x4.Translate(position) });
            Manager.AddComponentData(entity, default(Targets));
            Manager.AddBuffer<StatModifiers>(entity);
            Manager.AddComponent<StatChanged>(entity);
            Manager.SetComponentEnabled<StatChanged>(entity, false);
            return entity;
        }

        private Entity CreatePoint(float3 position)
        {
            var entity = Manager.CreateEntity();
            Manager.AddComponentData(entity, new LocalToWorld { Value = float4x4.Translate(position) });
            return entity;
        }

        private void BindTarget(Entity bound, Entity target)
        {
            Manager.SetComponentData(bound, new Targets { Target = target });
        }

        private void CreateClip(Entity bound, StatKey statKey, DistanceUpdateMode mode, float multiplier)
        {
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, new TrackBinding { Value = bound });
            Manager.AddComponentData(clip, new DistanceToStatData
            {
                From = Target.Self,
                To = Target.Target,
                StatTarget = Target.Self,
                StatKey = statKey,
                Mode = mode,
                Multiplier = multiplier
            });
            Manager.AddComponentData(clip, default(DistanceToStatState));
            Manager.AddComponent<ClipActive>(clip);
            Manager.AddComponent<ClipActivePrevious>(clip);
        }

        private void RunSystem()
        {
            World.GetOrCreateSystem<DistanceToStatSystem>().Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }
    }
}