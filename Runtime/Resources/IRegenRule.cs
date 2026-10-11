namespace CG.CombatFramework
{
    public interface IRegenRule
    {
        public float Evaluate(in RegenState stat, float deltaTime);
    }
}