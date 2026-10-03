namespace CG.CombatFramework
{
    public interface ITickSource
    {
        public void Register(ITickable tickable);
        public void Unregister(ITickable tickable);
    }
}