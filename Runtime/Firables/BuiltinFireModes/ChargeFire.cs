using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Charged")]
    [BlockInfo("Holding charges up over Max Charge Time. Releasing fires with power equal to the charge (0 to 1), and below Minimum Charge it cancels. Can fire by itself when full.", Example = "Bow: add a Power scaler to the damage so a full draw hits hardest")]
    public sealed class ChargeFire : IFireMode
    {
        [SerializeField, Min(0.01f)] private float maxChargeTime = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("Releasing below this percent charge cancels")] private float minimumCharge = 0.2f;
        [SerializeField, Tooltip("If it will automatically fire at 100% charge")] private bool fireWhenFull = true;


        public IFireModeState CreateState() => new State(this);

        private sealed class State : IFireModeState
        {
            private readonly ChargeFire settings;
            private bool charging;
            private float elapsed;

            public State(ChargeFire settings) => this.settings = settings;

            public FiringPhase Phase => charging ? FiringPhase.Charging : FiringPhase.Idle;
            public float Progress01 => charging ? Mathf.Clamp01(elapsed / settings.maxChargeTime) : 0f;

            public void Press(IFireControl control)
            {
                if(charging || !control.CheckReady(true)) return;
                charging = true;
                elapsed = 0f;
            }

            public void Release(IFireControl control)
            {
                if(charging) Fire(control);
            }

            public void Cancel(IFireControl control) => charging = false;

            public void Tick(IFireControl control, float deltaTime)
            {
                if(!charging) return;
                elapsed += deltaTime;
                if(settings.fireWhenFull && elapsed >= settings.maxChargeTime) Fire(control);
            }

            private void Fire(IFireControl control)
            {
                float power = Progress01;
                charging = false;
                if(power >= settings.minimumCharge) control.TryFire(power);
            }
        }
    }
}