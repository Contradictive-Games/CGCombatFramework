namespace CG.CombatFramework
{
    public interface ICooldown
    {
        public float Remaining { get; }
        public float Duration { get; }
        public void Reduce(float seconds);
    }
}