using UnityEngine;

namespace CG.CombatFramework
{
    public interface IUnitSpawner
    {
        public IUnit Spawn(Unit prefab, in SpawnData data);
        public void Despawn(IUnit unit, DespawnReason reason);
        public void DespawnAll(DespawnReason reason);
    }
}
