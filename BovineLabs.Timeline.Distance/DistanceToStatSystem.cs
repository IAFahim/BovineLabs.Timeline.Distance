using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Distance.Data;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace BovineLabs.Timeline.Distance
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct DistanceToStatSystem : ISystem
    {
        private struct StatMutation
        {
            public Entity Target;
            public Entity Source;
            public StatModifier Modifier;
            public bool IsRemove;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var mutations = new NativeQueue<StatMutation>(state.WorldUpdateAllocator);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Attach/sync a cleanup shadow so a clip destroyed mid-active still removes its modifier.
            state.Dependency = new AttachCleanupJob { ECB = ecb.AsParallelWriter() }.ScheduleParallel(state.Dependency);
            state.Dependency = new SyncCleanupJob().ScheduleParallel(state.Dependency);

            state.Dependency = new GatherActiveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Mutations = mutations.AsParallelWriter(),
                TargetsLookup = state.GetUnsafeComponentLookup<Targets>(true),
                LtwLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true),
                Sources = state.GetUnsafeComponentLookup<EntityLinkSource>(true),
                Entries = state.GetUnsafeBufferLookup<EntityLinkEntry>(true)
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new GatherRemoveJob
            {
                Mutations = mutations.AsParallelWriter()
            }.ScheduleParallel(state.Dependency);

            // Zombie clip entities (State gone, Cleanup retained) enqueue their modifier removal, then shed cleanup.
            state.Dependency = new GatherDestroyedJob
            {
                Mutations = mutations.AsParallelWriter(),
                ECB = ecb.AsParallelWriter()
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ApplyJob
            {
                Mutations = mutations,
                StatModifiers = SystemAPI.GetBufferLookup<StatModifiers>(),
                StatChangeds = SystemAPI.GetComponentLookup<StatChanged>(),
                StorageInfo = SystemAPI.GetEntityStorageInfoLookup()
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(DistanceToStatState))]
        [WithNone(typeof(DistanceToStatCleanup))]
        private partial struct AttachCleanupJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity)
            {
                ECB.AddComponent(sortKey, entity, new DistanceToStatCleanup { Target = Entity.Null });
            }
        }

        [BurstCompile]
        private partial struct SyncCleanupJob : IJobEntity
        {
            private void Execute(in DistanceToStatState state, ref DistanceToStatCleanup cleanup)
            {
                cleanup.Target = state.AppliedTarget;
            }
        }

        [BurstCompile]
        [WithNone(typeof(DistanceToStatState))]
        private partial struct GatherDestroyedJob : IJobEntity
        {
            public NativeQueue<StatMutation>.ParallelWriter Mutations;
            public EntityCommandBuffer.ParallelWriter ECB;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity, in DistanceToStatCleanup cleanup)
            {
                if (cleanup.Target != Entity.Null)
                {
                    Mutations.Enqueue(new StatMutation { Target = cleanup.Target, Source = entity, IsRemove = true });
                }

                ECB.RemoveComponent<DistanceToStatCleanup>(sortKey, entity);
            }
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct GatherActiveJob : IJobEntity
        {
            public float DeltaTime;
            public NativeQueue<StatMutation>.ParallelWriter Mutations;

            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> Sources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Entries;

            private void Execute(Entity clipEntity, in TrackBinding binding, in DistanceToStatData data,
                ref DistanceToStatState state, EnabledRefRO<ClipActivePrevious> activePrev)
            {
                if (binding.Value == Entity.Null || data.StatKey.Value == 0) return;
                if (!TargetsLookup.TryGetComponent(binding.Value, out var targets)) return;

                var isFirstFrame = !activePrev.ValueRO;

                // OnStart normally fires only on the enter edge; if resolution transiently fails that one frame the
                // sample would be lost for the whole clip. Retry until the first successful apply (AppliedTarget set).
                var onStartPending = data.Mode == DistanceUpdateMode.OnStart && state.AppliedTarget == Entity.Null;

                var shouldSample = DistanceSampling.ShouldSample(data.Mode, isFirstFrame || onStartPending, data.Interval,
                    DeltaTime, state.Timer, out var newTimer);
                state.Timer = newTimer; // persist unconditionally so Interval mode actually accumulates on skip frames
                if (!shouldSample) return;

                var fromEntity = ResolveTarget(binding.Value, data.From, data.FromLinkKey, in targets, Sources,
                    Entries);
                var toEntity = ResolveTarget(binding.Value, data.To, data.ToLinkKey, in targets, Sources, Entries);
                var statEntity = ResolveTarget(binding.Value, data.StatTarget, data.StatLinkKey, in targets, Sources,
                    Entries);

                if (fromEntity == Entity.Null || toEntity == Entity.Null || statEntity == Entity.Null) return;
                if (!LtwLookup.TryGetComponent(fromEntity, out var fromLtw) ||
                    !LtwLookup.TryGetComponent(toEntity, out var toLtw)) return;

                if (!DistanceSampling.TryComputeModifier(fromLtw.Position, toLtw.Position, data.Multiplier,
                        data.StatKey, out var modifier)) return;

                if (state.AppliedTarget != Entity.Null && state.AppliedTarget != statEntity)
                    Mutations.Enqueue(new StatMutation
                    {
                        Target = state.AppliedTarget,
                        Source = clipEntity,
                        IsRemove = true
                    });

                state.AppliedTarget = statEntity;

                Mutations.Enqueue(new StatMutation
                {
                    Target = statEntity,
                    Source = clipEntity,
                    Modifier = modifier,
                    IsRemove = false
                });
            }

            private static Entity ResolveTarget(
                Entity self, Target mode, ushort linkKey,
                in Targets targets,
                in UnsafeComponentLookup<EntityLinkSource> sources,
                in UnsafeBufferLookup<EntityLinkEntry> entries)
            {
                if (linkKey != 0)
                    return EntityLinkResolver.TryResolve(self, targets, mode, linkKey, sources, entries, out var linked)
                        ? linked
                        : Entity.Null;
                return targets.Get(mode, self);
            }
        }

        [BurstCompile]
        [WithDisabled(typeof(ClipActive))]
        [WithAll(typeof(ClipActivePrevious))]
        private partial struct GatherRemoveJob : IJobEntity
        {
            public NativeQueue<StatMutation>.ParallelWriter Mutations;

            private void Execute(Entity clipEntity, ref DistanceToStatState state)
            {
                if (state.AppliedTarget == Entity.Null) return;

                Mutations.Enqueue(new StatMutation
                {
                    Target = state.AppliedTarget,
                    Source = clipEntity,
                    IsRemove = true
                });

                state.AppliedTarget = Entity.Null;
            }
        }

        [BurstCompile]
        private struct ApplyJob : IJob
        {
            public NativeQueue<StatMutation> Mutations;
            public BufferLookup<StatModifiers> StatModifiers;
            public ComponentLookup<StatChanged> StatChangeds;

            [ReadOnly] public EntityStorageInfoLookup StorageInfo;

            public void Execute()
            {
                while (Mutations.TryDequeue(out var mutation))
                {
                    if (!StatModifiers.TryGetBuffer(mutation.Target, out var buffer)) continue;

                    StatChangeds.SetComponentEnabled(mutation.Target, true);

                    var array = buffer.AsNativeArray();
                    for (var i = array.Length - 1; i >= 0; i--)
                    {
                        var source = array[i].SourceEntity;
                        if (DistanceSampling.ShouldDropModifier(source, mutation.Source, StorageInfo.Exists(source)))
                            buffer.RemoveAtSwapBack(i);
                    }

                    if (!mutation.IsRemove)
                        buffer.Add(new StatModifiers
                        {
                            SourceEntity = mutation.Source,
                            Value = mutation.Modifier
                        });
                }
            }
        }
    }
}