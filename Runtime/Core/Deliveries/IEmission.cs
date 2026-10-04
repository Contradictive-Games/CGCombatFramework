namespace CG.CombatFramework
{
    public interface IEmission
    {
        public void Emit(in DeliveryContext ctx);
    }
}