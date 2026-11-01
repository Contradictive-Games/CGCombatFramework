using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public sealed class TestArrival : MonoBehaviour, IArrivalSequence
    {
        [SerializeField] private float duration = 1f;

        public readonly List<CombatTag> LastTags = new();

        public float Duration
        {
            get => duration;
            set => duration = value;
        }

        public int Begun { get; private set; }
        public int Stopped { get; private set; }

        public float Begin(IUnit unit, IReadOnlyList<CombatTag> tags)
        {
            Begun++;
            LastTags.Clear();
            for (int i = 0; i < tags.Count; i++)
            {
                LastTags.Add(tags[i]);
            }
            return duration;
        }

        public void Stop() => Stopped++;
    }
}
