namespace CG.CombatFramework
{
    public interface IPayload
    {
        public void Apply(in EffectContext ctx);
    }
}
