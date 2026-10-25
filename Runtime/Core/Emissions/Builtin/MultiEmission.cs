using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Multi Emission")]
    [BlockInfo("Runs every emission in the list from the same origin and aim.", Example = "Fire a projectile and a muzzle burst at the same time")]
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