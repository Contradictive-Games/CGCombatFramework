using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [DisallowMultipleComponent]
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Presentation/Unit ViewBinder")]
    public sealed class UnitViewBind : MonoBehaviour, IUnitComponent
    {
        private readonly List<IUnitView> views = new();
        private IUnit owner;

        #region IUnitComponent

        void IUnitComponent.Initialize(IUnit owner)
        {
            this.owner = owner;
            GetComponentsInChildren(true, views);
        }

        void IUnitComponent.Bind() { }
        void IUnitComponent.Unbind() { }

        void IUnitComponent.OnSpawned(in SpawnData _)
        {
            for (int i = 0; i < views.Count; i++)
            {
                views[i].Bind(owner);
            }
        }

        void IUnitComponent.OnDespawned(DespawnReason _)
        {
            for (int i = 0; i < views.Count; i++)
            {
                views[i].Unbind();
            }
        }

        #endregion
    }
}