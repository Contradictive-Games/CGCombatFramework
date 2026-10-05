using UnityEngine;

namespace CG.CombatFramework
{
    [AddComponentMenu("CG Combat Framework/Unit Components/Stats")]
    public sealed class StatsComponent : MonoBehaviour, IStatProvider, IUnitComponent
    {
        [SerializeField] private UnitStatProfile profile;

        private StatBlock block;

        public void Initialize(IUnit owner) => block = new StatBlock(profile);
        public void Bind() {}
        public void Unbind() { }

        public void OnSpawned(in SpawnData data) => block.ResetToProfile();
        public void OnDespawned(DespawnReason despawnReason) { }


        public bool TryGetStat(StatType type, out IStat stat) => block.TryGetStat(type, out stat);
        public float GetValue(StatType type, float fallback = 0f) => block.GetValue(type, fallback);
        public IStat GetOrCreate(StatType type) => block.GetOrCreate(type);
        public int RemoveAllModifiersFromSource(object source) => block.RemoveAllModifiersFromSource(source);
    }
}