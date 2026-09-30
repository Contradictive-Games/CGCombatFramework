using System.Collections.Generic;

namespace CG.CombatFramework
{
    public static class Payloads
    {
        public static void ApplyAll(IReadOnlyList<IPayload> payloads, in EffectContext ctx)
        {
            if(payloads == null) return;
            for(int i = 0; i < payloads.Count; i++)
            {
                payloads[i]?.Apply(in ctx);
            }
        }
    }
}