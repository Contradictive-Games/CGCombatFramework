using System;
using System.Collections.Generic;

namespace CG.CombatFramework
{
    public sealed class Firable : IFireControl, ITickable
    {
        private readonly IFireModeState mode;
        private readonly IGateState[] gates;
        private float phaseElapsed;
        private bool charged;

        public FirableDefinition Definition { get; }
        public IUnit Owner { get; }
        public FiringPhase Phase => mode.Phase;
        public float Progress01 => mode.Progress01;
        public bool IsReady => CheckReady(false);
        public IReadOnlyList<IGateState> Gates => gates;


        public Firable(FirableDefinition definition, IUnit owner)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            Definition = definition;
            Owner = owner;
            mode = (definition.Mode ?? new SemiAutoFire()).CreateState();

            var source = definition.Gates.OrEmpty();
            int count = 0;
            for(int i = 0; i <source.Length; i++)
            {
                if(source[i] != null) count++;
            }
            gates = new IGateState[count];
            for(int i = 0, j = 0; i < source.Length; i++)
            {
                if(source[i] != null) gates[j++] = source[i].CreateState(this);
            }
        }

        public void Press()
        {
            if (CombatServices.IsPaused || !Owner.IsAlive) return;
            var before = mode.Phase;
            mode.Press(this);
            AfterMode(before);
        }

        public void Release()
        {
            var before = mode.Phase;
            mode.Release(this);
            AfterMode(before);
        }

        public void Cancel()
        {
            var before = mode.Phase;
            mode.Cancel(this);
            AfterMode(before);
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < gates.Length; i++)
            {
                gates[i].Tick(deltaTime);
            }
            var before = mode.Phase;
            if (before != FiringPhase.Idle) phaseElapsed += deltaTime;
            mode.Tick(this, deltaTime);
            AfterMode(before);

            if (charged || mode.Phase != FiringPhase.Charging || mode.Progress01 < 1f) return;
            charged = true;
            GameEvents.Raise(Owner, new FirableChargedEvent(Owner, this));
        }

        public void Reset()
        {
            Cancel();
            for (int i = 0; i < gates.Length; i++)
            {
                gates[i].Reset();
            }
        }


        public bool CheckReady(bool raiseBlocked)
        {
            var reason = FireBlockReason.None;
            if (CombatServices.IsPaused) reason = FireBlockReason.Paused;
            else if (!Owner.IsAlive) reason = FireBlockReason.Dead;
            else
            {
                for (int i = 0; i < gates.Length; i++)
                {
                    if(gates[i].IsReady(out var gateReason)) continue;
                    reason = gateReason == FireBlockReason.None ? FireBlockReason.Condition : gateReason;
                    break;
                }
            }

            if (reason == FireBlockReason.None) return true;
            if (raiseBlocked)
            {
                GameEvents.Raise(Owner, new FireBlockedEvent(Owner, this, reason));
            }
            return false;
        }


        public bool TryFire(float power = 1f, bool silent = false)
        {
            if (!CheckReady(!silent)) return false;
            CommitGates();
            EmitWithoutGates(power);
            return true;
        }


        public void CommitGates()
        {
            for (int i = 0; i < gates.Length; i++)
            {
                gates[i].Commit();
            }
        }


        public void EmitWithoutGates(float power = 1f)
        {
            var ctx = DeliveryContext.FromUnit(Owner, Definition, power);
            var emissions = Definition.Emissions.OrEmpty();
            for (int i = 0; i < emissions.Length; i++)
            {
                emissions[i]?.Emit(in ctx);
            }

            GameEvents.Raise(Owner, new FiredEvent(Owner, this, power));
        }

        public bool TryGetGate<T>(out T gate) where T : class
        {
            for (int i = 0; i < gates.Length; i++)
            {
                if (gates[i] is T match)
                {
                    gate = match;
                    return true;
                }
            }
            gate = null;
            return false;
        }

        private void AfterMode(FiringPhase before)
        {
            var current = mode.Phase;
            if (current == before) return;
            var reason = before != FiringPhase.Idle ? mode.EndReason : FiringEndReason.None;
            float elapsed = phaseElapsed;
            phaseElapsed = 0f;
            charged = false;
            GameEvents.Raise(Owner, new FiringPhaseChangedEvent(Owner, this, before, current, reason, elapsed));
        }
    }
}
