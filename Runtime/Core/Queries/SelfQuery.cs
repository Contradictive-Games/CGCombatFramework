using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Self")]
    public sealed class SelfQuery : IHitQuery
    {
        public void Query(in DeliveryContext ctx, HitCollector results)
        {
            var self = ctx.Effect.SourceUnit;
            if(self != null) results.Add(
                self, 
                new HitData(self.Transform.position, Vector3.up, ctx.Direction),
                0f);
        }
    }
}