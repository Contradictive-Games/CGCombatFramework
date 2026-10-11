using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Box (line with width)")]
    public sealed class BoxQuery : IHitQuery
    {
        [SerializeField, Min(0f), Tooltip("Forward from origin")] private float length = 6f;
        [SerializeField, Min(0f)] private float width = 2f;
        [SerializeField, Min(0f)] private float height = 2f;
        [SerializeField, Tooltip("Keep box level ignoring vertical aim")] private bool flatten = true;
        [SerializeField] private LayerMask mask = ~0;
        [SerializeField] private QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide;


        public void Query(in DeliveryContext ctx, HitCollector results)
        {
            Vector3 forward = ctx.Direction;
            if(flatten) forward.y = 0f;
            if(forward.sqrMagnitude < 0.0001f) return;
            forward.Normalize();

            Vector3 center = ctx.Origin + forward * (length * 0.5f);
            var halfExtents = new Vector3(width * 0.5f, height * 0.5f, length * 0.5f);
            var source = ctx.Effect.SourceUnit;
            var buffer = PhysicsBuffers.Colliders;
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, buffer, Quaternion.LookRotation(forward), mask, triggers);

            for(int i = 0; i < count; i++)
            {
                if(!HitResolver.TryResolve(buffer[i], out var unit, out _) || ReferenceEquals(unit, source)) continue;
                Vector3 point = HitResolver.ClosestPoint(buffer[i], ctx.Origin);
                float along = Mathf.Max(0f, Vector3.Dot(point - ctx.Origin, forward));
                results.Add(unit, new HitData(point, -forward, forward), along);
            }
        }
    }
}
