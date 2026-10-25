using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Unit (target, self, owner)"), MovedFrom(false, sourceClassName: "DirectTargetQuery")]
    [BlockInfo("Hits exactly one unit: the target already in the context, the source, or the source's owner.", Example = "Target on a projectile's On Hit Unit; Source for a self heal")]
    public sealed class UnitQuery : IHitQuery
    {
        [SerializeField] private Subject subject = Subject.Target;

        public void Query(in DeliveryContext ctx, HitCollector results)
        {
            var unit = ctx.Effect.GetUnit(subject);
            if(unit == null) return;
            var hit = subject == Subject.Target ? ctx.Effect.Hit : new HitData(unit.Transform.position, Vector3.up, ctx.Direction);
            results.Add(unit, hit, 0f);
        }
    }
}
