namespace CG.CombatFramework
{
    public static class Payloads
    {
        public static void ApplyAll(IPayload[] payloads, in EffectContext ctx)
        {
            if(payloads == null) return;
            for(int i = 0; i < payloads.Length; i++)
            {
                payloads[i]?.Apply(in ctx);
            }
        }
    }
}