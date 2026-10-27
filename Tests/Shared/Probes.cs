namespace CG.CombatFramework.Tests
{
    /// <summary>An emission that counts instead of hitting anything.</summary>
    public sealed class CountingEmission : IEmission
    {
        public int Count;
        public float LastPower;

        public void Emit(in DeliveryContext ctx)
        {
            Count++;
            LastPower = ctx.Effect.Power;
        }
    }

    /// <summary>An emission that keeps every context it was given, for checking origins and directions.</summary>
    public sealed class RecordingEmission : IEmission
    {
        public readonly System.Collections.Generic.List<DeliveryContext> Received = new();

        public DeliveryContext Last => Received[Received.Count - 1];

        public void Emit(in DeliveryContext ctx) => Received.Add(ctx);
    }

    public sealed class CountingPayload : IPayload
    {
        public int Count;
        public EffectContext Last;

        public void Apply(in EffectContext ctx)
        {
            Count++;
            Last = ctx;
        }
    }

    /// <summary>A condition with a fixed answer.</summary>
    public sealed class FixedCondition : ICondition
    {
        public bool Pass;

        public FixedCondition(bool pass) => Pass = pass;

        public bool Evaluate(in EffectContext ctx) => Pass;
    }

    public sealed class CountingBehaviour : IStatusEffectBehavior
    {
        public int Applied, StackChanges, Ticks, Removed;
        public EffectRemoveReason LastReason;

        public void OnApplied(StatusEffectInstance effect) => Applied++;
        public void OnStacksChanged(StatusEffectInstance effect, int previousStacks) => StackChanges++;
        public void OnTick(StatusEffectInstance effect) => Ticks++;

        public void OnRemoved(StatusEffectInstance effect, EffectRemoveReason reason)
        {
            Removed++;
            LastReason = reason;
        }
    }

    public sealed class KnockbackProbe : IKnockbackReceiver
    {
        public int Count;
        public UnityEngine.Vector3 Last;

        public void ApplyKnockback(UnityEngine.Vector3 impulse, in EffectContext ctx)
        {
            Count++;
            Last = impulse;
        }
    }
}
