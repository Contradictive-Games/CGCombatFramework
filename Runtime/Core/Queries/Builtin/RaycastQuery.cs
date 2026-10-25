using System;
using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Raycast")]
    [BlockInfo("Casts a ray (or a sphere cast with Radius) along the aim, with optional random spread. Can pierce units, and world geometry blocks it when Blocked By World is on.", Example = "Sniper with Pierce 2 to hit three enemies in a line")]
    public sealed class RaycastQuery : IHitQuery
    {
        [SerializeField, Min(0f)] private float range = 50f;
        [SerializeField, Min(0f), Tooltip("Greater than 0 = sphere cast")] private float radius = 0f;
        [SerializeField] private LayerMask mask = ~0;
        [SerializeField] private QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide;
        [SerializeField, Min(0), Tooltip("How many units this can pass through after the first")] private int pierce = 0;
        [SerializeField, Tooltip("Stops at first collider that isn't a unit")] private bool blockedByWorld = true;
        [SerializeField, Tooltip("Pass through any dead units (generally true)")] private bool ignoreDead = true;
        [SerializeField, Range(0f, 45f), Tooltip("Random raycast spread")] private float spreadDegrees;
        [SerializeField, Tooltip("If spread is sideways only")] private bool horizontalSpreadOnly = true;


        public void Query(in DeliveryContext ctx, HitCollector results)
        {
            var source= ctx.Effect.SourceUnit;
            Vector3 dir = AimMath.Spread(ctx.Direction, spreadDegrees, horizontalSpreadOnly, ctx.Effect.Random);
            var buffer = PhysicsBuffers.Hits;
            int count = radius > 0f ?
                Physics.SphereCastNonAlloc(ctx.Origin, radius, dir, buffer, range, mask, triggers)
                :
                Physics.RaycastNonAlloc(ctx.Origin, dir, buffer, range, mask, triggers);
            PhysicsSort.ByDistance(buffer, count);

            int remaining = pierce + 1;
            for(int i = 0; i < count && remaining > 0; i++)
            {
                var hit = buffer[i];
                if(!HitResolver.TryResolve(hit.collider, out var unit, out var hitbox))
                {
                    if(blockedByWorld) return;
                    continue;
                }
                if(ReferenceEquals(unit, source) || (ignoreDead && !unit.IsAlive)) continue;
                Vector3 point = hit.distance > 0f ? hit.point : ctx.Origin;
                if(results.Add(
                    unit,
                    new HitData(point, hit.normal, dir, hitbox),
                    hit.distance))
                {
                    remaining--;
                }
            }
        }

    }
}
