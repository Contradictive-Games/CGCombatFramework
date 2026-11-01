using System;
using System.Collections.Generic;

namespace CG.CombatFramework
{
    /// <summary>
    /// An authored game definition to allow various things to be referred to by an ID
    /// </summary>
    public interface IGameDefinition
    {
        public string Id { get; }
        public IReadOnlyList<IDefinitionData> Data => Array.Empty<IDefinitionData>();

        public bool TryGetData<T>(out T data) where T : class, IDefinitionData
        {
            data = null;
            return false;
        }
    }
}
