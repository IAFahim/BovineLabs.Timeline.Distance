using BovineLabs.Essence.Data;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Distance.Data
{
    [BurstCompile]
    public static class DistanceSampling
    {
        public static bool ShouldSample(DistanceUpdateMode mode, bool isFirstFrame, float interval, float deltaTime,
            float timer, out float newTimer)
        {
            switch (mode)
            {
                case DistanceUpdateMode.OnStart:
                    newTimer = timer;
                    return isFirstFrame;
                case DistanceUpdateMode.Continuous:
                    newTimer = timer;
                    return true;
                case DistanceUpdateMode.Interval:
                    return ShouldSampleInterval(isFirstFrame, interval, deltaTime, timer, out newTimer);
                default:
                    newTimer = timer;
                    return false;
            }
        }

        public static bool TryComputeModifier(float3 from, float3 to, float multiplier, float weight, StatKey statKey,
            out StatModifier modifier)
        {
            // weight is the clip's evaluated timeline ease (0..1) so a blend in/out fades the stat contribution.
            var distance = math.distance(from, to) * multiplier * weight;
            if (!math.isfinite(distance))
            {
                modifier = default;
                return false;
            }

            modifier = new StatModifier
            {
                Type = statKey,
                ModifyType = StatModifyType.Added,
                Value = (int)math.round(distance)
            };
            return true;
        }

        public static bool ShouldDropModifier(Entity entrySource, Entity mutationSource, bool entrySourceExists)
        {
            return entrySource == mutationSource;
        }

        private static bool ShouldSampleInterval(bool isFirstFrame, float interval, float deltaTime, float timer,
            out float newTimer)
        {
            if (isFirstFrame)
            {
                newTimer = 0f;
                return true;
            }

            newTimer = timer + deltaTime;
            if (newTimer < interval)
                return false;

            newTimer -= interval;
            return true;
        }
    }
}
