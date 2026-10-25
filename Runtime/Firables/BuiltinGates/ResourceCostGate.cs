using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Resource Cost (mana, etc)")]
    [BlockInfo("Needs enough of a resource and spends it on fire. Cannot Deplete stops the cost from taking it to 0.", Example = "Fireball costs 25 Mana; blood magic costs Health with Cannot Deplete on")]
    public sealed class ResourceCostGate : IFireGate
    {
        [SerializeField] private ResourceType resource;
        [SerializeField, Tooltip("No random scalers")]
        private ScalableValue cost = new(10f);
        [SerializeField, Tooltip("Paying can not empty. Useful for health costs to not kill the player. Turn to true if you don't care about that")]
        private bool cannotDeplete = false;

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
                float cost = settings.cost.Evaluate(in ctx);
                bool ok = TryGetResource(out var r) && (settings.cannotDeplete ? r.Current > cost : r.Current >= cost);
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
