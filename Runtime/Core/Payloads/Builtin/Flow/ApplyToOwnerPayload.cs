using System;
using UnityEngine;

namespace CG.CombatFramework
{
    [Serializable, TypeMenu("Flow/Apply to Owner")]
    public sealed class ApplyToOwnerPayload : IPayload
    {
        [SerializeReference, SelectType]
        private IPayload[] payloads = Array.Empty<IPayload>();

        public void Apply(in EffectContext ctx)
        {
            var source = ctx.SourceUnit;
            var owner = source?.Root();
            if (owner == null || ReferenceEquals(owner, source)) return;
            var toOwner = ctx.Retarget(owner, ctx.Hit);
            Payloads.ApplyAll(payloads, in toOwner);
        }
    }
}