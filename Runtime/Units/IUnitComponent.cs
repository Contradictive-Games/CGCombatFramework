namespace CG.CombatFramework
{
    public interface IUnitComponent
    {
        public void Initialize(IUnit owner);
        public void Bind();
        public void Unbind();

        public void OnSpawned(in SpawnData data);
        public void OnDespawned(DespawnReason reason);
    }
}