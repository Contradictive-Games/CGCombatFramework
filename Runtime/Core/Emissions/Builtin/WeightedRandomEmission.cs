using System;
using UnityEngine;

namespace CG.CombatFramework
{
    [Serializable]
    public sealed class WeightedEmission
    {
        [Min(0f)] public float Weight = 1f;
        [SerializeReference, SelectType] public IEmission[] Emissions = Array.Empty<IEmission>();
    }

    [Serializable, TypeMenu("Weighted Random")]
    [BlockInfo("Picks one option at random, weighted, and runs its emissions.", Example = "Chaos bolt: Fire weight 3, Frost 2, Lightning 1")]
    public sealed class WeightedRandomEmission : IEmission
    {
        [SerializeField]
        private WeightedEmission[] options = Array.Empty<WeightedEmission>();

        public void Emit(in DeliveryContext ctx)
        {
            float total = 0f;
            for(int i = 0; i < options.Length; i++)
            {
                if(options[i] != null) total += options[i].Weight;
            }
            if(total <= 0f) return;

            float roll = ctx.Effect.Random.Range(0f, total);
            for(int i =0; i < options.Length; i++)
            {
                var option = options[i];
                if(option == null) continue;
                roll -= option.Weight;
                if(roll <= 0f)
                {
                    option.Emissions.EmitAll(in ctx);
                    return;
                }
            }
        }
    }
}
