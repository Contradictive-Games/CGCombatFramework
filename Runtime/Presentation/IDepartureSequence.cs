namespace CG.CombatFramework
{
    public interface IDepartureSequence
    {
        public float Begin(IUnit unit, DespawnReason reason);
        public void Stop() { }
    }
}
