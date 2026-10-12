using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Hit Zone")]
    public sealed class HitZoneCondition : ICondition
    {
        [SerializeField, Tooltip("Hitbox's zone tag or parent of it")]
        private CombatTag zoneTag;

        public bool Evaluate(in EffectContext ctx)
        {
            var hitbox = ctx.Hit.Hitbox;
            return hitbox != null && hitbox.Zone != null && zoneTag != null && hitbox.Zone.IsOrDescendedFrom(zoneTag);
        }
    }
}
