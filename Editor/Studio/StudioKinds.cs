using System;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    internal sealed class StudioTemplate
    {
        public readonly string Name;
        public readonly Action<ScriptableObject> Build;

        public StudioTemplate(string name, Action<ScriptableObject> build)
        {
            Name = name;
            Build = build;
        }
    }

    internal sealed class StudioKind
    {
        public readonly string Title;
        public readonly string Singular;
        public readonly Type AssetType;
        public readonly StudioTemplate[] Templates;
        public readonly BlockCategory Category;

        public StudioKind(string title, string singular, Type assetType, BlockCategory category, StudioTemplate[] templates)
        {
            Title = title;
            Singular = singular;
            AssetType = assetType;
            Category = category;
            Templates = templates ?? Array.Empty<StudioTemplate>();
        }
    }

    internal static class StudioKinds
    {
        public static readonly StudioKind[] All =
        {
            new StudioKind("Firables", "Firable", typeof(FirableDefinition), BlockCategory.FireMode, StudioTemplates.Firables),
            new StudioKind("Status Effects", "Status Effect", typeof(StatusEffectDefinition), BlockCategory.Behavior, StudioTemplates.Effects),
            new StudioKind("Damage Pipelines", "Damage Pipeline", typeof(DamagePipeline), BlockCategory.DamageStep, StudioTemplates.Pipelines)
        };

        public static Sprite IconOf(UnityEngine.Object asset)
        {
            switch(asset)
            {
                case FirableDefinition firable: return firable.Icon;
                case StatusEffectDefinition effect: return effect.Icon;
                default: return null;
            }
        }

        public static int IndexOf(Type type)
        {
            if(type == null) return -1;
            for(int i = 0; i < All.Length; i++)
            {
                if(All[i].AssetType.IsAssignableFrom(type)) return i;
            }
            return -1;
        }

        public static string NameOf(UnityEngine.Object asset)
        {
            if(asset == null) return "None";
            if(asset is GameObject prefab && prefab.TryGetComponent<Unit>(out var unit)) return unit.DisplayName;
            return asset is GameDefinition definition ? definition.DisplayName : asset.name;
        }
    }
}
