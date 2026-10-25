using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Everyone (no physics)")]
    [BlockInfo("Every registered unit, without physics. Optionally only those within Max Distance of the origin.", Example = "A map-wide boss shout")]
    public sealed class EveryoneQuery : IHitQuery
    {
        [SerializeField, Min(0f), Tooltip("0 = the whole map")] 
        private float maxDistance;
        [SerializeField] private bool includeSource;
        [SerializeField] private bool aliveOnly = true;

        public void Query(in DeliveryContext ctx, HitCollector results)
        {
            var source = ctx.Effect.SourceUnit;
            float maxSqr = maxDistance > 0f ? maxDistance * maxDistance : float.MaxValue;
            var all = CombatServices.Units.All;
            for(int i = 0; i < all.Count; i++)
            {
                var unit = all[i];
                if(!includeSource && ReferenceEquals(unit, source)) continue;
                if(aliveOnly && !unit.IsAlive) continue;
                Vector3 to = unit.Transform.position - ctx.Origin;
                float sqr = to.sqrMagnitude;
                if(sqr > maxSqr) continue;
                float distance = Mathf.Sqrt(sqr);
                Vector3 dir = distance > 0.0001f ? to / distance : ctx.Direction;
                results.Add(unit, new HitData(unit.Transform.position, -dir, dir), distance);
            }
        }
    }
}