namespace CG.CombatFramework
{
    public interface IChargeCounter
    {
        public int Charges { get; }
        public int MaxCharges { get; }
        public float RechargeProgess01 { get; }
    }
}