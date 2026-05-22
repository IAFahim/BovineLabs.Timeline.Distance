#if UNITY_EDITOR || BL_DEBUG
using System.Diagnostics.CodeAnalysis;
using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Core;
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
        private const string DrawForced = "distancetostatdebugsystem.force-draw";
        private const string DrawGlobalDescEnabled = "Enable the drawer in the editor.";

        [ConfigVar(DrawForced, false, DrawGlobalDescEnabled)]
        internal static readonly SharedStatic<bool> Enabled =
            SharedStatic<bool>.GetOrCreate<DistanceToStatDebugSystemForced>();

        private struct DistanceToStatDebugSystemForced
        {
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct DistanceToStatDebugSystem : ISystem
    {
        private UnsafeComponentLookup<LocalToWorld> _ltwLookup;
        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _ltwLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<DrawSystem.Singleton>()) return;
            ref var drawSystem = ref SystemAPI.GetSingletonRW<DrawSystem.Singleton>().ValueRW;

            Drawer drawer;
            if (!DistanceToStatDebugSystemConfig.Enabled.Data)
            {
                drawer = drawSystem.CreateDrawer<DistanceToStatDebugSystem>();
                if (!drawer.IsEnabled) return;
            }
            else
            {
                drawer = drawSystem.CreateDrawer();
            }

            _ltwLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);

            state.Dependency = new DrawDistanceJob
            {
                Drawer = drawer,
                LtwLookup = _ltwLookup,
                TargetsLookup = _targetsLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawDistanceJob : IJobEntity
        {
            public Drawer Drawer;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;

            // Beautiful Minimalist Palette
            private static readonly Color LineColor = new(0.1f, 0.85f, 0.75f, 0.4f); // Soft Mint/Cyan
            private static readonly Color PointColor = new(0.1f, 0.95f, 0.85f, 0.9f); // Bright Mint
            private static readonly Color TextColor = new(1f, 1f, 1f, 0.95f); // Crisp White

            private void Execute(Entity entity, in TrackBinding binding, in DistanceToStatData data)
            {
                if (binding.Value == Entity.Null) return;
                if (!TargetsLookup.TryGetComponent(binding.Value, out var targets)) return;

                var fromEntity = ResolveTarget(binding.Value, data.From, data.FromLinkKey, in targets);
                var toEntity = ResolveTarget(binding.Value, data.To, data.ToLinkKey, in targets);

                if (fromEntity == Entity.Null || toEntity == Entity.Null) return;
                if (!LtwLookup.TryGetComponent(fromEntity, out var fromLtw) ||
                    !LtwLookup.TryGetComponent(toEntity, out var toLtw)) return;

                var start = fromLtw.Position;
                var end = toLtw.Position;

                DrawElegantTether(start, end, data.Multiplier);
            }

            private unsafe void DrawElegantTether(float3 start, float3 end, float multiplier)
            {
                var distance = math.distance(start, end);
                if (distance < 0.01f) return;

                // Create a gentle arc based on distance to make it look organic
                var mid = (start + end) * 0.5f;
                mid.y += math.clamp(distance * 0.15f, 0.2f, 2.0f); // Arc peak

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

                // Quadratic Bezier Curve Generation
                for (var i = 1; i <= segments; i++)
                {
                    var t = i / (float)segments;
                    var u = 1 - t;

                    // Bezier Point
                    var current = u * u * start + 2 * u * t * mid + t * t * end;

                    lines[lineLength++] = prev;
                    lines[lineLength++] = current;
                    prev = current;
                }

                // Draw the sleek curved line
                Drawer.Lines(lines.GetSubArray(0, lineLength), LineColor);

                // Draw sharp, minimal anchor points
                Drawer.Point(start, 0.06f, PointColor);
                Drawer.Point(end, 0.06f, PointColor);

                // Format the text elegantly: "5.2m  [520]" 
                var statValue = (int)math.round(distance * multiplier);

                var text = new FixedString64Bytes();
                text.Append(distance); // Formatting float natively
                text.Append('m');
                text.Append("  [");
                text.Append(statValue);
                text.Append(']');

                // Float the text exactly at the peak of the arc with a small visual lift
                var textPos = mid + new float3(0f, 0.25f, 0f);
                Drawer.Text64(textPos, text, TextColor, 12f);
            }

            private Entity ResolveTarget(Entity self, Target mode, ushort linkKey, in Targets targets)
            {
                if (linkKey != 0 &&
                    EntityLinkResolver.TryResolve(self, targets, mode, linkKey, LinkSources, Links, out var linked))
                    return linked;
                return targets.Get(mode, self);
            }
        }
    }
}
#endif