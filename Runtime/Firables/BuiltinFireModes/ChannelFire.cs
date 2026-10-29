using UnityEngine;

namespace CG.CombatFramework
{
    public enum ChannelCommit { OnStart, EveryPulse, OnEnd }

    [System.Serializable, TypeMenu("Channel")]
    [BlockInfo("Holding (or toggling) starts a channel that pulses every Pulse Interval until released, Max Duration runs out or a Keep Channeling While condition fails. Commit picks when gates are paid, and Power follows the curve.", Example = "Drain Life: a pulse every 0.25s for up to 3s, broken by stuns")]
    public sealed class ChannelFire : IFireMode
    {
        [SerializeField, Tooltip("Press to start and press again to stop. Releasing does nothing. False = must be held")]
        private bool toggle = false;
        [SerializeField, Min(0.01f), Tooltip("Seconds between each pulse")]
        private float pulseInterval = 0.25f;
        [SerializeField, Min(0f), Tooltip("Max channel time. 0 = until released")]
        private float maxDuration = 3f;
        [SerializeField, Tooltip("Pulse the moment the channeling starts")]
        private bool pulseOnStart = true;
        [SerializeField, Tooltip("On start: pay once, Every pulse: each pulse pays, and channel ends when one cannot pay. On end: check at start pay when ends")]
        private ChannelCommit commit = ChannelCommit.OnStart;
        [SerializeField, Tooltip("x: seconds channeled, y: power of pulse at X")]
        private AnimationCurve powerOverTime = AnimationCurve.Constant(0f, 1f, 1f);
        [SerializeReference, SelectType, Tooltip("Checked on owner before each pulse, channel breaks if any fail")]
        private ICondition[] keepChannelingWhile = System.Array.Empty<ICondition>();

        public IFireModeState CreateState() => new State(this);

        private sealed class State : IFireModeState
        {
            private readonly ChannelFire settings;
            private bool channeling;
            private float elapsed;
            private float pulseTimer;

            public State(ChannelFire settings) => this.settings = settings;

            public FiringPhase Phase => channeling ? FiringPhase.Channeling : FiringPhase.Idle;
            public float Progress01 => channeling && settings.maxDuration > 0f ? Mathf.Clamp01(elapsed / settings.maxDuration) : 0f;
            public FiringEndReason EndReason { get; private set; }

            public void Press(IFireControl control)
            {
                if (channeling)
                {
                    if(settings.toggle) End(control, FiringEndReason.Released);
                    return;
                }
                if(channeling || !control.CheckReady(true)) return;

                channeling = true;
                elapsed = 0f;
                pulseTimer = settings.pulseInterval;
                if(settings.commit == ChannelCommit.OnStart) control.CommitGates();

                if(settings.pulseOnStart) Pulse(control);
            }

            public void Release(IFireControl control)
            {
                if(channeling && !settings.toggle) End(control, FiringEndReason.Released);
            }

            public void Cancel(IFireControl control)
            {
                if(channeling) End(control, FiringEndReason.Cancelled);
            }

            public void Tick(IFireControl control, float deltaTime)
            {
                if(!channeling) return;
                if(!control.Owner.IsAlive || !StillAllowed(control))
                {
                    End(control, FiringEndReason.Interrupted);
                    return;
                }
                elapsed += deltaTime;
                pulseTimer -= deltaTime;
                while(channeling && pulseTimer <= 0f)
                {
                    pulseTimer += settings.pulseInterval;
                    Pulse(control);
                }

                if(channeling && settings.maxDuration > 0f && elapsed >= settings.maxDuration)
                {
                    End(control, FiringEndReason.Completed);
                }
            }

            private void Pulse(IFireControl control)
            {
                float power = settings.powerOverTime.Evaluate(elapsed);
                if(settings.commit != ChannelCommit.EveryPulse)
                {
                    control.EmitWithoutGates(power);
                    return;
                }

                if(!control.TryFire(power, false)) End(control, FiringEndReason.Interrupted);
            }

            private bool StillAllowed(IFireControl control)
            {
                var ctx = new EffectContext(control.Owner, control.Definition, control.Owner);
                return settings.keepChannelingWhile.All(in ctx);
            }

            private void End(IFireControl control, FiringEndReason reason)
            {
                channeling = false;
                EndReason = reason;
                if(settings.commit == ChannelCommit.OnEnd && reason != FiringEndReason.Cancelled) control.CommitGates();
            }
        }
    }
}
