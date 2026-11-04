using UnityEngine;

namespace CG.CombatFramework.Tests
{
    [RequireComponent(typeof(TestBrain))]
    public sealed class TestNeedsBrain : MonoBehaviour, IUnitComponent
    {
        public void Initialize(IUnit owner) { }
    }
}
