using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Repeat")]
    [BlockInfo("Runs its emissions Count times in a row. Random spread inside is rolled again each time.", Example = "Shotgun: Repeat x8 of Delivery (Raycast with 8 degree spread)")]
    public sealed class RepeatEmission : IEmission
    {
        [SerializeField, Min(1)] private int count = 2;
        [SerializeReference, SelectType] private IEmission[] emissions = System.Array.Empty<IEmission>();

        public void Emit(in DeliveryContext ctx)
        {
            if (emissions == null) return;
            for(int i = 0; i < count; i++)
            {
                emissions.EmitAll(in ctx);
            }
        }
    }
}
