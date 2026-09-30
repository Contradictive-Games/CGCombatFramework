using UnityEngine;

namespace CG.CombatFramework
{
    public interface IUnit
    {
        public string DisplayName { get; }
        public Transform Transform { get; }
        
        public bool IsAlive { get; }

        public IUnit Owner { get; }

        public bool TryGet<T>(out T capability) where T : class;
    }
}