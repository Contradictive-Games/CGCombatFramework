namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Full Auto")]
    [BlockInfo("Fires every tick while held, as fast as the gates allow.", Example = "Assault rifle with a 0.1s cooldown")]
    public sealed class FullAutoFire : IFireMode
    {
        public IFireModeState CreateState() => new State();

        private sealed class State : IFireModeState
        {
            private bool held;

            public FiringPhase Phase => held ? FiringPhase.Firing : FiringPhase.Idle;

            public void Press(IFireControl control)
            {
                held = true;
                control.TryFire();
            }

            public void Release(IFireControl control) => held = false;
            public void Cancel(IFireControl control) => held = false;

            public void Tick(IFireControl control, float deltaTime)
            {
                if(held) control.TryFire(1f, true);
            }
        }
    }
}