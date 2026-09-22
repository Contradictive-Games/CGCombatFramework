using UnityEngine;

namespace CG.RGS.CombatFramework
{
    [CreateAssetMenu(fileName = "DamageType", menuName = "CG/Combat Framework/Damage Type")]
    public sealed class DamageType : ScriptableObject
    {
        [Header("UI")]
        public string Name = "DamageType";
        public Color Color = Color.red;
        
        [Header("Meta")]
        public string Description;
    }
}