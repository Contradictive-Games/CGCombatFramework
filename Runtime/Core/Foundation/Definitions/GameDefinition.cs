using UnityEngine;

namespace CG.CombatFramework
{
    public abstract class GameDefinition : ScriptableObject, IGameDefinition
    {
        public string Id => cachedId ??= $"{idNamespace}:{IdPrefix}.{id}";
        protected virtual string IdPrefix => GetType().Name.ToLowerInvariant();

        [Header("Game Definition ID")]
        [Tooltip("What proceeds everything in the ID. Useful for mods and adding custom definitions. Base game = core:")]
        [SerializeField] private string idNamespace = "core";
        [SerializeField] private string id;

        private string cachedId;

        protected virtual void OnValidate()
        {
            if(string.IsNullOrWhiteSpace(id) && !string.IsNullOrEmpty(name))
            {
                id = name.Trim().ToLowerInvariant().Replace(' ', '_');
            }
            cachedId = null;
        }
    }
}
