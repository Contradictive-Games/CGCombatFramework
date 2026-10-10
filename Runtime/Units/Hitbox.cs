using UnityEngine;

namespace CG.CombatFramework
{
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Hitbox")]
    public sealed class Hitbox : MonoBehaviour
    {
        public Unit Owner;
        public float DamageMultiplier = 1f;
        public CombatTag Zone;

        private void Reset()
        {
            Owner = GetComponentInParent<Unit>();
        }

        private void Awake()
        {
            if(Owner == null)
            {
                Owner = GetComponentInParent<Unit>();
            }
        }
    }
}