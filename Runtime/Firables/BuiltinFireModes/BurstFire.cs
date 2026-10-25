using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Burst")]
    [BlockInfo("One press fires Shots shots, Interval seconds apart. Gates are checked and paid once per burst.", Example = "3-round burst rifle")]
    public sealed class BurstFire : IFireMode
    {
        [SerializeField, Min(2)] private int shots = 3;
        [SerializeField, Min(0f), Tooltip("Interval between shots")] private float interval = 0.08f;

        public IFireModeState CreateState() => new State(this);

        private sealed class State : IFireModeState
        {
            private readonly BurstFire settings;
            private int remaining;
            private float timer;

            public State(BurstFire settings) => this.settings = settings;

            public FiringPhase Phase => remaining > 0 ? FiringPhase.Bursting : FiringPhase.Idle;
            public float Progress01 => remaining > 0 ? 1f - remaining / (float)settings.shots : 0f;

            public void Press(IFireControl control)
            {
                if(remaining > 0 || !control.TryFire()) return;
                remaining = settings.shots - 1;
                timer = settings.interval;
            }

            public void Cancel(IFireControl control) => remaining = 0;

            public void Tick(IFireControl control, float deltaTime)
            {
                if(remaining <= 0f) return;
                if (!control.Owner.IsAlive)
                {
                    remaining = 0;
                    return;
                }

                timer -= deltaTime;
                while(timer <= 0f && remaining > 0)
                {
                    control.EmitWithoutGates();
                    remaining--;
                    timer += settings.interval;
                }
            }
        }
    }
}