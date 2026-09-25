using UnityEngine;

namespace CG.RGS.CombatFramework
{
    public interface IWeapon
    {
        public string Name { get; }
        public float AttackRate { get; }
        public IWeaponAttackStrategy[] AttackStrategies { get; }
    } 
}
