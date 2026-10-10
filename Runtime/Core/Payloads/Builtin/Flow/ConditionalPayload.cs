using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Flow/If ... Else")]
    public sealed class ConditionalPayload : IPayload
    {
        [Header("Condition Settings")]
        [SerializeField, Tooltip("If any condition can be met to execute 'then' payloads. If false, all conditions must be met")] 
        private bool any = false;
        [SerializeReference, SelectType, Tooltip("All conditions that must be met")]
        private ICondition[] conditions = System.Array.Empty<ICondition>();
        [Header("Payloads")]
        [SerializeReference, SelectType, Tooltip("All payloads that will be executed if conditions are met")]
        private IPayload[] then = System.Array.Empty<IPayload>();
        [SerializeReference, SelectType, Tooltip("All payloads that will be executed if conditions are not me")]
        private IPayload[] otherwise = System.Array.Empty<IPayload>();


        public void Apply(in EffectContext ctx)
        {
            if(any) Payloads.ApplyAll(conditions.Any(in ctx) ? then : otherwise, in ctx);
            else Payloads.ApplyAll(conditions.All(in ctx) ? then : otherwise, in ctx);
        }
    }
}