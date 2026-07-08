#if UNITY_EDITOR || BL_DEBUG
using System.Diagnostics.CodeAnalysis;
using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Core;
using BovineLabs.Timeline.Core.Debug;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Distance.Data;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace BovineLabs.Timeline.Distance.Debug
{
    [Configurable]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters should be documented",
        Justification = "Using see cref")]
    public static class DistanceToStatDebugSystemConfig
    {
        [ConfigVar("bovinelabs.distancetostatdebugsystem.draw-enabled", false,
            "Enable the Distance stat debug drawer in the editor.")]
        public static readonly SharedStatic<bool> Enabled =
            SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        private struct Tags
        {
            public struct Enabled
            {
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct DistanceToStatDebugSystem : ISystem
    {
        private UnsafeComponentLookup<LocalToWorld> _ltwLookup;
        private UnsafeComponentLookup<LocalTransform> _localTransformLookup;
        private UnsafeComponentLookup<Parent> _parentLookup;
        private UnsafeComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _ltwLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            _parentLookup = state.GetUnsafeComponentLookup<Parent>(true);
            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<DistanceToStatDebugSystem>(
                    ref state, DistanceToStatDebugSystemConfig.Enabled.Data, out var drawer,
                    out var viewer, out var hasViewer))
                return;

            _ltwLookup.Update(ref state);
            _localTransformLookup.Update(ref state);
            _parentLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);

            state.Dependency = new DrawDistanceJob
            {
                Drawer = drawer,
                Viewer = viewer,
                HasViewer = hasViewer,
                LtwLookup = _ltwLookup,
                LocalTransformLookup = _localTransformLookup,
                ParentLookup = _parentLookup,
                TargetsLookup = _targetsLookup,
                LinkSourceLookup = _linkSourceLookup,
                LinkLookup = _linkLookup
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawDistanceJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public UnsafeComponentLookup<Parent> ParentLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSourceLookup;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> LinkLookup;

            private float3 GetAntiJitterPosition(Entity e, float3 fallback)
            {
                if (LocalTransformLookup.HasComponent(e) && !ParentLookup.HasComponent(e))
                    return LocalTransformLookup[e].Position;
                return fallback;
            }

            private static readonly Color LineColor = TimelineDebugColors.Connection;
            private static readonly Color PointColor = TimelineDebugColors.Anchor;
            private static readonly Color TextColor = TimelineDebugColors.Label;

            private void Execute(Entity entity, in TrackBinding binding, in DistanceToStatData data)
            {
                if (binding.Value == Entity.Null || data.StatKey.Value.IsNull) return;
                if (!TargetsLookup.TryGetComponent(binding.Value, out var targets)) return;

                data.From.TryResolve(binding.Value, targets, LinkSourceLookup, LinkLookup, out var fromEntity, false);
                data.To.TryResolve(binding.Value, targets, LinkSourceLookup, LinkLookup, out var toEntity, false);

                if (fromEntity == Entity.Null || toEntity == Entity.Null) return;
                if (!LtwLookup.TryGetComponent(fromEntity, out var fromLtw) ||
                    !LtwLookup.TryGetComponent(toEntity, out var toLtw)) return;

                var start = GetAntiJitterPosition(fromEntity, fromLtw.Position);
                var end = GetAntiJitterPosition(toEntity, toLtw.Position);

                var tier = TimelineDebugTier.Resolve(start, Viewer, HasViewer);
                DrawElegantTether(start, end, data.Multiplier, tier);
            }

            private unsafe void DrawElegantTether(float3 start, float3 end, float multiplier, DebugTier tier)
            {
                var distance = math.distance(start, end);
                if (!math.isfinite(distance) || distance < 0.01f) return;

                var mid = (start + end) * 0.5f;
                mid.y += math.clamp(distance * 0.15f, 0.2f, 2.0f);

                const int segments = 20;
                const int points = segments * 2;
                var linesData = stackalloc float3[points];
                var lines = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<float3>(linesData, points,
                    Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref lines, AtomicSafetyHandle.GetTempMemoryHandle());
#endif

                var lineLength = 0;
                var prev = start;

                for (var i = 1; i <= segments; i++)
                {
                    var t = i / (float)segments;
                    var u = 1 - t;

                    var current = u * u * start + 2 * u * t * mid + t * t * end;

                    lines[lineLength++] = prev;
                    lines[lineLength++] = current;
                    prev = current;
                }

                Drawer.Lines(lines.GetSubArray(0, lineLength), LineColor);

                if (tier >= DebugTier.Mid)
                {
                    Drawer.Point(start, 0.06f, PointColor);
                    Drawer.Point(end, 0.06f, PointColor);
                    Drawer.Text32(mid + new float3(0f, 0.25f, 0f), (FixedString32Bytes)"Distance", TextColor, 12f);
                }

                if (tier == DebugTier.Close)
                {
                    var statValue = (int)math.round(distance * multiplier);
                    var text = new FixedString128Bytes();
                    text.Append(distance);
                    text.Append((FixedString32Bytes)"m  -> [");
                    text.Append(statValue);
                    text.Append((FixedString32Bytes)"]");
                    Drawer.Text128(mid + new float3(0f, 0.5f, 0f), text, TimelineDebugColors.Label, 11f);
                }
            }
        }
    }
}
#endif