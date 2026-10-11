namespace CG.CombatFramework
{
    public enum EffectRemoveReason { Expired, Dispelled, Died, Despawned }

    public readonly struct StatusEffectAppliedEvent
    {
        public readonly IUnit Host;
        public readonly StatusEffectInstance Effect;
        public readonly int PreviousStacks;

        public StatusEffectAppliedEvent(IUnit host, StatusEffectInstance effect, int previousStacks)
        {
            Host = host;
            Effect = effect;
            PreviousStacks = previousStacks;
        }
    }

    public readonly struct StatusEffectRemovedEvent
    {
        public readonly IUnit Host;
        public readonly StatusEffectInstance Effect;
        public readonly EffectRemoveReason Reason;

        public StatusEffectRemovedEvent(IUnit host, StatusEffectInstance effect, EffectRemoveReason reason)
        {
            Host = host;
            Effect = effect;
            Reason = reason;
        }
    }
}