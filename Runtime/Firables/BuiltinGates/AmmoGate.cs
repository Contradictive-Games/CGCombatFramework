using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Ammo Gate")]
    public sealed class AmmoGate : IFireGate
    {
        [SerializeField, Min(1)] private int magazineSize = 15;
        private int perShot = 1;
        private float reloadSeconds;
        private bool autoReloadWhenEmpty;
        private StatType reloadSpeed;

        public IGateState CreateState(IFireControl control) => new State(this, control);

        private sealed class State : IGateState, IReloadable
        {
            private readonly AmmoGate settings;
            private readonly IFireControl control;
            private float reloadRemaining;
            private float reloadDuration;

            public int Current { get; private set; }
            public int Capacity => settings.magazineSize;
            public bool IsReloading => reloadRemaining > 0f;
            public float ReloadProgress01 => IsReloading && reloadDuration > 0f ? 1f - reloadRemaining / reloadDuration : 0f;
            public float Fill01 => IsReloading ? 1f - ReloadProgress01 : 0f;

            public State(AmmoGate settings, IFireControl control)
            {
                this.settings= settings;
                this.control = control;
                Current = settings.magazineSize;
            }

            public bool IsReady(out FireBlockReason reason)
            {
                if(IsReloading)
                {
                    reason = FireBlockReason.Reloading;
                    return false;
                }
                if(Current < settings.perShot)
                {
                    reason = FireBlockReason.NoAmmo;
                    return false;
                }
                reason = FireBlockReason.None;
                return true;
            }

            public void Commit()
            {
                Current -= settings.perShot;
            }

            public bool Reload()
            {
                if(IsReloading || Current >= Capacity) return false;
                float speed = settings.reloadSpeed != null ? Mathf.Max(0.01f, control.Owner.GetStat(settings.reloadSpeed, 1f)) : 1f;
                reloadDuration = reloadRemaining = Mathf.Max(0.0001f, settings.reloadSeconds / speed);
                return true;
            }

            public void Tick(float deltaTime)
            {
                if (IsReloading)
                {
                    reloadRemaining -= deltaTime;
                    if(reloadRemaining > 0f) return;
                    reloadRemaining = 0f;
                    Current = Capacity;
                }
                else if(settings.autoReloadWhenEmpty && Current < settings.perShot) Reload();
            }

            public void Reset()
            {
                Current = Capacity;
                reloadRemaining = 0f;
            }
        }
    }
}