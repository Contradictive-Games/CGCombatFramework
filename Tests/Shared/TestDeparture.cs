using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public sealed class TestDeparture : MonoBehaviour, IDepartureSequence
    {
        [SerializeField] private float duration = 1f;

        public float Duration
        {
            get => duration;
            set => duration = value;
        }

        public int Begun { get; private set; }
        public int Stopped { get; private set; }
        public DespawnReason LastReason { get; private set; }
        public IUnit Killer { get; private set; }
        public bool SawDeath { get; private set; }

        public float Begin(IUnit unit, DespawnReason reason)
        {
            Begun++;
            LastReason = reason;
            SawDeath = unit.TryGet<IHealth>(out var health) && health.LastDeath.HasValue;
            Killer = SawDeath ? health.LastDeath.Value.Killer : null;
            return duration;
        }

        public void Stop() => Stopped++;
    }
}
