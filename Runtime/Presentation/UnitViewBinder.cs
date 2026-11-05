using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [UnitPart("View Binder", Description = "Binds IUnitView components in children, like health bars and hit flashes.", Color = "#79C0FF", Icon = "other", Order = -40)]
    [DisallowMultipleComponent]
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Presentation/Unit ViewBinder")]
    public sealed class UnitViewBinder : MonoBehaviour, IUnitComponent
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

        void IUnitComponent.OnReleased()
        {
            for (int i = 0; i < views.Count; i++)
            {
                views[i].Unbind();
            }
        }

        #endregion
    }
}