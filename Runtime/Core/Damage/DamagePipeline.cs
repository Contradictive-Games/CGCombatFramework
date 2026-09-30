using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "Damage Pipeline", menuName = "CG Combat Framework/Damage Pipeline")]
    public sealed class DamagePipeline : ScriptableObject
    {
        [SerializeReference, SelectType] public List<IDamageStep> Steps = new();


        public DamageCalculation Run(in DamageRequest req, IUnit targetUnit)
        {
            var calc = new DamageCalculation(in req, targetUnit);
            for(int i = 0; i < Steps.Count && !calc.IsCancelled; i++)
            {
                Steps[i]?.Process(ref calc);
            }
            calc.Amount = Mathf.Max(0f, calc.Amount);
            return calc;
        }
    }
}