using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "Firable", menuName = Globals.k_PACKAGE_NAME + "/Firable")]
    public sealed class FirableDefinition : GameDefinition
    {
        protected override string IdPrefix => "firable";

        [Header("Identity")]
        public Sprite Icon;
        public string Description;

        [Header("Firing")]
        [SerializeReference, SelectType] public IFireMode Mode = new SemiAutoFire();
        [SerializeReference, SelectType] public IFireGate[] Gates = System.Array.Empty<IFireGate>();
        [SerializeReference, SelectType] public IEmission[] Emissions = System.Array.Empty<IEmission>();
    }
}
