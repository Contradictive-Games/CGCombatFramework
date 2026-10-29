using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Header("Presentation")]
        [SerializeReference, SelectType, Tooltip("Models/effects/sounds")]
        private IDefinitionData[] data = System.Array.Empty<IDefinitionData>();

        public IReadOnlyList<IDefinitionData> Data => data;

        public bool TryGetData<T>(out T value) where T : class, IDefinitionData
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] is T match)
                {
                    value = match;
                    return true;
                }
            }

            value = null;
            return false;
        }


        [SerializeField, FormerlySerializedAs("Name"), FormerlySerializedAs("DisplayName")]
        private string displayName;

        public string DisplayName
        {
            get => string.IsNullOrEmpty(displayName) ? name : displayName;
            set => displayName = value;
        }

        private string cachedId;

        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrEmpty(name))
            {
                id = name.Trim().ToLowerInvariant().Replace(' ', '_');
            }
            cachedId = null;
        }
    }
}
