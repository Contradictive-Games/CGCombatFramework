namespace CG.CombatFramework
{
    using Random = UnityEngine.Random;

    public sealed class UnityGameRandom : IGameRandom
    {
        public float Range(float min, float max) => Random.Range(min, max);

        public bool Roll(float chance01) {
            if(chance01 <= 0f) return false;
            if(chance01 >= 1f) return true;
            return Value() < chance01;
        }

        public float Value() => Random.value;
    }
}