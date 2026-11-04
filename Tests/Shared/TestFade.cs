using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public sealed class TestFade : MonoBehaviour, IArrivalSequence, IDepartureSequence
    {
        public float Begin(IUnit unit, IReadOnlyList<CombatTag> tags) => 0f;

        public float Begin(IUnit unit, DespawnReason reason) => 0f;
    }
}
