using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Multi Emission")]
    public sealed class MultiEmission : IEmission
    {
        [SerializeReference, SelectType] private IEmission[] emissions = new IEmission[0];

        public void Emit(in DeliveryContext ctx)
        {
            if(emissions == null) return;
            for(int i =0;i<emissions.Length; i++)
            {
                emissions[i]?.Emit(in ctx);
            }
        }
    }
}