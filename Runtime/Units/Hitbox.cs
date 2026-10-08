using UnityEngine;

namespace CG.CombatFramework
{
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Hitbox")]
    public sealed class Hitbox : MonoBehaviour
    {
        [field: SerializeField] public Unit Owner;
        [field: SerializeField] public float DamageMultiplier = 1f;
        [field: SerializeField] public CombatTag Zone;

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