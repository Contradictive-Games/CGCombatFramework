namespace CG.CombatFramework
{
    using Random = UnityEngine.Random;

    public sealed class UnityCombatRandom : ICombatRandom
    {
        public float Range(float min, float max) => Random.Range(min, max);

        public bool Roll(float chance01) => Value() < chance01;

        public float Value() => Random.value;
    }
}