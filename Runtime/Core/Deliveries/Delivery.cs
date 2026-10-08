using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Delivery")]
    public sealed class Delivery : IEmission
    {
        [SerializeReference, SelectType] private IHitQuery query;
        [SerializeReference, SelectType] private List<ICondition> filters = new();
        [SerializeField, Min(1)] private int maxTargets = 32;
        [SerializeReference, SelectType] private List<IPayload> payloads = new();

        public void Emit(in DeliveryContext ctx)
        {
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
                    var perHit = new EffectContext(effect.SourceUnit, effect.Cause, hit.Unit, hit.Hit, effect.Power, effect.Stacks, effect.ProcDepth, effect.Random);
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