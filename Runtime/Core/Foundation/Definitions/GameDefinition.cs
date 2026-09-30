using UnityEngine;

namespace CG.CombatFramework
{
    public abstract class GameDefinition : ScriptableObject, IGameDefinition
    {
        public abstract string DefinitionPrefix { get; }
        public string Id => (DefinitionPrefix + id).ToLowerInvariant().Trim().Replace(' ', '_');

        [Header("id")]
        [SerializeField] private string id = "{null}";
    }
}
