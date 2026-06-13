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
            "Which endpoint A of the distance is measured from. When fromLink is set, this same enum also selects whose link map is read to resolve the linked entity.")]
        public Target from = Target.Owner;

        public EntityLinkSchema fromLink;

        [Tooltip(
            "Which endpoint B of the distance is measured to. When toLink is set, this same enum also selects whose link map is read to resolve the linked entity.")]
        public Target to = Target.Target;
        public EntityLinkSchema toLink;

        [Header("Stat Routing")]
        [Tooltip(
            "Which entity owns the stat that receives the modifier. When statTargetLink is set, this same enum also selects whose link map is read to resolve the linked entity.")]
        public Target statTarget = Target.Self;

        public EntityLinkSchema statTargetLink;
        public StatSchemaObject stat;

        [Tooltip(
            "Multiplier applied to the metre distance before it is rounded to the integer Added modifier. The stat reads that integer value divided by 100, so use 100 to map 1m to 1 stat unit (e.g. 1.5m becomes 150, read as 1.5).")]
        public float multiplier = 100f;

        [Header("Update Mode")]
        [Tooltip(
            "Continuous updates the modifier every frame; Interval updates every Interval seconds; OnStart writes once when the clip becomes active.")]
        public DistanceUpdateMode mode = DistanceUpdateMode.Continuous;

        [Tooltip("Used only if Mode is Interval")]
        public float interval = 0.5f;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            if (stat == null) return;

            EntityLinkAuthoringUtility.TryGetKey(fromLink, out var fromKey);
            EntityLinkAuthoringUtility.TryGetKey(toLink, out var toKey);
            EntityLinkAuthoringUtility.TryGetKey(statTargetLink, out var statTargetKey);

            var builder = new DistanceToStatBuilder
            {
                Data = new DistanceToStatData
                {
                    From = from,
                    FromLinkKey = fromKey,
                    To = to,
                    ToLinkKey = toKey,
                    StatTarget = statTarget,
                    StatLinkKey = statTargetKey,
                    StatKey = stat.Key,
                    Mode = mode,
                    Interval = interval,
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