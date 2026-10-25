using UnityEngine;

namespace CG.CombatFramework
{
    public enum FactionRelation { Friendly, Neutral, Hostile, Unknown }

    [CreateAssetMenu(fileName = "Faction", menuName = Globals.k_PACKAGE_NAME + "/Types/Faction")]
    public sealed class Faction : GameDefinition
    {
        [Header("Faction Settings")]
        public Color Color = Color.white;
        public Faction[] HostileTo = System.Array.Empty<Faction>();
        public Faction[] FriendlyTo = System.Array.Empty<Faction>();


        public FactionRelation RelationTo(Faction other)
        {
            if(other == null) return FactionRelation.Unknown;
            if(other == this) return FactionRelation.Friendly;
            if(Contains(HostileTo, other) || Contains(other.HostileTo, this)) return FactionRelation.Hostile;
            if(Contains(FriendlyTo, other) || Contains(other.FriendlyTo, this)) return FactionRelation.Friendly;
            return FactionRelation.Neutral;
        }

        private static bool Contains(Faction[] list, Faction faction)
        {
            if(list == null) return false;
            for(int i = 0; i < list.Length; i++)
            {
                if(list[i] == faction) return true;
            }
            return false;
        }
    }

    public static class Factions
    {
        public static FactionRelation RelationTo(this IUnit self, IUnit other)
        {
            if(self == null || other == null) return FactionRelation.Unknown;
            var a = self.Root();
            var b = other.Root();
            if(ReferenceEquals(a, b)) return FactionRelation.Friendly;
            return a.Faction != null ? a.Faction.RelationTo(b.Faction) : FactionRelation.Unknown;
        }
    }
}
