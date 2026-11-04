using UnityEngine;

namespace CG.CombatFramework.Tests
{
    [UnitPart("Test Brain", Description = "Thinks", Group = "AI", Color = "#F78166", Icon = "query")]
    [RequireComponent(typeof(HealthComponent))]
    public sealed class TestBrain : MonoBehaviour, IUnitComponent
    {
        public void Initialize(IUnit owner) { }
    }
}
