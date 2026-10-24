using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Chain")]
    public sealed class ChainQuery : IHitQuery
    {
        [SerializeField, Tooltip("Start at the context's target when there is one. Otherwise nearest valid unit to the origin")]
        private bool startAtTarget = true;
        [SerializeField, Min(0f), Tooltip("How far from the origin the first target can be when there's no context target")]
        private float firstRange = 12f;
        [SerializeField, Min(0)] private int jumps = 3;
        [SerializeField, Min(0f)] private float jumpRange = 6f;
        [SerializeField] private LayerMask mask = ~0;
        [SerializeField] private QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide;
        [SerializeReference, SelectType, Tooltip("Who the chan may land on. Source = caster, Target = the candidate")]
        private ICondition[] canHit = { new RelationCondition() };

        public void Query(in DeliveryContext ctx, HitCollector results)
        {
            IUnit current = startAtTarget ? ctx.Effect.TargetUnit : null;
            Vector3 from = ctx.Origin;

            if(current == null) current = Nearest(in ctx, from, firstRange, results);

            for(int jump = 0; current != null && jump <= jumps; jump++)
            {
                Vector3 at = current.Transform.position;
                Vector3 dir = at -from;
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : ctx.Direction;
                results.Add(current, new HitData(at, -dir, dir), jump);
                from = at;
                current = jump < jumps ? Nearest(in ctx, from, jumpRange, results) : null;
            }
        }


        private IUnit Nearest(in DeliveryContext ctx, Vector3 from, float range, HitCollector results)
        {
            var source = ctx.Effect.SourceUnit;
            var buffer = PhysicsBuffers.Colliders;
            int count = Physics.OverlapSphereNonAlloc(from, range, buffer, mask, triggers);
            IUnit best = null;
            float bestSqr = float.MaxValue;
            for(int i =0; i < count; i++)
            {
                if(!HitResolver.TryResolve(buffer[i], out var unit, out _)) continue;
                if(ReferenceEquals(unit, source) || !unit.IsAlive || results.Contains(unit)) continue;
                float sqr = (unit.Transform.position - from).sqrMagnitude;
                if(sqr >= bestSqr) continue;
                var check =ctx.Effect.Retarget(unit, default);
                if(!canHit.All(in check)) continue;
                bestSqr = sqr;
                best = unit;
            }

            return best;
        }
    }
}
