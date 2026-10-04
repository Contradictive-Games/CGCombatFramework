using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable]
    public sealed class Delivery : IEmission
    {
        [SerializeReference, SelectType] private IHitQuery query;
        [SerializeReference, SelectType] private List<ICondition> filters = new();
        [SerializeReference, Min(1)] private int maxTargets = 32;
        [SerializeReference, SelectType] private List<IPayload> payloads = new();

        public void Emit(in DeliveryContext ctx)
        {
            
        }
    }
}