using UnityEngine;

namespace CG.CombatFramework
{
    [DisallowMultipleComponent]
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Unit Components/Stat Component")]
    public sealed class StatsComponent : MonoBehaviour, IStatProvider, IUnitComponent
    {
        [SerializeField] private UnitStatProfile profile;

        private StatBlock block;

#region IUnitComponent

        void IUnitComponent.Initialize(IUnit owner) => block = new StatBlock(profile);
        void IUnitComponent.OnDespawned(DespawnReason despawnReason) => block.ResetToProfile();

#endregion

        public bool TryGetStat(StatType type, out IStat stat) => block.TryGetStat(type, out stat);
        public float GetValue(StatType type, float fallback = 0f) => block.GetValue(type, fallback);
        public IStat GetOrCreate(StatType type) => block.GetOrCreate(type);
        public int RemoveAllModifiersFromSource(object source) => block.RemoveAllModifiersFromSource(source);
    }
}
