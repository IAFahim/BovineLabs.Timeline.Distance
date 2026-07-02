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

    /// <summary>
    /// Cleanup shadow of <see cref="DistanceToStatState.AppliedTarget"/> so the applied modifier is removed even
    /// when the clip entity is destroyed while active (subscene stream-out / director destroy) and never traverses
    /// the normal ClipActive exit edge.
    /// </summary>
    public struct DistanceToStatCleanup : ICleanupComponentData
    {
        public Entity Target;
    }
}