namespace CG.CombatFramework
{
    public sealed class SeededCombatRandom : ICombatRandom
    {
        private readonly System.Random rng;

        public SeededCombatRandom(int seed) => rng = new System.Random(seed);

        public float Value() => (float)rng.NextDouble();

        public float Range(float min, float max) => min + Value() * (max - min);
        public bool Roll(float chance01)
        {
            if(chance01 <= 0f) return false;
            if(chance01 >= 1f) return true;
            return Value() < chance01;
        }
    }
}