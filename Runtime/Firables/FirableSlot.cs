using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "Slot", menuName = Globals.k_PACKAGE_NAME + "/Firable Slot")]
    public sealed class FirableSlot : GameDefinition
    {
        protected override string IdPrefix => "firable_slot";

    }
}
