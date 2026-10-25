using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Charges")]
    public sealed class ChargesGate : IFireGate
    {
        [SerializeField, Min(1)] private int maxCharges = 2;
        [SerializeField, Min(0.01f), Tooltip("Time in seconds to refill one charge")]
        private float rechargeSeconds = 4f;
        [SerializeField, Tooltip("Optional owner stat that divides recharge time. 1 = normal, 2 = twice as fast, 0.5 = half as fast")]
        private StatType rechargeRate;

        public IGateState CreateState(IFireControl control) => new State(this, control);

        private sealed class State : IGateState, IChargeCounter
        {
            private readonly ChargesGate settings;
            private readonly IFireControl control;
            private float timer;

            public int Charges { get; private set; }
            public int MaxCharges => settings.maxCharges;
            public float RechargeProgress01 => Charges < MaxCharges ? Mathf.Clamp01(timer/ RechargeTime()) : 0f;
            public float Fill01 => Charges == 0 ? 1f - RechargeProgress01 : 0f;

            public State(ChargesGate settings, IFireControl control)
            {
                this.settings = settings;
                this.control = control;
                Charges = settings.maxCharges;
            }

            public bool IsReady(out FireBlockReason reason)
            {
                reason = Charges > 0 ? FireBlockReason.None : FireBlockReason.NoCharges;
                return Charges > 0;
            }

            public void Commit()
            {
                if(Charges > 0) Charges--;
            }

            public void Tick(float deltaTime)
            {
                if(Charges >= MaxCharges) return;
                timer += deltaTime;

                float duration = RechargeTime();
                while(timer >= duration && Charges < MaxCharges)
                {
                    timer -= duration;
                    Charges++;
                }

                if(Charges >= MaxCharges) timer = 0f;
            }

            public void Reset()
            {
                Charges = MaxCharges;
                timer = 0f;
            }

            public void AddCharges(int count)
            {
                Charges = Mathf.Min(MaxCharges, Charges + Mathf.Max(0, count));
                if(Charges >= MaxCharges) timer = 0f;
            }


            private float RechargeTime()
            {
                float rate = control.Owner.GetRate(settings.rechargeRate);
                return settings.rechargeSeconds / rate;
            }
        }
    }
}
