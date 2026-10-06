using UnityEngine;
using System.Collections.Generic;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Flow/If ... Else")]
    public sealed class ConditionalPayload : IPayload
    {
        [Header("Condition Settings")]
        [SerializeField, Tooltip("If any condition can be met to execute 'then' payloads. If false, all conditions must be met")] 
        private bool any = false;
        [SerializeReference, SelectType, Tooltip("All conditions that must be met")]
        private List<ICondition> conditions = new();
        [Header("Payloads")]
        [SerializeReference, SelectType, Tooltip("All payloads that will be executed if conditions are met")]
        private List<IPayload> then = new();
        [SerializeReference, SelectType, Tooltip("All payloads that will be executed if conditions are not me")]
        private List<IPayload> otherwise = new();


        public void Apply(in EffectContext ctx)
        {
            if(any) Payloads.ApplyAll(conditions.Any(in ctx) ? then : otherwise, in ctx);
            else Payloads.ApplyAll(conditions.All(in ctx) ? then : otherwise, in ctx);
        }
    }
}