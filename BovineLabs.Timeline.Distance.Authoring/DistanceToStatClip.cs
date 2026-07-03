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
        public EntityLinkSchema fromLink;
        public EntityLinkSchema toLink;
        public EntityLinkSchema statTargetLink;

        [Header("Distance Calculation")]
        [Tooltip(
            "Which endpoint A of the distance is measured from. When fromLink is set, this same enum also selects whose link map is read to resolve the linked entity.")]
        public Target from = Target.Owner;

        [Tooltip(
            "Which endpoint B of the distance is measured to. When toLink is set, this same enum also selects whose link map is read to resolve the linked entity.")]
        public Target to = Target.Target;

        [Header("Stat Routing")]
        [Tooltip(
            "Which entity owns the stat that receives the modifier. When statTargetLink is set, this same enum also selects whose link map is read to resolve the linked entity.")]
        public Target statTarget = Target.Self;

        public StatSchemaObject stat;

        [Tooltip(
            "Multiplier applied to the metre distance before it is rounded to the integer Added modifier. The stat reads that integer value divided by 100, so use 100 to map 1m to 1 stat unit (e.g. 1.5m becomes 150, read as 1.5).")]
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