namespace CG.CombatFramework
{
    public interface IUnitComponent
    {
        void Initialize(IUnit owner);
        void Bind() { }
        void Unbind() { }

        void OnSpawned(in SpawnData data) { }
        void OnDespawned(DespawnReason reason) { }
    }
}
