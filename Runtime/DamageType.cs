using UnityEngine;

namespace CG.RGS.CombatFramework
{
    [CreateAssetMenu(fileName = "CG/Combat Framework/Damage Type")]
    public sealed class DamageType : ScriptableObject
    {
        public string Name = "Physical";
        public Color Color = Color.red;
        public string Description;
    }
}