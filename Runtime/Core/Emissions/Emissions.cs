namespace CG.CombatFramework
{
    public static class Emissions
    {
        public static void EmitAll(IEmission[] emissions, in DeliveryContext ctx)
        {
            for (int i = 0; i < emissions.Length; i++)
            {
                emissions[i]?.Emit(in ctx);
            }
        }
    }
}
