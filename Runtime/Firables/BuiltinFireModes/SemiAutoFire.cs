namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Semi Auto (per-press)")]
    [BlockInfo("Fires once per press.", Example = "Pistol, and most abilities")]
    public sealed class SemiAutoFire : IFireMode
    {
        public IFireModeState CreateState() => new State();

        private sealed class State : IFireModeState
        {
            public FiringPhase Phase => FiringPhase.Idle;
            public void Press(IFireControl control) => control.TryFire();
        }
    }
}