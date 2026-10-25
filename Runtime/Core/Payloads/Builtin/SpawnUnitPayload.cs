using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Units/Spawn Unit")]
    [BlockInfo("Spawns a unit prefab at the target (or the hit point), optionally owned by the source and using its faction.", Example = "Drop a turret where the grenade lands")]
    public sealed class SpawnUnitPayload : IPayload
    {
        [SerializeField, Tooltip("What will be spawned")] private Unit prefab;
        [SerializeField] private bool ownedBySource = true;
        [SerializeField] private bool useSourceFaction = true;

        public void Apply(in EffectContext ctx)
        {
            if(prefab == null) return;
            var source = ctx.SourceUnit;
            var target = ctx.TargetUnit;
            Vector3 at = target != null ? target.Transform.position : ctx.Hit.Point;
            Quaternion rot = source != null ? source.Transform.rotation : Quaternion.identity;
            var faction = useSourceFaction && source != null ? source.Faction : null;

            CombatServices.Spawner.Spawn(prefab, new SpawnData(at, rot, default, ownedBySource ? source : null, faction));
        }
    }
}
