using System;
using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Resource Cost (mana, etc)")]
    public sealed class ResourceCostGate : IFireGate
    {
        [SerializeField] private ResourceType resource;
        [SerializeField, Tooltip("No random scalers")]
        private ScalableValue cost = new(10f);

        public IGateState CreateState(IFireControl control) => new State(this, control);

        private sealed class State : IGateState
        {
            private readonly ResourceCostGate settings;
            private readonly IFireControl control;
            private IResource resource;
            private bool resolved;

            public State(ResourceCostGate settings, IFireControl control)
            {
                this.settings = settings;
                this.control = control;
            }


            public bool IsReady(out FireBlockReason reason)
            {
                var ctx = Context();
                bool ok = TryGetResource(out var r) && r.Current >= settings.cost.Evaluate(in ctx);
                reason = ok ? FireBlockReason.None : FireBlockReason.NotEnoughResource;
                return ok;
            }

            public void Commit()
            {
                if(!TryGetResource(out var r)) return;
                var ctx = Context();
                r.TrySpend(settings.cost.Evaluate(in ctx), in ctx);
            }

            public void Reset() => resolved = false;

            private EffectContext Context() => new (control.Owner, control.Definition, control.Owner);

            private bool TryGetResource(out IResource found)
            {
                if (!resolved)
                {
                    resolved = true;
                    resource = null;
                    if(control.Owner.TryGet<IResourceProvider>(out var provider)) provider.TryGet(settings.resource, out resource);
                }
                found = resource;
                return found != null;
            }
        }
    }
}