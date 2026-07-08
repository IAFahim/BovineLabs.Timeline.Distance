using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Distance.Data;
using BovineLabs.Timeline.Distance.Data.Builders;
using BovineLabs.Timeline.EntityLinks.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Distance.Authoring
{
    public sealed class DistanceToStatClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Distance Calculation")]
        [Tooltip(
            "Endpoint A of the distance. When From Link is set, this same enum also selects whose link map is read to resolve the linked entity.")]
        public Target from = Target.Owner;

        [Tooltip(
            "Optional link override for endpoint A: resolve A to a linked entity via From's link map instead of the From slot directly. Leave empty to use the From slot.")]
        public EntityLinkSchema fromLink;

        [Tooltip(
            "Endpoint B of the distance. When To Link is set, this same enum also selects whose link map is read to resolve the linked entity.")]
        public Target to = Target.Target;

        [Tooltip(
            "Optional link override for endpoint B: resolve B to a linked entity via To's link map instead of the To slot directly. Leave empty to use the To slot.")]
        public EntityLinkSchema toLink;

        [Header("Stat Routing")]
        [Tooltip(
            "Which entity owns the stat that receives the modifier. When Stat Target Link is set, this same enum also selects whose link map is read to resolve the linked entity.")]
        public Target statTarget = Target.Self;

        [Tooltip(
            "Optional link override for the stat owner: resolve the stat entity via Stat Target's link map instead of the Stat Target slot directly. Leave empty to use the Stat Target slot.")]
        public EntityLinkSchema statTargetLink;

        [Tooltip("The stat that receives the distance as an Added modifier while this clip is active.")]
        public StatSchemaObject stat;

        [Tooltip(
            "Metres are multiplied by this before rounding to the integer Added modifier. Stats are x100 fixed-point (read as value/100), so 100 = 1 metre reads as 1.0 stat units (e.g. 1.5m -> 150 -> 1.5).")]
        public float multiplier = 100f;

        [Header("Update Mode")]
        [Tooltip(
            "Continuous updates the modifier every frame; Interval updates every Interval seconds; OnStart writes once when the clip becomes active.")]
        public DistanceUpdateMode mode = DistanceUpdateMode.Continuous;

        [Min(0f)] [Tooltip("Used only if Mode is Interval")]
        public float interval = DistanceInterval.Default;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            if (stat == null)
            {
                Debug.LogWarning(
                    $"DistanceToStatClip '{name}' has no Stat assigned; the clip will not modify any stat.", this);
                return;
            }

            context.Baker.DependsOn(stat);

            var safeInterval = DistanceInterval.Resolve(mode, interval);
            if (safeInterval != interval)
            {
                Debug.LogWarning(
                    $"DistanceToStatClip '{name}' is in Interval mode with interval {interval}; clamping to {safeInterval}s to avoid per-frame updates.",
                    this);
            }

            var builder = new DistanceToStatBuilder
            {
                Data = new DistanceToStatData
                {
                    From = EntityLinkAuthoringUtility.BakeRef(context.Baker, fromLink, from),
                    To = EntityLinkAuthoringUtility.BakeRef(context.Baker, toLink, to),
                    StatTarget = EntityLinkAuthoringUtility.BakeRef(context.Baker, statTargetLink, statTarget),
                    StatKey = stat.Key,
                    Mode = mode,
                    Interval = safeInterval,
                    Multiplier = multiplier
                },
                HasState = true
            };
            var commands = new BakerCommands(context.Baker, clipEntity);
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}