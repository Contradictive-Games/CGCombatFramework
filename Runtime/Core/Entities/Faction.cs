using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    public enum FactionRelation { Friendly, Neutral, Hostile, Unknown }

    [CreateAssetMenu(fileName = "Faction", menuName = "Contradictive Games/Combat/Faction")]
    public sealed class Faction : ScriptableObject, IGameDefinition
    {
        public string Id => "core:faction_" + Name.ToLowerInvariant().Trim().Replace(' ', '_');
        public string Name;
        public Color Color = Color.white;
        public List<Faction> HostileTo = new();
        public List<Faction> FriendlyTo = new();

        public FactionRelation RelationTo(Faction other)
        {
            if(other == null) return FactionRelation.Unknown;
            if(other == this) return FactionRelation.Friendly;
            if(HostileTo.Contains(other) || other.HostileTo.Contains(this)) return FactionRelation.Hostile;
            if(FriendlyTo.Contains(other) || other.FriendlyTo.Contains(this)) return FactionRelation.Friendly;
            return FactionRelation.Neutral;
        }
    }
}