using UnityEngine;

namespace CG.CombatFramework
{
    public enum ChannelCommit { OnStart, EveryPulse, OnEnd }
    public enum ChannelEndReason { Release, Completed, Interrupted, Cancelled }

    public readonly struct ChannelEvent
    {
        public readonly IUnit Owner;
        public readonly FirableDefinition Definition;
        public readonly bool Started;
        public readonly ChannelEndReason EndReason;
        public readonly float Elapsed;


        public ChannelEvent(IUnit owner, FirableDefinition definition, bool started, ChannelEndReason endReason, float elapsed)
        {
            Owner = owner;
            Definition = definition;
            Started = started;
            EndReason = endReason;
            Elapsed = elapsed;
        }
    }


    [System.Serializable, TypeMenu("Channel")]
    public sealed class ChannelFire : IFireMode
    {
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

            public void Press(IFireControl control)
            {
                if(channeling || !control.CheckReady(true)) return;

                channeling = true;
                elapsed = 0f;
                pulseTimer = settings.pulseInterval;
                if(settings.commit == ChannelCommit.OnStart) control.CommitGates();
                
                GameEvents.Raise(control.Owner, new ChannelEvent(control.Owner, control.Definition, true, default, 0f));

                if(settings.pulseOnStart) Pulse(control);
            }

            public void Release(IFireControl control)
            {
                if(channeling) End(control, ChannelEndReason.Release);
            }

            public void Cancel(IFireControl control)
            {
                if(channeling) End(control, ChannelEndReason.Cancelled);
            }

            public void Tick(IFireControl control, float deltaTime)
            {
                if(!channeling) return;
                if(!control.Owner.IsAlive || !StillAllowed(control))
                {
                    End(control, ChannelEndReason.Interrupted);
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
                    End(control, ChannelEndReason.Completed);
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

                if(!control.TryFire(power, false)) End(control, ChannelEndReason.Interrupted);
            }

            private bool StillAllowed(IFireControl control)
            {
                var ctx = new EffectContext(control.Owner, control.Definition, control.Owner);
                return settings.keepChannelingWhile.All(in ctx);
            }

            private void End(IFireControl control, ChannelEndReason reason)
            {
                channeling = false;
                if(settings.commit == ChannelCommit.OnEnd && reason != ChannelEndReason.Cancelled) control.CommitGates();
                GameEvents.Raise(control.Owner, new ChannelEvent(control.Owner, control.Definition, false, reason, elapsed));
            }
        }
    }
}
