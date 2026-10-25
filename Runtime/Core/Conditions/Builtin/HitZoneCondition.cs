using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Hit Zone")]
    [BlockInfo("Passes when the hit landed on a Hitbox whose zone is this tag or one of its child tags.", Example = "Headshots: If Hit Zone Head then deal extra damage")]
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
