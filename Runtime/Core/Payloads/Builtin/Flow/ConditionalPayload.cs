using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Flow/If ... Else")]
    public sealed class ConditionalPayload : IPayload
    {
        [SerializeReference, SelectType, Tooltip("All conditions that must be met")]
        private ICondition[] conditions = System.Array.Empty<ICondition>();
        [Header("Payloads")]
        [SerializeReference, SelectType, Tooltip("All payloads that will be executed if conditions are met")]
        private IPayload[] then = System.Array.Empty<IPayload>();
        [SerializeReference, SelectType, Tooltip("All payloads that will be executed if conditions are not me")]
        private IPayload[] otherwise = System.Array.Empty<IPayload>();


        public void Apply(in EffectContext ctx)
        {
            Payloads.ApplyAll(conditions.All(in ctx) ? then : otherwise, in ctx);
        }
    }
}
