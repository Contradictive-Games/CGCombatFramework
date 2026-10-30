namespace CG.CombatFramework
{
    public interface IHealthListener
    {
        public void OnDied(in DiedEvent evt) { }
        public void OnRevived(in RevivedEvent evt) { }
    }
}
