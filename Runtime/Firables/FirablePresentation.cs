using System;
using UnityEngine;

namespace CG.CombatFramework
{
    [Serializable, TypeMenu("Presentation/Firable")]
    [BlockInfo("Models, effects, sounds and animation names for this firable. Read by the sample views.", Example = "Rifle: Model, Muzzle Flash, Fire Sound, Fire Trigger \"Shoot\"")]
    public sealed class FirablePresentation : IDefinitionData
    {
        public GameObject Model;
        public GameObject MuzzleFlash;
        public GameObject Beam;
        public AudioClip FireSound;
        public AudioClip LoopSound;
        public string PressTrigger;
        public string FireTrigger;
        public string ChargeBool;
        public string ChannelBool;
        public string ReloadTrigger;
    }
}
