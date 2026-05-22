using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Distance.Data;
using BovineLabs.Timeline.EntityLinks.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Distance.Authoring
{
    public sealed class DistanceToStatClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Distance Calculation")] public Target from = Target.Owner;

        public EntityLinkSchema fromLink;

        public Target to = Target.Target;
        public EntityLinkSchema toLink;

        [Header("Stat Routing")] public Target statTarget = Target.Self;

        public EntityLinkSchema statTargetLink;
        public StatSchemaObject stat;

        [Tooltip(
            "Multiplier applied before converting the float distance into an integer stat (e.g., 100 to map 1.5m to 150)")]
        public float multiplier = 1f;

        [Header("Update Mode")] public DistanceUpdateMode mode = DistanceUpdateMode.Continuous;

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

            context.Baker.AddComponent(clipEntity, new DistanceToStatData
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
            });

            context.Baker.AddComponent<DistanceToStatState>(clipEntity);

            base.Bake(clipEntity, context);
        }
    }
}