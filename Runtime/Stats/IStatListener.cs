namespace CG.CombatFramework
{
    public interface IStatListener
    {
        public void OnStatChanged(in StatChangedEvent evt);
    }
}
