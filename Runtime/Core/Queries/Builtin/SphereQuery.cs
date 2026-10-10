using System.Threading.Tasks;
using UnityEngine;

namespace CG.CombatFramework
{
    public enum QueryCenter { Origin, AimPoint, Source, Target }

    [System.Serializable, TypeMenu("Sphere (area)")]
    public sealed class SphereQuery : IHitQuery
    {
        [SerializeField, Tooltip("Origin: where delivery starts (muzzle, or host for EmitAtTarget) \nAimPoint: where the owner aims (grenade)\nSource: caster (shockwave) \nTarget: unit in context (missile hitting target)")]
        private QueryCenter center = QueryCenter.Origin;
        [SerializeField, Min(0f)] private float radius = 4f;
        [SerializeField] private LayerMask mask = ~0;
        [SerializeField] private QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide;
        [SerializeField, Tooltip("Include source (caster) unit for who to deliver payloads to")] 
        private bool includeSource;

        public void Query(in DeliveryContext ctx, HitCollector results)
        {
            Vector3 c = Center(in ctx, center);
            var source = ctx.Effect.SourceUnit;
            var buffer = PhysicsBuffers.Colliders;
            int count = Physics.OverlapSphereNonAlloc(c, radius, buffer, mask, triggers);

            for(int i = 0; i < count; i++)
            {
                if(!HitResolver.TryResolve(buffer[i], out var unit, out _)) continue;
                if(!includeSource && ReferenceEquals(unit, source)) continue;

                Vector3 point = HitResolver.ClosestPoint(buffer[i], c);
                Vector3 offset = point - c;
                float distance = offset.magnitude;
                Vector3 dir = distance > 0.0001f ? offset / distance : ctx.Direction;
                results.Add(unit, new HitData(point, -dir, dir), distance);
            }
        }


        private static Vector3 Center(in DeliveryContext ctx, QueryCenter center)
        {
            switch(center)
            {
                case QueryCenter.AimPoint: return ctx.AimPoint;
                case QueryCenter.Source:
                    var source = ctx.Effect.SourceUnit;
                    return source != null ? source.Transform.position : ctx.Origin;
                case QueryCenter.Target:
                    var target = ctx.Effect.TargetUnit;
                    return target != null? target.Transform.position : ctx.Origin;
                default: return ctx.Origin;
            }
        }
    }
}
