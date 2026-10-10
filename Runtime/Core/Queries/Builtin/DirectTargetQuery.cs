namespace CG.CombatFramework
{
    /// <summary>
    /// Target already in context (normally from projectiles, lock-on abilities)
    /// </summary>
    [System.Serializable, TypeMenu("Direct Target")]
    public sealed class DirectTargetQuery : IHitQuery
    {
        public void Query(in DeliveryContext ctx, HitCollector results)
        {
            var target = ctx.Effect.TargetUnit;
            if(target != null) results.Add(target, ctx.Effect.Hit, 0f);
        }
    }
}
