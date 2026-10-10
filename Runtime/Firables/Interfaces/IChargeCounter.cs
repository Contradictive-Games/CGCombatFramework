namespace CG.CombatFramework
{
    public interface IChargeCounter
    {
        public int Charges { get; }
        public int MaxCharges { get; }
        public float RechargeProgress01 { get; }
    }
}