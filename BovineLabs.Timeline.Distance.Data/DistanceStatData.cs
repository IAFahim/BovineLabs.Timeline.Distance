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
        // Target A
        public Target From;
        public ushort FromLinkKey;

        // Target B
        public Target To;
        public ushort ToLinkKey;

        // Where the stat lives and which stat to modify
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
    }
}