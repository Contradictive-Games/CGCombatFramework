using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Repeat")]
    public sealed class RepeatEmission : IEmission
    {
        [SerializeField, Min(1)] private int count = 2;
        [SerializeReference, SelectType] private IEmission emission;

        public void Emit(in DeliveryContext ctx)
        {
            if (emission == null) return;
            for (int i = 0; i < count; i++)
            {
                emission.Emit(in ctx);
            }
        }
    }
}