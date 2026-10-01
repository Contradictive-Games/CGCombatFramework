using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    public enum DespawnReason { Recycled, Killed, Unloaded, Respawning }
    public readonly struct SpawnData
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly UnitId Id;
        public readonly IUnit Owner;
        public readonly Faction Faction;


        public readonly IReadOnlyList<StatusEffectDefinition> StartingEffects;


        public SpawnData(Vector3 position, Quaternion rotation, UnitId id = default, IUnit owner = null, 
            Faction faction = null, IReadOnlyList<StatusEffectDefinition> startingEffects = null)
        {
            Position = position;
            Rotation = rotation;
            Id = id;
            Owner = owner;
            Faction = faction;
            StartingEffects = startingEffects;
        }
    }
}