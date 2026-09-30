namespace CG.CombatFramework
{
    public interface IValueScaler
    {
        public float Apply(float value, in EffectContext ctx);
    }
}