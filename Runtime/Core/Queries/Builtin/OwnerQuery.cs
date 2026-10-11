using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Owner (of the source, to Root)")]
    public sealed class OwnerQuery : IHitQuery
    {
        public void Query(in DeliveryContext ctx, HitCollector results)
        {
            var source = ctx.Effect.SourceUnit;
            var owner = source?.Root();
            if(owner == null || ReferenceEquals(owner, source)) return;
            results.Add(owner, new HitData(owner.Transform.position, Vector3.up, ctx.Direction), 0f);
        }
    }
}