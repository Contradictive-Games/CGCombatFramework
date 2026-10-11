namespace CG.CombatFramework
{
    /// <summary>
    /// Used for different types of rolling that may be implemented
    /// </summary>
    public interface IGameRandom
    {
        public float Value();
        public float Range(float min, float max);
        public bool Roll(float chance01);
    }
}