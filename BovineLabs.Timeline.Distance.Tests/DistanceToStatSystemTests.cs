using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Core.ObjectManagement;
using BovineLabs.Testing;
using BovineLabs.Timeline.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Distance.Data;
using BovineLabs.Timeline.EntityLinks.Data;
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
            Assert.AreEqual(1, modifiers[0].Value.Type.Value.ID());
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
                From = new EntityLinkRef { ReadRootFrom = Target.Self },
                To = new EntityLinkRef { ReadRootFrom = Target.Target },
                StatTarget = new EntityLinkRef { ReadRootFrom = Target.Self },
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
            // DistanceToStatSystem reads the EndSimulation ECB singleton (registered by the system's OnCreate) and
            // schedules jobs that write into its command buffer. Update that system afterwards to complete the
            // producer jobs and play the buffer back, mirroring the real frame order.
            var ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            World.GetOrCreateSystem<DistanceToStatSystem>().Update(WorldUnmanaged);
            ecbSystem.Update();
            Manager.CompleteAllTrackedJobs();
        }
    }
}