using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Delivery")]
    public sealed class Delivery : IEmission
    {
        [SerializeReference, SelectType] private IHitQuery query;
        [SerializeReference, SelectType] private ICondition[] filters = System.Array.Empty<ICondition>();
        [SerializeField, Min(1)] private int maxTargets = 32;
        [SerializeReference, SelectType] private IPayload[] payloads = System.Array.Empty<IPayload>();

        public void Emit(in DeliveryContext ctx)
        {
            using var sample = CombatProfiler.Delivery.Auto();
            
            if(query == null) return;

            var hits = HitCollector.Rent();
            try
            {
                query.Query(in ctx, hits);
                hits.SortByDistance();

                var effect = ctx.Effect;
                int applied = 0;
                for(int i = 0; i < hits.Hits.Count && applied < maxTargets; i++)
                {
                    var hit = hits.Hits[i];
                    var perHit = effect.Retarget(hit.Unit, hit.Hit);
                    if(!filters.All(in perHit)) continue;

                    Payloads.ApplyAll(payloads, in perHit);
                    applied++;
                }
            }
            finally
            {
                HitCollector.Return(hits);
            }
        }
    }
}