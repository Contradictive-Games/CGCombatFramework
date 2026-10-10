namespace CG.CombatFramework
{
    public interface IReloadable
    {
        public int Current { get; }
        public int Capacity { get; }
        public bool IsReloading { get; }
        public float ReloadProgress01 { get; }
        public bool Reload();
    }
}