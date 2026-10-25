using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Cone (frontal)")]
    [BlockInfo("Finds units within Range whose direction from the origin falls inside Angle.", Example = "Flamethrower or a frontal cleave")]
    public sealed class ConeQuery : IHitQuery
    {
        [SerializeField, Range(1f, 360f)] private float angle = 60f;
        [SerializeField, Min(0f)] private float range = 3f;
        [SerializeField] private LayerMask mask = ~0;
        [SerializeField] private QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide;
        [SerializeField, Tooltip("Measure angle on ground plane, usual for top-down")] private bool flatten = true;

        public void Query(in DeliveryContext ctx, HitCollector results)
        {
            Vector3 forward = Flat(ctx.Direction);
            if(forward.sqrMagnitude < 0.0001f) return;
            forward.Normalize();
            float cosHalf = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
            var source = ctx.Effect.SourceUnit;
            var buffer = PhysicsBuffers.Colliders;
            int count = Physics.OverlapSphereNonAlloc(ctx.Origin, range, buffer, mask, triggers);

            for(int i = 0; i < count; i++)
            {
                if(!HitResolver.TryResolve(buffer[i], out var unit, out var hurtbox) || ReferenceEquals(unit, source)) continue;

                Vector3 point = HitResolver.ClosestPoint(buffer[i], ctx.Origin);
                Vector3 to = Flat(point - ctx.Origin);
                if(to.sqrMagnitude > 0.0001f && Vector3.Dot(to.normalized, forward) < cosHalf) continue;

                Vector3 dir = to.sqrMagnitude > 0.0001f ? to.normalized : forward;
                results.Add(unit, new HitData(point, -dir, dir, hurtbox), to.magnitude);
            }
        }

        private Vector3 Flat(Vector3 v)
        {
            if(flatten) v.y = 0f;
            return v;
        }
    }
}