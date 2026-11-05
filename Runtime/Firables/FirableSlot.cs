using UnityEngine;

namespace CG.CombatFramework
{
    [DefinitionInfo("Firable Slots", Group = "Combat", Description = "Named loadout slots that firables equip into.", Color = "#8B949E", Icon = "slot", Order = -10)]
    [CreateAssetMenu(fileName = "Slot", menuName = Globals.k_PACKAGE_NAME + "/Firable Slot")]
    public sealed class FirableSlot : GameDefinition
    {
        protected override string IdPrefix => "firable_slot";

    }
}
