using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Repeat")]
    public sealed class RepeatEmission : IEmission
    {
        [SerializeField, Min(1)] private int count = 2;
        [SerializeReference, SelectType] private IEmission[] emissions;

        public void Emit(in DeliveryContext ctx)
        {
            if (emissions == null) return;
            Emissions.EmitAll(emissions, in ctx);
        }
    }
}
