using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CG.CombatFramework
{
    [DefinitionInfo("Tags", Group = "Combat", Description = "Labels in a hierarchy that units, hits and effects can carry.", Color = "#A371F7", Icon = "tag", Order = -30)]
    [CreateAssetMenu(fileName = "Tag", menuName = Globals.k_PACKAGE_NAME + "/Types/Tag")]
    public sealed class CombatTag : GameDefinition
    {
        private const int k_MaxDepth = 32;

        private static readonly List<CombatTag> buffer = new();
        private static int hierarchyVersion;

        [Header("Tag Settings")]
        [SerializeField, FormerlySerializedAs("Parent")]
        private CombatTag parent;

        private CombatTag[] lineage;
        private int lineageVersion = -1;

        public CombatTag Parent
        {
            get => parent;
            set
            {
                if(ReferenceEquals(parent, value)) return;
                parent = value;
                hierarchyVersion++;
            }
        }

        internal CombatTag[] Lineage
        {
            get
            {
                if(lineage == null || lineageVersion != hierarchyVersion)
                {
                    lineage = BuildLineage();
                    lineageVersion = hierarchyVersion;
                }
                return lineage;
            }
        }

        public bool IsOrDescendedFrom(CombatTag other)
        {
            if(other is null) return false;
            var chain = Lineage;
            for(int i = 0; i < chain.Length; i++)
            {
                if(ReferenceEquals(chain[i], other)) return true;
            }
            return false;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            hierarchyVersion++;
        }

        private CombatTag[] BuildLineage()
        {
            buffer.Clear();
            for(var tag = this; !(tag is null) && buffer.Count < k_MaxDepth && !buffer.Contains(tag); tag = tag.parent)
            {
                buffer.Add(tag);
            }
            var built = buffer.ToArray();
            buffer.Clear();
            return built;
        }
    }
}
