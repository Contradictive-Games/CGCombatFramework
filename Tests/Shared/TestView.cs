using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public sealed class TestView : MonoBehaviour, IUnitView
    {
        public int Binds { get; private set; }
        public int Unbinds { get; private set; }

        public void Bind(IUnit unit) => Binds++;
        public void Unbind() => Unbinds++;
    }
}
