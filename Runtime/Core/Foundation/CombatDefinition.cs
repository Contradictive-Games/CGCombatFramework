using UnityEngine;

namespace CG.CombatFramework
{
    public abstract class CombatDefinition : ScriptableObject, IGameDefinition
    {
        [SerializeField] private string id;

        public string Id => id;


        #if UNITY_EDITOR

        protected virtual void OnValidate()
        {
            if(string.IsNullOrWhiteSpace(id) && !string.IsNullOrEmpty(name)) id = name.Trim().ToLowerInvariant().Replace(' ', '_');
        }
        #endif
    }
}