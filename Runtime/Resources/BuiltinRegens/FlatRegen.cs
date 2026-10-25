using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Flat Per Second")]
    [BlockInfo("Restores Per Second while below max.", Example = "5 Mana per second")]
    public sealed class FlatRegen : IRegenRule
    {
        [SerializeField] private float perSecond = 5f;
        public float Evaluate(in RegenState state, float deltaTime)
        {
            return state.Current < state.Max ? perSecond * deltaTime : 0f;
        }
    }
}