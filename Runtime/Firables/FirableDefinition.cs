using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "Firable", menuName = Globals.k_PACKAGE_NAME + "/Firable")]
    public sealed class FirableDefinition : GameDefinition
    {
        protected override string IdPrefix => "firable";

        [Header("Identity")]
        public string DisplayName;
        public Sprite Icon;
        public string Description;

        [Header("Firing")]
        [SerializeReference, SelectType] public IFireMode Mode = new SemiAutoFire();
        [SerializeReference, SelectType] public List<IFireGate> Gates = new();
        [SerializeReference, SelectType] public List<IEmission> Emissions = new();
    }
}