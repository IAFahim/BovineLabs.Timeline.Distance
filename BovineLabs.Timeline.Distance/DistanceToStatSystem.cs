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
using Unity.Mathematics;
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

            state.Dependency = new ApplyJob
            {
                Mutations = mutations,
                StatModifiers = SystemAPI.GetBufferLookup<StatModifiers>(),
                StatChangeds = SystemAPI.GetComponentLookup<StatChanged>(),
                StorageInfo = SystemAPI.GetEntityStorageInfoLookup()
            }.Schedule(state.Dependency);
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

                if (!ShouldUpdate(data.Mode, isFirstFrame, data.Interval, DeltaTime, ref state.Timer)) return;

                var fromEntity = ResolveTarget(binding.Value, data.From, data.FromLinkKey, in targets, Sources,
                    Entries);
                var toEntity = ResolveTarget(binding.Value, data.To, data.ToLinkKey, in targets, Sources, Entries);
                var statEntity = ResolveTarget(binding.Value, data.StatTarget, data.StatLinkKey, in targets, Sources,
                    Entries);

                if (fromEntity == Entity.Null || toEntity == Entity.Null || statEntity == Entity.Null) return;
                if (!LtwLookup.TryGetComponent(fromEntity, out var fromLtw) ||
                    !LtwLookup.TryGetComponent(toEntity, out var toLtw)) return;

                var distance = math.distance(fromLtw.Position, toLtw.Position) * data.Multiplier;
                if (!math.isfinite(distance))
                    return;

                var modifier = new StatModifier
                {
                    Type = data.StatKey,
                    ModifyType = StatModifyType.Added,
                    Value = (int)math.round(distance)
                };

                // If the resolved stat target changed since last update (re-route via link/Targets slot),
                // remove our modifier from the OLD target first; otherwise it leaks there permanently
                // because the per-update add only replaces on the new target and clip-end removes only
                // from state.AppliedTarget.
                if (state.AppliedTarget != Entity.Null && state.AppliedTarget != statEntity)
                {
                    Mutations.Enqueue(new StatMutation
                    {
                        Target = state.AppliedTarget,
                        Source = clipEntity,
                        IsRemove = true
                    });
                }

                state.AppliedTarget = statEntity;

                Mutations.Enqueue(new StatMutation
                {
                    Target = statEntity,
                    Source = clipEntity,
                    Modifier = modifier,
                    IsRemove = false
                });
            }

            private static bool ShouldUpdate(DistanceUpdateMode mode, bool isFirstFrame, float interval, float deltaTime,
                ref float timer)
            {
                switch (mode)
                {
                    case DistanceUpdateMode.OnStart:
                        return isFirstFrame;
                    case DistanceUpdateMode.Continuous:
                        return true;
                    case DistanceUpdateMode.Interval:
                        return ShouldUpdateInterval(isFirstFrame, interval, deltaTime, ref timer);
                    default:
                        return false;
                }
            }

            private static bool ShouldUpdateInterval(bool isFirstFrame, float interval, float deltaTime, ref float timer)
            {
                if (isFirstFrame)
                {
                    timer = 0f;
                    return true;
                }

                timer += deltaTime;
                if (timer < interval)
                    return false;

                timer -= interval;
                return true;
            }

            private static Entity ResolveTarget(
                Entity self, Target mode, ushort linkKey,
                in Targets targets,
                in UnsafeComponentLookup<EntityLinkSource> sources,
                in UnsafeBufferLookup<EntityLinkEntry> entries)
            {
                if (linkKey != 0 &&
                    EntityLinkResolver.TryResolve(self, targets, mode, linkKey, sources, entries, out var linked))
                    return linked;
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

                    // Remove our own previous modifier and garbage-collect any entry whose SourceEntity
                    // no longer exists. Without this, a clip entity destroyed mid-activation (subscene
                    // unload / timeline teardown) leaves its modifier on this still-alive target forever,
                    // because GatherRemoveJob can only fire for a clip that still exists.
                    var array = buffer.AsNativeArray();
                    for (var i = array.Length - 1; i >= 0; i--)
                    {
                        var source = array[i].SourceEntity;
                        if (source == mutation.Source || (source != Entity.Null && !StorageInfo.Exists(source)))
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