using UnityEngine;

namespace CG.CombatFramework
{
    public abstract class GameDefinition : ScriptableObject, IGameDefinition
    {
        public string Id => idNamespace + definitionPrefix + id;

        [Header("Game Definition ID")]
        [Tooltip("What proceeds everything in the ID. Useful for mods and adding custom definitions. Base game = core:")]
        [SerializeField] private string idNamespace = "core:";
        [Tooltip("What comes before the ID and after the IDNamespace. EX: stat_maxhealth")]
        [SerializeField] private string definitionPrefix;
        [SerializeField] private string id;

        protected virtual void OnValidate()
        {
            
        }
    }
}
