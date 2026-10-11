using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "Damage Pipeline", menuName = "CG Combat Framework/Damage Pipeline")]
    public sealed class DamagePipeline : ScriptableObject
    {
        [SerializeReference, SelectType] public IDamageStep[] Steps = System.Array.Empty<IDamageStep>();


        public DamageCalculation Run(in DamageRequest req, IUnit targetUnit)
        {
            var calc = new DamageCalculation(in req, targetUnit);
            var steps = Steps.OrEmpty();
            for(int i = 0; i < steps.Length && !calc.IsCancelled; i++)
            {
                steps[i]?.Process(ref calc);
            }
            calc.Amount = Mathf.Max(0f, calc.Amount);
            return calc;
        }
    }
}