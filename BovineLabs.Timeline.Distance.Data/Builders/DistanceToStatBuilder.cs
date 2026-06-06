using BovineLabs.Core.EntityCommands;
using Unity.Entities;

namespace BovineLabs.Timeline.Distance.Data.Builders
{
    public struct DistanceToStatBuilder
    {
        public DistanceToStatData Data;
        public bool HasState;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(Data);
            if (HasState)
            {
                builder.AddComponent<DistanceToStatState>();
            }
        }
    }
}
