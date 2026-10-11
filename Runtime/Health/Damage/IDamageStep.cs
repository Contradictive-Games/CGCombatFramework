namespace CG.CombatFramework
{
    public interface IDamageStep
    {
        public void Process(ref DamageCalculation calc);
    }
}