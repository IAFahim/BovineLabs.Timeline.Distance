namespace BovineLabs.Timeline.Distance.Data
{
    public static class DistanceInterval
    {
        public const float Default = 0.5f;

        public static float Resolve(DistanceUpdateMode mode, float interval)
        {
            if (mode == DistanceUpdateMode.Interval && interval <= 0f)
            {
                return Default;
            }

            return interval;
        }
    }
}
