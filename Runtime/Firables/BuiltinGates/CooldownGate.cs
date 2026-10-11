using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Cooldown")]
    public sealed class CooldownGate : IFireGate
    {
        [SerializeField, Min(0f)] private float seconds = 0.5f;
        [SerializeField, Tooltip("Optional stat (0 => 1). 0.25 = 25% shorter")] private StatType cooldownReduction;
        [SerializeField, Tooltip("Optional stat, divides the cooldown. 1 = normal")] private StatType rate;


        public IGateState CreateState(IFireControl control) => new State(this, control);

        private sealed class State : IGateState, ICooldown
        {
            private readonly CooldownGate settings;
            private readonly IFireControl control;
            private float remaining;
            private float duration;
            public float Remaining => Mathf.Max(0f, remaining);
            public float Duration => duration;

            public State(CooldownGate settings, IFireControl control)
            {
                this.settings = settings;
                this.control = control;
            }

            public float Fill01 => duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;

            public bool IsReady(out FireBlockReason reason)
            {
                reason = remaining > 0f ? FireBlockReason.Cooldown : FireBlockReason.None;
                return remaining <= 0f;
            }

            public void Commit()
            {
                var owner = control.Owner;
                float reduction = Mathf.Clamp01(owner.GetStat(settings.cooldownReduction));
                float speed = settings.rate != null ? Mathf.Max(0.01f, owner.GetStat(settings.rate, 1f)) : 1f;
                duration = settings.seconds * (1f - reduction) / speed;
                remaining = Mathf.Min(remaining, 0f) + duration;
            }

            public void Tick(float deltaTime)
            {
                if(remaining > 0f) remaining -= deltaTime;
            }

            public void Reduce(float seconds) => remaining = Mathf.Max(0f, remaining - Mathf.Max(0f, seconds));


            public void Reset()
            {
                remaining = 0f;
                duration = 0f;
            }
        }
    }
}