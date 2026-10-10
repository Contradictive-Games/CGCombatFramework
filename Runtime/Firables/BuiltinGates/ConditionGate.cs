using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Conditions")]
    public sealed class ConditionGate : IFireGate
    {
        [SerializeReference, SelectType] private ICondition[] conditions = System.Array.Empty<ICondition>();

        public IGateState CreateState(IFireControl control) => new State(this, control);

        private sealed class State : IGateState
        {
            private readonly ConditionGate settings;
            private readonly IFireControl control;

            public State(ConditionGate settings, IFireControl control)
            {
                this.settings = settings;
                this.control = control;
            }


            public bool IsReady(out FireBlockReason reason)
            {
                var ctx = new EffectContext(control.Owner, control.Definition, control.Owner);
                bool ok = settings.conditions.All(in ctx);
                reason = ok ? FireBlockReason.None : FireBlockReason.Condition;
                return ok;
            }

            public void Commit(){ }
        }
    }
}