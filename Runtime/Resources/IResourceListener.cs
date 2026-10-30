namespace CG.CombatFramework
{
    public interface IResourceListener
    {
        public void OnResourceChanged(in ResourceChangedEvent evt) { }
        public void OnResourceDepleted(in ResourceDepletedEvent evt) { }
    }
}
