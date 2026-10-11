using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Presentation/Unit ViewBinder")]
    public sealed class UnitViewBind : MonoBehaviour, IUnitComponent
    {
        private readonly List<IUnitView> views = new();
        private IUnit owner;

        public void Initialize(IUnit owner)
        {
            this.owner = owner;
            GetComponentsInChildren(true, views);
        }

        public void Bind(){ }
        public void Unbind(){ }

        public void OnSpawned(in SpawnData _)
        {
            for(int i = 0; i < views.Count; i++)
            {
                views[i].Bind(owner);
            }
        }

        public void OnDespawned(DespawnReason _)
        {
            for(int i = 0; i < views.Count; i++)
            {
                views[i].Unbind();
            }
        }
    }
}