using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Ammo Gate")]
    public sealed class AmmoGate : IFireGate
    {
        [SerializeField, Min(1)] private int magazineSize = 15;
        [SerializeField, Min(1), Tooltip("How much ammo is consumed per shot")] private int perShot = 1;
        [SerializeField, Min(0.01f)] private float reloadSeconds = 1.25f;
        [SerializeField, Tooltip("Will the reload start as soon as the magazine is empty")] private bool autoReloadWhenEmpty = true;
        [SerializeField] private StatType reloadSpeedStat;

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
                float speed = control.Owner.GetRate(settings.reloadSpeedStat);
                reloadDuration = reloadRemaining = Mathf.Max(0.0001f, settings.reloadSeconds / speed);
                GameEvents.Raise(control.Owner, new ReloadStartedEvent(control.Owner, control.Definition, reloadDuration));
                return true;
            }

            public void Tick(float deltaTime)
            {
                if (IsReloading)
                {
                    reloadRemaining -= deltaTime;
                    if(reloadRemaining > 0f) return;
                    GameEvents.Raise(control.Owner, new ReloadCompletedEvent(control.Owner, control.Definition));
                    reloadRemaining = 0f;
                    Current = Capacity;
                }
                else if(settings.autoReloadWhenEmpty && Current < settings.perShot) Reload();
            }

            public void AddAmmo(int amount)
            {
                Current = amount <= 0 ? Capacity : Mathf.Min(Capacity, Current + amount);
                if(Current >= Capacity) reloadRemaining = 0f;
            }

            public void Reset()
            {
                Current = Capacity;
                reloadRemaining = 0f;
            }
        }
    }
}
