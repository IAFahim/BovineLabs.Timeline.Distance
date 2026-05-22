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
                TargetsLookup = SystemAPI.GetComponentLookup<Targets>(true),
                LtwLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                Sources = state.GetUnsafeComponentLookup<EntityLinkSource>(true),
                Entries = state.GetUnsafeBufferLookup<EntityLinkEntry>(true)
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new GatherRemoveJob
            {
                Mutations = mutations.AsParallelWriter(),
                TargetsLookup = SystemAPI.GetComponentLookup<Targets>(true),
                Sources = state.GetUnsafeComponentLookup<EntityLinkSource>(true),
                Entries = state.GetUnsafeBufferLookup<EntityLinkEntry>(true)
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ApplyJob
            {
                Mutations = mutations,
                StatModifiers = SystemAPI.GetBufferLookup<StatModifiers>(),
                StatChangeds = SystemAPI.GetComponentLookup<StatChanged>()
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct GatherActiveJob : IJobEntity
        {
            public float DeltaTime;
            public NativeQueue<StatMutation>.ParallelWriter Mutations;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> Sources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Entries;

            private void Execute(Entity clipEntity, in TrackBinding binding, in DistanceToStatData data,
                ref DistanceToStatState state, EnabledRefRO<ClipActivePrevious> activePrev)
            {
                if (binding.Value == Entity.Null || data.StatKey.Value == 0) return;
                if (!TargetsLookup.TryGetComponent(binding.Value, out var targets)) return;

                var isFirstFrame = !activePrev.ValueRO;
                var shouldUpdate = false;

                if (data.Mode == DistanceUpdateMode.OnStart)
                {
                    shouldUpdate = isFirstFrame;
                }
                else if (data.Mode == DistanceUpdateMode.Continuous)
                {
                    shouldUpdate = true;
                }
                else if (data.Mode == DistanceUpdateMode.Interval)
                {
                    if (isFirstFrame)
                    {
                        state.Timer = 0f;
                        shouldUpdate = true;
                    }
                    else
                    {
                        state.Timer += DeltaTime;
                        if (state.Timer >= data.Interval)
                        {
                            state.Timer -= data.Interval;
                            shouldUpdate = true;
                        }
                    }
                }

                if (!shouldUpdate) return;

                var fromEntity = ResolveTarget(binding.Value, data.From, data.FromLinkKey, in targets, Sources,
                    Entries);
                var toEntity = ResolveTarget(binding.Value, data.To, data.ToLinkKey, in targets, Sources, Entries);
                var statEntity = ResolveTarget(binding.Value, data.StatTarget, data.StatLinkKey, in targets, Sources,
                    Entries);

                if (fromEntity == Entity.Null || toEntity == Entity.Null || statEntity == Entity.Null) return;
                if (!LtwLookup.TryGetComponent(fromEntity, out var fromLtw) ||
                    !LtwLookup.TryGetComponent(toEntity, out var toLtw)) return;

                var distance = math.distance(fromLtw.Position, toLtw.Position) * data.Multiplier;

                var modifier = new StatModifier
                {
                    Type = data.StatKey,
                    ModifyType = StatModifyType.Added,
                    Value = (int)math.round(distance)
                };

                Mutations.Enqueue(new StatMutation
                {
                    Target = statEntity,
                    Source = clipEntity,
                    Modifier = modifier,
                    IsRemove = false
                });
            }
        }

        [BurstCompile]
        [WithDisabled(typeof(ClipActive))]
        [WithAll(typeof(ClipActivePrevious))]
        private partial struct GatherRemoveJob : IJobEntity
        {
            public NativeQueue<StatMutation>.ParallelWriter Mutations;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> Sources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Entries;

            private void Execute(Entity clipEntity, in TrackBinding binding, in DistanceToStatData data)
            {
                if (binding.Value == Entity.Null || data.StatKey.Value == 0) return;
                if (!TargetsLookup.TryGetComponent(binding.Value, out var targets)) return;

                var statEntity = ResolveTarget(binding.Value, data.StatTarget, data.StatLinkKey, in targets, Sources,
                    Entries);
                if (statEntity == Entity.Null) return;

                Mutations.Enqueue(new StatMutation
                {
                    Target = statEntity,
                    Source = clipEntity,
                    IsRemove = true
                });
            }
        }

        [BurstCompile]
        private struct ApplyJob : IJob
        {
            public NativeQueue<StatMutation> Mutations;
            public BufferLookup<StatModifiers> StatModifiers;
            public ComponentLookup<StatChanged> StatChangeds;

            public void Execute()
            {
                while (Mutations.TryDequeue(out var mutation))
                {
                    if (!StatModifiers.TryGetBuffer(mutation.Target, out var buffer)) continue;

                    StatChangeds.SetComponentEnabled(mutation.Target, true);

                    var array = buffer.AsNativeArray();
                    for (var i = array.Length - 1; i >= 0; i--)
                        if (array[i].SourceEntity == mutation.Source)
                        {
                            buffer.RemoveAtSwapBack(i);
                            break;
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
}