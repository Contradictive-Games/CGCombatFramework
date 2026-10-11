namespace CG.CombatFramework
{
    public readonly struct RegenState
    {
        public readonly float Current;
        public readonly float Max;
        public readonly float TimeSinceLoss;
        public readonly float TimeSinceGain;

        public RegenState(float current, float max, float timeSinceLoss, float timeSinceGain)
        {
            Current = current;
            Max = max;
            TimeSinceLoss = timeSinceLoss;
            TimeSinceGain = timeSinceGain;
        }
    }
}