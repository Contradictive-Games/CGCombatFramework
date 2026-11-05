using UnityEngine;

namespace CG.CombatFramework
{
    [UnitPart("Despawn On Death", Description = "Despawns the unit a few seconds after it dies, so bodies clean themselves up.", Color = "#F0883E", Icon = "damage", Order = -70)]
    [DisallowMultipleComponent]
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Unit Components/Despawn On Death")]
    public sealed class DespawnOnDeath : MonoBehaviour, IUnitComponent, IHealthListener, ITickable
    {
        [SerializeField, Min(0f), Tooltip("Seconds of combat time the body stays after death before it despawns")]
        private float delay = 3f;

        private IUnit owner;
        private IHealth health;
        private float remaining = -1f;

        public float Delay => delay;
        public bool IsScheduled => remaining >= 0f;

        void IUnitComponent.Initialize(IUnit owner) => this.owner = owner;

        void IUnitComponent.Bind()
        {
            if(owner.TryGet(out health)) health.AddListener(this);
        }

        void IUnitComponent.Unbind() => health?.RemoveListener(this);
        void IUnitComponent.OnSpawned(in SpawnData data) => remaining = -1f;
        void IUnitComponent.OnDespawned(DespawnReason reason) => remaining = -1f;

        void IHealthListener.OnDied(in DiedEvent evt)
        {
            remaining = delay;
            GameEvents.Raise(owner, new DespawnScheduledEvent(owner, delay, DespawnReason.Killed));
        }

        void IHealthListener.OnRevived(in RevivedEvent evt) => remaining = -1f;

        public void Tick(float deltaTime)
        {
            if(remaining < 0f) return;
            remaining -= deltaTime;
            if(remaining > 0f) return;
            remaining = -1f;
            CombatServices.Spawner.Despawn(owner, DespawnReason.Killed);
        }
    }
}
