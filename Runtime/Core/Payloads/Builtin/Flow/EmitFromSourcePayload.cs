namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Flow/Emit From Source")]
    public sealed class EmitFromSourcePayload : IPayload
    {
        [UnityEngine.SerializeReference, SelectType] 
        private IEmission[] emissions = System.Array.Empty<IEmission>();

        public void Apply(in EffectContext ctx)
        {
            var source = ctx.SourceUnit;
            if (source == null) return;
            var aim = DeliveryContext.FromUnit(source, ctx.Cause, ctx.Power, ctx.Random);
            var effect = ctx.Nested(null, default);
            var delivery = new DeliveryContext(in effect, aim.Origin, aim.Direction, aim.AimPoint);
            for (int i = 0; i < emissions.Length; i++)
            {
                emissions[i]?.Emit(in delivery);
            }
        }
    }
}