using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using Unity.Entities;

namespace BovineLabs.Timeline.Distance.Data
{
    public enum DistanceUpdateMode : byte
    {
        OnStart,
        Continuous,
        Interval
    }

    public struct DistanceToStatData : IComponentData
    {
        public Target From;
        public ushort FromLinkKey;

        public Target To;
        public ushort ToLinkKey;

        public Target StatTarget;
        public ushort StatLinkKey;
        public StatKey StatKey;

        public DistanceUpdateMode Mode;
        public float Interval;
        public float Multiplier;
    }

    public struct DistanceToStatState : IComponentData
    {
        public float Timer;
        public Entity AppliedTarget;
    }
}