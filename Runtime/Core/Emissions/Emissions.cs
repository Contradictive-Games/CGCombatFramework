namespace CG.CombatFramework
{
    public static class Emissions
    {
        public static void EmitAll(this IEmission[] emissions, in DeliveryContext ctx)
        {
            if(emissions == null) return;
            for (int i = 0; i < emissions.Length; i++)
            {
                emissions[i]?.Emit(in ctx);
            }
        }
    }
}
